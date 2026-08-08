import importlib
import os
import subprocess
import sys
from pathlib import Path

import pytest
from unittest.mock import AsyncMock, Mock, patch


def test_server_filtering_importable_in_isolation():
    """server_filtering must import without circular ImportError (PY2.arch.1)."""
    venv_python = sys.executable
    src = str(Path(__file__).parent.parent / "src")
    env = {**os.environ, "PYTHONPATH": src}
    result = subprocess.run(
        [venv_python, "-c", "import unity_mcp.server_filtering"],
        capture_output=True, text=True, encoding="utf-8", env=env, timeout=60,
    )
    assert result.returncode == 0, f"Circular import: {result.stderr}"


async def test_list_connections(mock_bridge):
    from unity_mcp.server import list_connections
    result = await list_connections()
    assert "9500" in result
    assert "connected" in result


async def test_reconnect_unity(mock_bridge):
    from unity_mcp.server import reconnect_unity
    result = await reconnect_unity(9500)
    assert "Connected" in result


async def test_reconnect_unity_auto_discovers(mock_bridge):
    """reconnect_unity(0) auto-discovers port via read_unity_port (lazy import)."""
    from unity_mcp.tools.connection import reconnect_unity
    with patch("unity_mcp.server_filtering.read_unity_port", return_value=9501) as mock_disc:
        result = await reconnect_unity(0)
    mock_disc.assert_called_once()
    assert result is not None


async def test_reconnect_unity_clears_session_enabled(mock_bridge, monkeypatch):
    """Issue 24: reconnect_unity must reset gating._session_enabled.

    Categories enabled via discover_tools(category=..., enable=True) for a PRIOR
    Unity project must not stay force-visible after switching to a different one.
    """
    from unity_mcp.tools import gating
    from unity_mcp.server import reconnect_unity
    monkeypatch.setattr(gating, "_session_enabled", {"asset"})
    await reconnect_unity(9500)
    assert gating._session_enabled == set()


async def test_reconnect_unity_does_not_reset_gating_when_connect_fails(monkeypatch):
    """M19: gating must only be reset AFTER a successful s.connect(port).

    Mocked at the UnityBridge.connect boundary — not ConnectionSlot.connect —
    because the slot's connect() never raises on connection-refused/timeout:
    it catches OSError/asyncio.TimeoutError internally and returns a soft-fail
    string. A slot.connect() mocked to raise directly never exercises that
    real path and would stay green even if the reset-gating guard were
    removed."""
    import unity_mcp.server as srv
    from unity_mcp.connection_slot import ConnectionSlot
    from unity_mcp.tools import gating
    from unity_mcp.server import reconnect_unity
    from helpers import make_mock_bridge

    monkeypatch.setattr(gating, "_session_enabled", {"asset"})
    b = make_mock_bridge(connected=False)
    b.connect = AsyncMock(side_effect=OSError("refused"))
    real_slot = ConnectionSlot()
    monkeypatch.setattr(srv, "slot", real_slot)

    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        result = await reconnect_unity(9500)

    assert "not yet available" in result
    assert real_slot.connected is False
    assert gating._session_enabled == {"asset"}


async def test_reconnect_unity_resets_gating_only_after_connect_call(mock_bridge, monkeypatch):
    """M19: verify call order — s.connect(port) must run BEFORE gating.reset()."""
    import unity_mcp.server as srv
    from unity_mcp.server import reconnect_unity

    call_order = []
    original_connect = srv.slot.connect

    async def tracked_connect(*a, **kw):
        call_order.append("connect")
        return await original_connect(*a, **kw)

    srv.slot.connect = AsyncMock(side_effect=tracked_connect)

    from unity_mcp.tools import gating
    real_reset = gating.reset

    def tracked_reset():
        call_order.append("reset")
        return real_reset()

    monkeypatch.setattr(gating, "reset", tracked_reset)

    await reconnect_unity(9500)

    assert call_order == ["connect", "reset"]


