from mcp.types import ToolAnnotations

RO = ToolAnnotations(readOnlyHint=True)
RW = ToolAnnotations(readOnlyHint=False)
RW_IDEM = ToolAnnotations(readOnlyHint=False, idempotentHint=True)
DEL = ToolAnnotations(readOnlyHint=False, destructiveHint=True)


async def retry_safe_cmds(mcp) -> frozenset[str]:
    """Tool names eligible for resend after a frame may have been delivered.

    Unknown/unannotated tools (including internal/plugin tools with no
    annotations) are NOT included — fail closed after SENT.
    """
    return frozenset(
        t.name for t in await mcp.list_tools()
        if t.annotations is not None
        and (t.annotations.readOnlyHint or t.annotations.idempotentHint)
    )
