"""Bulk command execution + reference inspection/validation."""
from mcp.server.fastmcp.exceptions import ToolError
from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None

# Tools that require their typed MCP wrapper (Python DSL expansion) — rejected inside batch.
_dsl_tools: set[str] = set()


async def batch(commands: str, on_error: str = "continue", timeout: float = 75.0,
                atomic: bool = False, validate_aliases: bool = False) -> str:
    """Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop (default continue). timeout: seconds (default 75). atomic: True reverts ALL prior ops on first failure (Unity Undo); execute_code fs side-effects NOT reverted. PREFER over individual tool calls."""
    for line in commands.splitlines():
        cmd = line.strip().split()[0] if line.strip() else ""
        if cmd in _dsl_tools:
            raise ToolError(f"{cmd} requires typed MCP tool (Python DSL expansion), not batch")
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
    return await _send("batch", args, timeout=timeout)


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
