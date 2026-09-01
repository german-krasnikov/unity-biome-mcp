from mcp.types import ToolAnnotations

RO = ToolAnnotations(readOnlyHint=True)
RW = ToolAnnotations(readOnlyHint=False)
RW_IDEM = ToolAnnotations(readOnlyHint=False, idempotentHint=True)
DEL = ToolAnnotations(readOnlyHint=False, destructiveHint=True)

# Internal wire commands with no MCP tool annotation (mcp.list_tools() never
# sees them -- they aren't registered via mcp.tool()) but proven safe to
# resend after a SENT/uncertain-delivery boundary:
#   get_status  -- CommandRouter.Registration.cs:76-90, pure read (scene/
#                  dirty/playing/compiling snapshot), zero mutation.
#   sync_status -- CommandRouter.Registration.cs:187 -> SyncHelper.GetSyncStatus()
#                  (SyncHelper.cs:155). Writes SessionState only as a
#                  self-converging self-heal (state -> "ready"); never bumps
#                  epoch, never triggers a compile, never touches the project.
#                  Same idempotent bar as the RW_IDEM tool class above -- a
#                  resend re-reads/re-converges the same status, never
#                  compounds. (The C# DedupRegistry op_id TTL cache also
#                  suppresses actual re-execution on retry regardless.)
_INTERNAL_RETRY_SAFE_CMDS = frozenset({"get_status", "sync_status"})


async def retry_safe_cmds(mcp) -> frozenset[str]:
    """Tool names eligible for resend after a frame may have been delivered.

    Unknown/unannotated tools (including internal/plugin tools with no
    annotations) are NOT included — fail closed after SENT. The internal
    wire commands in _INTERNAL_RETRY_SAFE_CMDS are the sole exception: they
    have no MCP tool annotation to inspect but are read-only/idempotent by
    inspection of their C# handler.
    """
    mcp_safe = frozenset(
        t.name for t in await mcp.list_tools()
        if t.annotations is not None
        and (t.annotations.readOnlyHint or t.annotations.idempotentHint)
    )
    return mcp_safe | _INTERNAL_RETRY_SAFE_CMDS
