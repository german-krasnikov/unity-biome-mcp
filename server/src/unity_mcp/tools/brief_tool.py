"""T21: brief_build MCP tool — project context snapshot."""

from ._annotations import RO as _RO
from ._common import bind

_send = None
_args = None


async def brief_build(
    kinds: str = "console,compile_errors,hierarchy",
    budget: int = 2000,
) -> str:
    """Use to get a snapshot of project state before starting work.
    Returns compact text block with requested context within token budget.
    kinds: comma-separated subset of: console, compile_errors, hierarchy, selection, profiler
    budget: max token estimate (default 2000; 1 token ≈ 4 chars)"""
    if _send is None:
        return "[Project Brief]  hash=000000000000  tokens=0/0\n\n[Unity: disconnected]"

    from ..brief_builder import make_default_builder

    kind_list: list[str] | None = [k.strip() for k in kinds.split(",") if k.strip()] or None
    builder = make_default_builder(budget, _send)
    brief = await builder.build(kinds=kind_list)
    return brief.to_text()


def register(mcp, send, args) -> None:
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(brief_build)
