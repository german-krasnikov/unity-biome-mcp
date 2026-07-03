"""Watch System — path-based field polling in Play Mode."""
from mcp.server.fastmcp.exceptions import ToolError
from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None

_DEFAULT_INTERVAL_MS = 500


async def watch(action: str, watch_id: str = "", path: str = "", component: str = "",
                field: str = "", condition: str = "", trigger_action: str = "log",
                interval_ms: int = _DEFAULT_INTERVAL_MS) -> str:
    """Manage watches. Play Mode only. action: add|remove|clear|reset.
    add: needs path/component/field. condition: '< 10','> 0','== null'.
    trigger_action: 'log' or 'pause'. remove/reset: needs watch_id."""
    if action == "add":
        return await _send("watch_add", _args(
            path=path, component=component, field=field,
            condition=condition or None,
            action=None if trigger_action == "log" else trigger_action,
            interval_ms=str(interval_ms) if interval_ms != _DEFAULT_INTERVAL_MS else None,
        ))
    if action == "remove":
        return await _send("watch_remove", _args(id=watch_id))
    if action == "clear":
        return await _send("watch_clear", {})
    if action == "reset":
        return await _send("watch_reset", _args(id=watch_id))
    raise ToolError(f"Unknown watch action '{action}'. Use: add|remove|clear|reset")


async def get_watches() -> str:
    """Get all active watches and recent log entries."""
    return await _send("get_watches", {})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(watch)
    mcp.tool(annotations=_RO)(get_watches)
