"""budget_status MCP tool — read-only cost snapshot."""
from ._common import bind
from .generated.catalog_tools import tools

_tracker = None


async def budget_status() -> str:
    """Returns Haiku cost: session/cap/day/skipped features. Text format."""
    if _tracker is None:
        return "budget tracking disabled (set UNITY_MCP_BUDGET=1)"
    return _tracker.status()


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, budget_status)
