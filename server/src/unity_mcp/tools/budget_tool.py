"""budget_status MCP tool — read-only cost snapshot."""
from ._annotations import RO as _RO
from ._common import bind

_tracker = None


async def budget_status() -> str:
    """Returns Haiku cost: session/cap/day/skipped features. Text format."""
    if _tracker is None:
        return "budget tracking disabled (set UNITY_MCP_BUDGET=1)"
    return _tracker.status()


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(budget_status)