async def test_reconnect_unity_preserves_disabled_tools_cache_refresh(mock_bridge):
    """Regression guard: gating.reset() must not disturb the existing s.connect(port)
    call contract that the _refresh_tools_cache reconnect callback relies on."""
    import unity_mcp.server as srv
    from unity_mcp.server import reconnect_unity
    result = await reconnect_unity(9500)
    srv.slot.connect.assert_awaited_once_with(9500)
    assert "Connected" in result


# --- Fix 4: reconnect_unity refreshes disabled-tools cache + notifies client ---

async def test_reconnect_unity_refreshes_disabled_tools_cache(mock_bridge):
    """After reconnect_unity, disabled tools cache must be refreshed from Unity —
    fixes the reported bug where an already-connected session never re-polls the
    checkbox state on a manual reconnect."""
    import unity_mcp.server as srv
    from unity_mcp.server import reconnect_unity

    async def _send_side_effect(cmd, args, timeout=0):
        if cmd == "get_disabled_tools":
            return {"ok": True, "data": "screenshot,run_tests"}
        return {"ok": True, "data": "ok"}
    mock_bridge.send = AsyncMock(side_effect=_send_side_effect)

    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        await reconnect_unity(9500)
        assert srv._disabled_tools_cache == {"screenshot", "run_tests"}
    finally:
        srv._disabled_tools_cache = orig


async def test_reconnect_unity_sends_tool_list_changed_notification(mock_bridge):
    """reconnect_unity must notify the MCP client (via ctx.session) to re-fetch
    ListTools so a stale in-session tool list is invalidated."""
    from types import SimpleNamespace
    from unity_mcp.server import reconnect_unity

    fake_ctx = SimpleNamespace(session=SimpleNamespace(send_tool_list_changed=AsyncMock()))
    await reconnect_unity(9500, ctx=fake_ctx)
    fake_ctx.session.send_tool_list_changed.assert_awaited_once()


async def test_reconnect_unity_no_ctx_does_not_raise(mock_bridge):
    """Direct-call tests (no FastMCP-injected ctx) must keep working — ctx defaults to None."""
    from unity_mcp.server import reconnect_unity
    result = await reconnect_unity(9500)
    assert "Connected" in result


async def test_reconnect_unity_skips_refresh_and_notify_when_connect_fails(monkeypatch):
    """A failed connect must not disturb the disabled-tools cache or notify the client —
    same invariant already protected for gating.reset()."""
    import unity_mcp.server as srv
    from unity_mcp.connection_slot import ConnectionSlot
    from unity_mcp.server import reconnect_unity
    from types import SimpleNamespace
    from helpers import make_mock_bridge

    b = make_mock_bridge(connected=False)
    b.connect = AsyncMock(side_effect=OSError("refused"))
    real_slot = ConnectionSlot()
    monkeypatch.setattr(srv, "slot", real_slot)

    orig = srv._disabled_tools_cache
    fake_ctx = SimpleNamespace(session=SimpleNamespace(send_tool_list_changed=AsyncMock()))
    try:
        srv._disabled_tools_cache = {"sentinel"}
        with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
            await reconnect_unity(9500, ctx=fake_ctx)
        assert srv._disabled_tools_cache == {"sentinel"}
        fake_ctx.session.send_tool_list_changed.assert_not_awaited()
    finally:
        srv._disabled_tools_cache = orig


async def test_send_routes_to_active(mock_bridge):
    """Verify _send() uses slot.bridge (which is mock_bridge)."""
    from unity_mcp.server import get_hierarchy
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Root\n  Child"})
    result = await get_hierarchy()
    mock_bridge.send.assert_awaited_once()
    assert "Root" in result


async def test_send_no_slot_raises():
    """Verify _send() raises ToolError when slot is None."""
    from mcp.server.fastmcp.exceptions import ToolError
    with patch("unity_mcp.server.slot", None):
        from unity_mcp.server import get_hierarchy
        with pytest.raises(ToolError, match="Server not initialized"):
            await get_hierarchy()


async def test_send_no_bridge_raises():
    """Verify _send() raises ToolError when slot.bridge is None."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_slot = Mock()
    mock_slot.bridge = None
    with patch("unity_mcp.server.slot", mock_slot):
        from unity_mcp.server import get_hierarchy
        with pytest.raises(ToolError, match="No Unity connection configured"):
            await get_hierarchy()
