"""Bulk command execution + reference inspection/validation."""
import re

from mcp.server.fastmcp.exceptions import ToolError

from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind
from .tool_specs import _SPECS

_send = None
_args = None

# DEV-55 [B3-#11]: ceiling for the caller-tunable inner timeout_ms sent to
# Unity's batch executor. Must stay below MCPServer.cs's hardcoded outer
# "batch" dispatch watchdog (65s) with margin -- otherwise the outer watchdog
# kills the whole command before Unity's own soft-timeout can return a
# graceful partial result. Mirrored by test_timing_invariants.py.
_TIMEOUT_MS_CEILING = 60000

# C#'s CommandRouter.Registration.cs "batch" dispatch default when the
# timeout_ms arg is omitted -- keep in sync (guarded by
# test_timing_invariants.py::test_batch_default_ms_source_matches_fixture).
_UNITY_BATCH_DEFAULT_MS = 25000

# Dispatch/serialization overhead subtracted from the caller's `timeout`
# before deriving the inner timeout_ms budget sent to Unity.
_BATCH_DISPATCH_GUARD_S = 5

# Tools that require their typed MCP wrapper (Python DSL expansion) — rejected inside batch.
_dsl_tools: set[str] = set()

# Python-only params that don't exist in C# — strip before forwarding to batch.
# Key: command name, Value: set of param names to remove.
_PYTHON_ONLY_PARAMS: dict[str, set[str]] = {
    "get_component": {"full"},
    "get_hierarchy": {"full"},
    "inspect": {"full"},
}

_SUMMARY_RE = re.compile(
    r"(?m)^ok:(?P<ok>\d+)(?: err:(?P<err>\d+))?"
    r"(?: skip:(?P<skip>\d+))?"
    r"(?P<timeout> timeout:\d+)?[ \t\r\n]*\Z"
)


def _strip_python_params(line: str) -> str:
    parts = line.strip().split(None, 1)
    if len(parts) < 2:
        return line
    cmd = parts[0]
    params_to_strip = _PYTHON_ONLY_PARAMS.get(cmd)
    if not params_to_strip:
        return line
    rest = parts[1]
    for p in params_to_strip:
        rest = re.sub(rf'\b{p}=(?:"[^"]*"|\'[^\']*\'|\S+)\s*', '', rest)
    return f"{cmd} {rest}".rstrip()


def _add_preflight_errors_to_summary(result: str, count: int) -> str:
    """Merge Python-side filtered errors into Unity's terminal summary."""
    if count <= 0:
        return result

    def _replace(match: re.Match[str]) -> str:
        total_errors = int(match.group("err") or "0") + count
        skip = match.group("skip")
        skip_part = f" skip:{skip}" if skip else ""
        return f"ok:{match.group('ok')} err:{total_errors}{skip_part}{match.group('timeout') or ''}"

    return _SUMMARY_RE.sub(_replace, result)


def _check_completeness(commands: str, result: str) -> str:
    """Prepend warning if summary counts don't cover all sent commands."""
    n_sent = sum(
        1 for line in commands.splitlines()
        if line.strip() and not line.strip().startswith("#")
    )
    m = _SUMMARY_RE.search(result)
    if not m:
        return result
    ok = int(m.group("ok") or 0)
    err = int(m.group("err") or 0)
    skip = int(m.group("skip") or 0)
    timeout_part = (m.group("timeout") or "").strip()
    timeout_val = int(timeout_part.split(":")[-1]) if timeout_part else 0
    if ok + err + skip + timeout_val != n_sent:
        unaccounted = n_sent - (ok + err + skip + timeout_val)
        return f"[BATCH_INCOMPLETE: {unaccounted} unaccounted]\n{result}"
    return result


def _preprocess_continue_mode(commands: str) -> tuple[str, list[str], list[int]]:
    """Filter/validate commands for on_error=continue. Returns (commands, pre_errors, orig_indices)."""
    pre_errors: list[str] = []
    orig_indices: list[int] = []
    clean: list[str] = []
    command_index = 0
    for line in commands.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            clean.append(line)
            continue
        i = command_index
        command_index += 1
        cmd = stripped.split()[0]
        if cmd in _dsl_tools:
            pre_errors.append(f"[{i}] err: '{cmd}' requires typed MCP tool, not batch")
            continue
        spec = _SPECS.get(cmd)
        if spec and spec.direct_only:
            pre_errors.append(f"[{i}] err: '{cmd}' is direct-only; call it as a typed MCP tool, not in batch")
            continue
        clean.append(_strip_python_params(line))
        orig_indices.append(i)
    if pre_errors and not clean:
        raise ToolError("\n".join(pre_errors))
    return "\n".join(clean), pre_errors, orig_indices


