from mcp.server.fastmcp import Context  # noqa: TC002 — fastmcp eval_str=True requires runtime
from mcp.server.fastmcp.exceptions import ToolError

from .generated.catalog_tools import list_connections, tools  # noqa: F401

_get_slot = None
_stdio_alive = None            # Callable[[], bool] | None — injected by register()
_refresh_tools_cache = None
_push_catalog = None


async def _list_connections_body() -> str:
    """List Unity connection status."""
    s = _get_slot() if _get_slot else None
    if s is None:
        return "No slot initialized"
    bridge = s.bridge
    tcp = bridge.transport_status if bridge is not None else "tcp:none"
    alive = _stdio_alive() if _stdio_alive else True   # fail-open when not wired
    stdio = "stdio:alive" if alive else "stdio:dead"
    return f"port {s.port} | {tcp} | {stdio}"


async def reconnect_unity(port: int = 0, ctx: Context = None) -> str:
    # ctx's annotation must stay a real Context (not catalog Any) -- see
    # codegen.py's auto_fix/smart_build comment for why (stays hand-written, SV8).
    """Reconnect to Unity. Port 0 or omitted = auto-discover from port files."""
    s = _get_slot() if _get_slot else None
    if s is None:
        raise ToolError("Server not initialized")
    if port <= 0:
        from unity_mcp.server_filtering import read_unity_port
        port = read_unity_port()
    old_port = s.port
    result = await s.connect(port)
    if s.connected:
        from unity_mcp.tools.gating import reset as _reset_gating
        if s.port != old_port:  # port changed = project switch → wipe session
            _reset_gating()  # clear session-enabled tools only on project switch —
        # a same-port reconnect (domain reload) preserves session state.
        if _refresh_tools_cache is not None:
            await _refresh_tools_cache(s.bridge)
        if _push_catalog is not None:
            await _push_catalog(s.bridge)
        if ctx is not None:
            # Manual reconnect must invalidate the client's in-session tool list —
            # no debounce here (unlike automatic reconnects), the user explicitly asked.
            await ctx.session.send_tool_list_changed()
    return result


def register(mcp, send, args, *, get_slot, stdio_alive=None,
             refresh_tools_cache=None, push_catalog=None, **_kw):
    global _get_slot, _stdio_alive, _refresh_tools_cache, _push_catalog
    _get_slot = get_slot
    _stdio_alive = stdio_alive
    _refresh_tools_cache = refresh_tools_cache
    _push_catalog = push_catalog
    tools.register(mcp, reconnect_unity)
