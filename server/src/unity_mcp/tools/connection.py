from mcp.server.fastmcp import Context
from mcp.server.fastmcp.exceptions import ToolError

from ._annotations import RO as _RO
from ._annotations import RW_IDEM as _RW_IDEM

_get_slot = None
_refresh_tools_cache = None
_push_catalog = None


def _stdio_alive() -> bool:
    """Delegate to server._stdio_alive (lazy import avoids circular dep)."""
    from unity_mcp.server import _stdio_alive as _probe
    return _probe()


async def list_connections() -> str:
    """List Unity connection status."""
    s = _get_slot() if _get_slot else None
    if s is None:
        return "No slot initialized"
    bridge = s.bridge
    tcp = bridge.transport_status if bridge is not None else "tcp:none"
    stdio = "stdio:alive" if _stdio_alive() else "stdio:dead"
    return f"port {s.port} | {tcp} | {stdio}"


async def reconnect_unity(port: int = 0, ctx: Context = None) -> str:
    """Reconnect to Unity. Port 0 or omitted = auto-discover from port files."""
    s = _get_slot() if _get_slot else None
    if s is None:
        raise ToolError("Server not initialized")
    if port <= 0:
        from unity_mcp.server_filtering import read_unity_port
        port = read_unity_port()
    result = await s.connect(port)
    if s.connected:
        from unity_mcp.tools.gating import reset as _reset_gating
        _reset_gating()  # clear session-enabled tools only after a successful connect —
        # a failed connect must not wipe gating for the still-active prior project.
        if _refresh_tools_cache is not None:
            await _refresh_tools_cache(s.bridge)
        if _push_catalog is not None:
            await _push_catalog(s.bridge)
        if ctx is not None:
            # Manual reconnect must invalidate the client's in-session tool list —
            # no debounce here (unlike automatic reconnects), the user explicitly asked.
            await ctx.session.send_tool_list_changed()
    return result


def register(mcp, send, args, *, get_slot, refresh_tools_cache=None, push_catalog=None, **_kw):
    global _get_slot, _refresh_tools_cache, _push_catalog
    _get_slot = get_slot
    _refresh_tools_cache = refresh_tools_cache
    _push_catalog = push_catalog
    mcp.tool(annotations=_RO)(list_connections)
    mcp.tool(annotations=_RW_IDEM)(reconnect_unity)
