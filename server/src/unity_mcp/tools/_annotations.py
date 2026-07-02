from mcp.types import ToolAnnotations

RO = ToolAnnotations(readOnlyHint=True)
RW = ToolAnnotations(readOnlyHint=False)
RW_IDEM = ToolAnnotations(readOnlyHint=False, idempotentHint=True)
DEL = ToolAnnotations(readOnlyHint=False, destructiveHint=True)


async def retry_safe_cmds(mcp) -> frozenset[str]:
    """Tool names safe to blindly retry after a client-side timeout: read-only
    or explicitly idempotent. Unknown/unannotated tools (including plugin
    tools with no annotations) are NOT included — fail closed, do not retry."""
    return frozenset(
        t.name for t in await mcp.list_tools()
        if t.annotations is not None
        and (t.annotations.readOnlyHint or t.annotations.idempotentHint)
    )
