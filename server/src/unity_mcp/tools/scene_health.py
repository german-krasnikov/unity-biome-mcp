"""Scene hierarchy/health audit — F4."""
from ._annotations import RO as _RO
from ._common import bind

_send = None
_args = None


async def scene_health(focus: str = "all") -> str:
    """Scene hierarchy/health audit.
    focus: all | hierarchy | naming | duplicates | origins | missing | empty | disabled
    Returns severity-tagged findings: CRITICAL/WARNING/INFO/OK per check."""
    return await _send("scene_health", _args(focus=focus))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(scene_health)
