"""P-320: Transport-layer status tests.

Distinguishes dead stdio transport from offline Unity TCP endpoint so the LLM
can give the right recovery instruction.
"""
import sys
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_slot(bridge, port: int = 9500) -> Mock:
    s = Mock()
    s.port = port
    s.bridge = bridge
    s.status = bridge.status if bridge else "disconnected"
    return s


def _make_connected_bridge():
    """Bridge mock where TCP writer is alive."""
    b = MagicMock()
    b.transport_status = "tcp:connected"
    b.status = "connected"
    return b


# ---------------------------------------------------------------------------
# Test 1 — dead stdio raises TRANSPORT_DEAD
# ---------------------------------------------------------------------------

async def test_stdio_dead_raises_structured_error(monkeypatch):
    """When stdio is closed (_stdio_alive() returns False), _send_raw must raise
    ToolError with the TRANSPORT_DEAD tag so the LLM surfaces the right fix."""
    from mcp.server.fastmcp.exceptions import ToolError
    import unity_mcp.server as srv

    # Wire a live bridge so the failure comes from stdio, not TCP.
    bridge = _make_connected_bridge()
    bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    slot = _make_slot(bridge)
    monkeypatch.setattr(srv, "slot", slot)

    # Simulate broken stdio pipe.
    with patch("unity_mcp.server._stdio_alive", return_value=False):
        with pytest.raises(ToolError) as exc_info:
            await srv._send_raw("get_hierarchy", {})

    assert "TRANSPORT_DEAD" in str(exc_info.value)


# ---------------------------------------------------------------------------
# Test 2 — list_connections shows both transport layers
# ---------------------------------------------------------------------------

async def test_list_connections_shows_both_layers(monkeypatch):
    """When TCP is connected and stdio is alive, list_connections must report
    both layers explicitly so the LLM can distinguish them."""
    import unity_mcp.tools.connection as conn_mod

    bridge = _make_connected_bridge()
    slot = _make_slot(bridge)
    monkeypatch.setattr(conn_mod, "_get_slot", lambda: slot)

    with patch("unity_mcp.tools.connection._stdio_alive", return_value=True):
        result = await conn_mod.list_connections()

    assert "tcp:connected" in result
    assert "stdio:alive" in result


async def test_list_connections_dead_stdio_shown(monkeypatch):
    """When stdio is dead, list_connections must show stdio:dead so the user
    knows the transport layer (not Unity) is the problem."""
    import unity_mcp.tools.connection as conn_mod

    bridge = _make_connected_bridge()
    slot = _make_slot(bridge)
    monkeypatch.setattr(conn_mod, "_get_slot", lambda: slot)

    with patch("unity_mcp.tools.connection._stdio_alive", return_value=False):
        result = await conn_mod.list_connections()

    assert "stdio:dead" in result


# ---------------------------------------------------------------------------
# Test 3 — bridge.transport_status when grace expired
# ---------------------------------------------------------------------------

def test_transport_status_failed_when_grace_expired():
    """Bridge in FAILED state must surface tcp:failed (not tcp:reconnecting)."""
    from unity_mcp.bridge import BridgeState, UnityBridge

    b = UnityBridge.__new__(UnityBridge)
    b._writer = None
    b._state = BridgeState.FAILED

    assert b.transport_status == "tcp:failed"


def test_transport_status_connected_when_writer_alive():
    """Bridge with active writer must surface tcp:connected."""
    from unity_mcp.bridge import BridgeState, UnityBridge

    b = UnityBridge.__new__(UnityBridge)
    writer = Mock()
    writer.is_closing.return_value = False
    b._writer = writer
    b._state = BridgeState.CONNECTED

    assert b.transport_status == "tcp:connected"


def test_transport_status_reconnecting_when_disconnected():
    """Bridge in DISCONNECTED state (no writer) must surface tcp:reconnecting."""
    from unity_mcp.bridge import BridgeState, UnityBridge

    b = UnityBridge.__new__(UnityBridge)
    b._writer = None
    b._state = BridgeState.DISCONNECTED

    assert b.transport_status == "tcp:reconnecting"


# ---------------------------------------------------------------------------
# Test 4 — _stdio_alive returns True on non-stdio transport (guard)
# ---------------------------------------------------------------------------

def test_stdio_alive_returns_true_for_http_transport(monkeypatch):
    """On non-stdio transports the stdio pipe is irrelevant; _stdio_alive must
    return True so the check never blocks HTTP/SSE sessions."""
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "http")
    from unity_mcp.server import _stdio_alive
    assert _stdio_alive() is True


def test_stdio_alive_returns_false_on_broken_pipe(monkeypatch):
    """_stdio_alive must catch BrokenPipeError and return False."""
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "stdio")
    buf = Mock()
    buf.flush.side_effect = BrokenPipeError
    with patch.object(sys, "stdout", SimpleNamespace(buffer=buf)):
        from unity_mcp.server import _stdio_alive
        assert _stdio_alive() is False


def test_stdio_alive_returns_true_when_flush_succeeds(monkeypatch):
    """_stdio_alive must return True when flush does not raise."""
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "stdio")
    buf = Mock()
    buf.flush.return_value = None
    with patch.object(sys, "stdout", SimpleNamespace(buffer=buf)):
        from unity_mcp.server import _stdio_alive
        assert _stdio_alive() is True
