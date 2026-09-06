"""Watch System — path-based field polling in Play Mode."""
from mcp.server.fastmcp.exceptions import ToolError

from ._annotations import RO as _RO
from ._annotations import RW as _RW

_DEFAULT_INTERVAL_MS = 500

# PR-04 physical registration pilot: watch/get_watches ToolSpec kwargs are owned
# here, not hand-typed in tool_specs.py. Plain dict kwargs (not ToolSpec instances)
# so this module never needs to import tool_specs -- see Plans/PR-04.md "Ordering
# trace" for why the import direction must stay one-way (tool_specs -> watch).
SPEC_KWARGS: dict[str, dict] = {
    'watch': {'category': 'RUNTIME', 'direct_only': True},
    'get_watches': {'category': 'RUNTIME', 'mutability': 'read'},
}


class WatchModule:
    """Owns the Watch command group end to end: MCP tool schemas + their C#
    wire-command mapping. Instance-scoped (not a module-global singleton) so
    two independently-configured hosts never cross-contaminate each other's
    `_send`/`_args`."""

    def __init__(self, send, args):
        self._send = send
        self._args = args

    async def watch(self, action: str, watch_id: str = "", path: str = "", component: str = "",
                     field: str = "", condition: str = "", trigger_action: str = "log",
                     interval_ms: int = _DEFAULT_INTERVAL_MS) -> str:
        """[Play Mode] Manage watches. Registers or removes watches. No confirmation required. action: add|remove|clear|reset.
        add: needs path/component/field. condition: '< 10','> 0','== null'.
        trigger_action: 'log' or 'pause'. remove/reset: needs watch_id."""
        if action == "add":
            return await self._send("watch_add", self._args(
                path=path, component=component, field=field,
                condition=condition or None,
                action=None if trigger_action == "log" else trigger_action,
                interval_ms=str(interval_ms) if interval_ms != _DEFAULT_INTERVAL_MS else None,
            ))
        if action == "remove":
            return await self._send("watch_remove", self._args(id=watch_id))
        if action == "clear":
            return await self._send("watch_clear", {})
        if action == "reset":
            return await self._send("watch_reset", self._args(id=watch_id))
        raise ToolError(f"Unknown watch action '{action}'. Use: add|remove|clear|reset")

    async def get_watches(self) -> str:
        """Get all active watches and recent log entries."""
        return await self._send("get_watches", {})

    def register(self, mcp) -> None:
        mcp.tool(annotations=_RW)(self.watch)
        mcp.tool(annotations=_RO)(self.get_watches)


# Process-wide singleton pointer -- same shape as every other tools/*.py module
# today (register_all() is called exactly once, at server startup). Bullet 6
# ("bound handlers work in two instances") is proven at the WatchModule class
# level directly (test_watch.py), not by making this compat shim multi-tenant.
_default: WatchModule | None = None


async def watch(action: str, watch_id: str = "", path: str = "",
                component: str = "", field: str = "", condition: str = "",
                trigger_action: str = "log",
                interval_ms: int = _DEFAULT_INTERVAL_MS) -> str:
    """[Play Mode] Manage watches. action: add|remove|clear|reset."""
    return await _default.watch(action, watch_id, path, component, field,
                                condition, trigger_action, interval_ms)


async def get_watches() -> str:
    """Get all active watches and recent log entries."""
    return await _default.get_watches()


def register(mcp, send, args) -> WatchModule:
    global _default
    _default = WatchModule(send, args)
    _default.register(mcp)
    return _default
