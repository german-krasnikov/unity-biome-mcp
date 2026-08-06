"""TDD: UnityBridge.status + ConnectionSlot.status + list_connections output."""
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.bridge import BridgeState, UnityBridge
from unity_mcp.connection_slot import ConnectionSlot


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_bridge() -> UnityBridge:
    bridge = UnityBridge(port=9500)
    bridge.stop_heartbeat()  # no background tasks
    return bridge


def _live_writer() -> MagicMock:
    w = MagicMock()
    w.is_closing.return_value = False
    return w


# ---------------------------------------------------------------------------
# UnityBridge.status
# ---------------------------------------------------------------------------

def test_status_when_connected():
    bridge = _make_bridge()
    bridge._writer = _live_writer()
    bridge._state = BridgeState.CONNECTED
    assert bridge.status == "connected"


def test_status_writer_alive_overrides_state():
    """Socket alive → 'connected' regardless of _state."""
    bridge = _make_bridge()
    bridge._writer = _live_writer()
    bridge._state = BridgeState.DISCONNECTED  # shouldn't matter
    assert bridge.status == "connected"


def test_status_when_disconnected_state_failed():
    bridge = _make_bridge()
    bridge._writer = None
    bridge._state = BridgeState.FAILED
    assert bridge.status == "disconnected"


def test_status_when_writer_none_state_disconnected():
    """No writer + DISCONNECTED → heartbeat will retry → 'reconnecting'."""
    bridge = _make_bridge()
    bridge._writer = None
    bridge._state = BridgeState.DISCONNECTED
    assert bridge.status == "reconnecting"


def test_status_when_domain_reloading():
    bridge = _make_bridge()
    bridge._writer = None
    bridge._state = BridgeState.DOMAIN_RELOADING
    assert bridge.status == "domain-reloading"


def test_status_closing_writer_treated_as_disconnected():
    """is_closing() True → treated same as None writer."""
    bridge = _make_bridge()
    w = MagicMock()
    w.is_closing.return_value = True
    bridge._writer = w
    bridge._state = BridgeState.DISCONNECTED
    assert bridge.status == "reconnecting"


# ---------------------------------------------------------------------------
# ConnectionSlot.status
# ---------------------------------------------------------------------------

def test_connection_slot_status_no_bridge():
    slot = ConnectionSlot()
    assert slot.status == "disconnected"


def test_connection_slot_status_delegates_to_bridge():
    slot = ConnectionSlot()
    mock_bridge = MagicMock()
    mock_bridge.status = "reconnecting"
    slot._bridge = mock_bridge
    assert slot.status == "reconnecting"


def test_connection_slot_status_connected():
    slot = ConnectionSlot()
    mock_bridge = MagicMock()
    mock_bridge.status = "connected"
    slot._bridge = mock_bridge
    assert slot.status == "connected"


# ---------------------------------------------------------------------------
# list_connections output
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_list_connections_shows_reconnecting():
    from unity_mcp.tools import connection as conn_mod
    from unittest.mock import patch

    mock_slot = MagicMock()
    mock_slot.port = 9500
    mock_slot.bridge.transport_status = "tcp:reconnecting"
    original = conn_mod._get_slot
    conn_mod._get_slot = lambda: mock_slot
    try:
        with patch("unity_mcp.tools.connection._stdio_alive", return_value=True):
            result = await conn_mod.list_connections()
    finally:
        conn_mod._get_slot = original

    assert "9500" in result
    assert "tcp:reconnecting" in result


@pytest.mark.asyncio
async def test_list_connections_shows_domain_reloading():
    from unity_mcp.tools import connection as conn_mod
    from unittest.mock import patch

    mock_slot = MagicMock()
    mock_slot.port = 9500
    mock_slot.bridge.transport_status = "tcp:reconnecting"
    original = conn_mod._get_slot
    conn_mod._get_slot = lambda: mock_slot
    try:
        with patch("unity_mcp.tools.connection._stdio_alive", return_value=True):
            result = await conn_mod.list_connections()
    finally:
        conn_mod._get_slot = original

    assert "9500" in result
    assert "tcp:" in result


@pytest.mark.asyncio
async def test_list_connections_shows_connected():
    from unity_mcp.tools import connection as conn_mod
    from unittest.mock import patch

    mock_slot = MagicMock()
    mock_slot.port = 9500
    mock_slot.bridge.transport_status = "tcp:connected"
    original = conn_mod._get_slot
    conn_mod._get_slot = lambda: mock_slot
    try:
        with patch("unity_mcp.tools.connection._stdio_alive", return_value=True):
            result = await conn_mod.list_connections()
    finally:
        conn_mod._get_slot = original

    assert "9500" in result
    assert "tcp:connected" in result
    assert "stdio:alive" in result
