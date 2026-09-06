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


def _make_mcp():
    """Minimal mcp stub: mcp.tool(annotations=X)(fn) must not raise."""
    mcp = MagicMock()
    mcp.tool.return_value = lambda fn: fn
    return mcp


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
    import unity_mcp.server as srv
    monkeypatch.setattr(srv, "_stdio_last_confirmed", 0.0)
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


# ---------------------------------------------------------------------------
# Test 5 — grace window for transient BrokenPipeError
# ---------------------------------------------------------------------------

def test_stdio_grace_window_returns_true_on_transient_failure(monkeypatch):
    """A BrokenPipeError within the grace window must return True (transient)."""
    import time
    import unity_mcp.server as srv
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "stdio")
    monkeypatch.setattr(srv, "_stdio_last_confirmed", time.monotonic())
    buf = Mock()
    buf.flush.side_effect = BrokenPipeError
    with patch.object(sys, "stdout", SimpleNamespace(buffer=buf)):
        assert srv._stdio_alive() is True


def test_stdio_grace_expired_returns_false(monkeypatch):
    """A BrokenPipeError after grace window expires must return False."""
    import unity_mcp.server as srv
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "stdio")
    monkeypatch.setattr(srv, "_stdio_last_confirmed", 0.0)
    buf = Mock()
    buf.flush.side_effect = BrokenPipeError
    with patch.object(sys, "stdout", SimpleNamespace(buffer=buf)):
        assert srv._stdio_alive() is False


# ---------------------------------------------------------------------------
# PR-02 Track A — connection.py stops lazy-importing the composition root;
# _stdio_alive becomes a callback slot injected via register().
# ---------------------------------------------------------------------------

def _protect_connection_globals(monkeypatch):
    """register() mutates connection.py's module globals directly (not via a
    fixture). Snapshot each one through monkeypatch BEFORE calling register()
    so teardown restores the pre-test value — prevents leaking a test-only
    _get_slot/_stdio_alive into later tests (e.g. test_connection_tools.py's
    reconnect_unity tests)."""
    import unity_mcp.tools.connection as conn_mod
    for name in ("_get_slot", "_stdio_alive", "_refresh_tools_cache", "_push_catalog"):
        monkeypatch.setattr(conn_mod, name, getattr(conn_mod, name))


async def test_list_connections_uses_injected_stdio_alive_callback(monkeypatch):
    """register(..., stdio_alive=...) must wire the callback that list_connections
    actually calls — no reverse import into unity_mcp.server needed."""
    import unity_mcp.tools.connection as conn_mod

    _protect_connection_globals(monkeypatch)
    slot = _make_slot(_make_connected_bridge())
    conn_mod.register(_make_mcp(), AsyncMock(), MagicMock(),
                       get_slot=lambda: slot, stdio_alive=lambda: False)

    result = await conn_mod.list_connections()

    assert "stdio:dead" in result


async def test_list_connections_stdio_alive_defaults_true_without_callback(monkeypatch):
    """Without an injected stdio_alive callback, list_connections must fail open
    (report alive) WITHOUT consulting the real unity_mcp.server probe — proves
    the module never falls back to importing/calling server._stdio_alive."""
    import unity_mcp.server as srv
    import unity_mcp.tools.connection as conn_mod

    monkeypatch.setattr(srv, "_stdio_alive", lambda: False)
    _protect_connection_globals(monkeypatch)
    slot = _make_slot(_make_connected_bridge())
    conn_mod.register(_make_mcp(), AsyncMock(), MagicMock(), get_slot=lambda: slot)

    result = await conn_mod.list_connections()

    assert "stdio:alive" in result


def test_connection_module_source_has_no_server_import():
    """connection.py must never import unity_mcp.server (the composition root) —
    the bug this PR fixes was a lazy `from unity_mcp.server import _stdio_alive`.
    Word-boundary regex so unity_mcp.server_filtering (a different, allowed
    module) doesn't false-positive this check."""
    import inspect
    import re

    import unity_mcp.tools.connection as conn_mod
    src = inspect.getsource(conn_mod)

    assert re.search(r"\bunity_mcp\.server\b", src) is None
    assert re.search(r"\bfrom\s+unity_mcp\s+import\s+server\b", src) is None
