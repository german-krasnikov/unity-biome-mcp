"""Bulk command execution + reference inspection/validation."""
from mcp.server.fastmcp.exceptions import ToolError
from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None

# Tools that require their typed MCP wrapper (Python DSL expansion) — rejected inside batch.
_dsl_tools: set[str] = set()


async def batch(commands: str, on_error: str = "continue", timeout: float = 30.0,
                atomic: bool = False) -> str:
    """Execute multiple commands in one call. Use for 2+ operations — reads AND writes. commands: one command per line (cmd key=value). on_error: continue|stop. timeout: seconds (default 30). atomic: when True, first failing op reverts ALL prior ops in this batch (uses Unity Undo). Note: file-system side-effects from execute_code are NOT reverted. Commands validated before execution: !param = missing required parameter, ?param→suggestion = unknown parameter (with did-you-mean suggestion when close enough), plus an auto-generated usage line. PREFER this over individual tool calls."""
    for line in commands.splitlines():
        cmd = line.strip().split()[0] if line.strip() else ""
        if cmd in _dsl_tools:
            raise ToolError(f"{cmd} requires typed MCP tool (Python DSL expansion), not batch")
    timeout_ms = max(1000, int((timeout - 5) * 1000))
    args = {"commands": commands}
    if on_error != "continue":
        args["on_error"] = on_error
    if timeout_ms != 25000:
        args["timeout_ms"] = timeout_ms
    if atomic:
        args["atomic"] = "true"
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
