"""Bulk command execution + reference inspection/validation."""
import re

from mcp.server.fastmcp.exceptions import ToolError

from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind
from .tool_specs import _SPECS

_send = None
_args = None

# Tools that require their typed MCP wrapper (Python DSL expansion) — rejected inside batch.
_dsl_tools: set[str] = set()

# Python-only params that don't exist in C# — strip before forwarding to batch.
# Key: command name, Value: set of param names to remove.
_PYTHON_ONLY_PARAMS: dict[str, set[str]] = {
    "get_component": {"full"},
    "get_hierarchy": {"full"},
    "inspect": {"full"},
}

_STRIP_RE = re.compile(r'\b({keys})=\S+\s*')


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
        rest = re.sub(rf'\b{p}=\S+\s*', '', rest)
    return f"{cmd} {rest}".rstrip()


async def batch(commands: str, on_error: str = "continue", timeout: float = 75.0,
                atomic: bool = False, validate_aliases: bool = False) -> str:
    """Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop (default continue). timeout: seconds (default 75). atomic: True reverts ALL prior ops on first failure (Unity Undo); execute_code fs side-effects NOT reverted. PREFER over individual tool calls."""
    pre_errors: list[str] = []
    orig_indices: list[int] = []  # filtered-line j (1-based) → original line number
    if on_error == "continue":
        clean: list[str] = []
        for i, line in enumerate(commands.splitlines(), 1):
            cmd = line.strip().split()[0] if line.strip() else ""
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
        commands = "\n".join(clean)
    else:
        stripped: list[str] = []
        for line in commands.splitlines():
            cmd = line.strip().split()[0] if line.strip() else ""
            if cmd in _dsl_tools:
                raise ToolError(f"{cmd} requires typed MCP tool (Python DSL expansion), not batch")
            spec = _SPECS.get(cmd)
            if spec and spec.direct_only:
                raise ToolError(f"'{cmd}' is direct-only; call it as a typed MCP tool, not in batch")
            stripped.append(_strip_python_params(line))
        commands = "\n".join(stripped)
    timeout_ms = max(1000, int((timeout - 5) * 1000))
    args = {"commands": commands}
    if on_error != "continue":
        args["on_error"] = on_error
    # 25000 is C#'s own hardcoded internal batch-executor default (NOT Python's
    # local default above) -- only omit timeout_ms when it happens to match
    # what Unity would use anyway. Post-A4 the two deliberately diverge (75s
    # client default -> 70000ms > Unity's old 25000ms floor), so timeout_ms is
    # now sent on effectively every call; that's intentional, not a token-economy
    # regression (see test_batch_timeout.py::test_batch_default_timeout_75s).
    if timeout_ms != 25000:
        args["timeout_ms"] = timeout_ms
    if atomic:
        args["atomic"] = "true"
    if validate_aliases:
        args["validate_aliases"] = "true"
    result = await _send("batch", args, timeout=timeout)
    if pre_errors:
        if orig_indices:
            def _remap(m):
                n = int(m.group(1))
                return f"[{orig_indices[n-1]}]" if 1 <= n <= len(orig_indices) else m.group(0)
            result = re.sub(r'\[(\d+)\]', _remap, result)
        return "\n".join(pre_errors) + "\n" + result
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