def _preprocess_stop_mode(commands: str) -> str:
    """Validate commands for on_error=stop. Raises ToolError on first DSL/direct-only hit."""
    stripped_lines: list[str] = []
    for line in commands.splitlines():
        cmd = line.strip().split()[0] if line.strip() else ""
        if cmd in _dsl_tools:
            raise ToolError(f"{cmd} requires typed MCP tool (Python DSL expansion), not batch")
        spec = _SPECS.get(cmd)
        if spec and spec.direct_only:
            raise ToolError(f"'{cmd}' is direct-only; call it as a typed MCP tool, not in batch")
        stripped_lines.append(_strip_python_params(line))
    return "\n".join(stripped_lines)


def _build_send_args(
    commands: str, on_error: str, timeout_ms: int, atomic: bool, validate_aliases: bool
) -> dict:
    """Build TCP args dict, omitting default/falsy values."""
    args: dict = {"commands": commands}
    if on_error != "continue":
        args["on_error"] = on_error
    # _UNITY_BATCH_DEFAULT_MS is C#'s own hardcoded internal batch-executor
    # default (NOT Python's local default above) -- only omit timeout_ms
    # when it happens to match what Unity would use anyway. Post-A4 the two
    # deliberately diverge (75s client default -> clamped to the 60000ms
    # DEV-55 ceiling, still above Unity's old 25000ms floor), so timeout_ms
    # is now sent on effectively every call; that's intentional, not a
    # token-economy regression (see test_batch_timeout.py::test_batch_default_timeout_75s).
    if timeout_ms != _UNITY_BATCH_DEFAULT_MS:
        args["timeout_ms"] = timeout_ms
    if atomic:
        args["atomic"] = "true"
    if validate_aliases:
        args["validate_aliases"] = "true"
    return args


def _remap_indices(match: re.Match, orig_indices: list[int]) -> str:
    """Remap a Unity-side zero-based index to original command ordinal."""
    n = int(match.group(1))
    return f"[{orig_indices[n]}]" if 0 <= n < len(orig_indices) else match.group(0)


def _merge_pre_errors(result: str, pre_errors: list[str], orig_indices: list[int]) -> str:
    """Prepend Python-side pre_errors to Unity result and remap indices."""
    if orig_indices:
        result = re.sub(
            r'(?m)^\[(\d+)\]',
            lambda m: _remap_indices(m, orig_indices),
            result,
        )
    result = _add_preflight_errors_to_summary(result, len(pre_errors))
    return "\n".join(pre_errors) + "\n" + result


async def batch(commands: str, on_error: str = "continue", timeout: float = 75.0,
                atomic: bool = False, validate_aliases: bool = False) -> str:
    """Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop (default continue). timeout: seconds (default 75; inner soft-timeout capped at 60s). atomic: reverts prior Undo-recorded mutations on failure; external/file/asset/package/process effects may remain. PREFER over individual tool calls."""
    pre_errors: list[str] = []
    orig_indices: list[int] = []
    if on_error == "continue":
        commands, pre_errors, orig_indices = _preprocess_continue_mode(commands)
    else:
        commands = _preprocess_stop_mode(commands)
    timeout_ms = min(_TIMEOUT_MS_CEILING, max(1000, int((timeout - _BATCH_DISPATCH_GUARD_S) * 1000)))
    args = _build_send_args(commands, on_error, timeout_ms, atomic, validate_aliases)
    result = await _send("batch", args, timeout=timeout)
    result = _check_completeness(commands, result)
    if pre_errors:
        return _merge_pre_errors(result, pre_errors, orig_indices)
    return result


async def references(action: str, path: str, children: bool = False, depth: int = 1,
                     source: str | None = None, target: str | None = None,
                     mappings: str | None = None) -> str:
    """References. action: get|find_to|remap. get: outgoing refs. find_to: reverse search. remap: remap refs."""
    return await _send("references", _args(
        action=action, path=path,
        children="true" if children else None,
        depth=depth if depth != 1 else None,
        source=source, target=target, mappings=mappings,
    ))


async def validate_references(path: str, depth: int = 3, verbose: bool = False, ignore_optional: bool = False) -> str:
    """Validate all ObjectReference fields under path recursively.
    Returns [ERROR]/[MISSING] for broken refs. Summary: "N ERROR, M OK".
    Use depth=1 for quick top-level scan, depth=3-5 for full subtree.
    verbose=True also shows [OK] lines (off by default to save tokens).
    ignore_optional=True skips fields marked [Optional] (reduces noise)."""
    return await _send("validate_references", _args(
        path=path, depth=depth,
        verbose="true" if verbose else None,
        ignore_optional="true" if ignore_optional else None))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(batch)
    mcp.tool(annotations=_RW)(references)
    mcp.tool(annotations=_RO)(validate_references)
