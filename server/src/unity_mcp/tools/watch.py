"""Watch System — path-based field polling in Play Mode."""
from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None

_DEFAULT_INTERVAL_MS = 500


async def watch_add(path: str, component: str, field: str,
                    condition: str = "", action: str = "log",
                    interval_ms: int = _DEFAULT_INTERVAL_MS) -> str:
    """Add a watch on a component field. Play Mode only.
    condition: optional comparison like '< 10', '> 0', '== null'.
    action: 'log' (default) or 'pause' (pauses the editor when triggered).
    Returns the watch ID (e.g. 'w1')."""
    return await _send("watch_add", _args(
        path=path, component=component, field=field,
        condition=condition or None,
        action=None if action == "log" else action,
        interval_ms=str(interval_ms) if interval_ms != _DEFAULT_INTERVAL_MS else None,
    ))


async def get_watches() -> str:
    """Get all active watches and recent log entries."""
    return await _send("get_watches", {})


async def watch_remove(watch_id: str) -> str:
    """Remove a watch by ID."""
    return await _send("watch_remove", _args(id=watch_id))


async def watch_clear() -> str:
    """Remove all watches."""
    return await _send("watch_clear", {})


async def watch_reset(watch_id: str) -> str:
    """Re-arm a triggered watch so it can trigger again."""
    return await _send("watch_reset", _args(id=watch_id))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(watch_add)
    mcp.tool(annotations=_RO)(get_watches)
    mcp.tool(annotations=_RW)(watch_remove)
    mcp.tool(annotations=_RW)(watch_clear)
    mcp.tool(annotations=_RW)(watch_reset)
