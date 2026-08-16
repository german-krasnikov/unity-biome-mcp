"""Port change during PlayMode reload — compound reconnect scenario.

Tests the Issue 5 chain: going_away + port change + pin clear + rediscovery.
- T-PC2: full chain — reload → refuse old port → discoverer finds new port
- T-PC3: reload state cleared after reconnect on new port
- T-PC4: port discoverer called exactly once per reconnect attempt
"""
import asyncio
import pytest
from unittest.mock import AsyncMock, Mock, patch

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge, BridgeState
from helpers import make_writer, make_idle_probe, reconnect_preamble


@pytest.fixture(autouse=True)
def _fast_timeouts():
    orig = bridge_mod.CONNECT_TIMEOUT
    bridge_mod.CONNECT_TIMEOUT = 0.05
    yield
    bridge_mod.CONNECT_TIMEOUT = orig


def _ok_reader():
    """Reader providing correct preamble for _open_reconnect_candidate."""
    preamble = reconnect_preamble()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=preamble)
    return reader


# T-PC2: reload → refuse old port → discoverer → new port → CONNECTED
async def test_port_change_after_domain_reload():
    """Full compound: domain reload + port change + rediscovery.

    Simulates: PlayMode exit → domain reload → Unity restarts on 9501 (TIME_WAIT
    forced fallback) → Python ConnectionRefused on 9500 → pin cleared → discoverer
    returns 9501 → connect succeeds.
    """
    async def mock_open(host, port):
        if port == 9500:
            raise ConnectionRefusedError("old port in TIME_WAIT")
        return _ok_reader(), make_writer()

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(UnityBridge, "start_heartbeat"):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._reload.mark()
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345

        # Attempt 1: pinned port → refused → pin cleared
        with pytest.raises(ConnectionRefusedError):
            await bridge._reconnect(fire_callbacks=False)
        assert bridge._pinned_port is None

        # Attempt 2: no pin → discoverer → 9501 → success
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._port == 9501
    assert bridge._pinned_port == 9501
    assert bridge._state == BridgeState.CONNECTED


# T-PC3: after successful port change, reload state cleared
async def test_reload_state_cleared_after_port_change_reconnect():
    """After reconnect on new port, reload state must be cleared.

    Prevents subsequent send() calls from seeing stale DomainReloadError.
    """
    async def mock_open(host, port):
        return _ok_reader(), make_writer()

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(UnityBridge, "start_heartbeat"):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._reload.mark()
        bridge._reload_gate.clear()
        await bridge._reconnect()

    assert bridge._reload.is_active() is False
    assert bridge._reload_gate.is_set() is True


# T-PC4: discoverer called exactly once per reconnect attempt
async def test_discoverer_called_once_per_reconnect_attempt():
    """Port discoverer must be called once per reconnect attempt, not cached.

    Each reconnect attempt reads fresh port from discoverer (port file may update
    between attempts as Unity writes its new port).
    """
    async def mock_open(host, port):
        return _ok_reader(), make_writer()

    discoverer = Mock(return_value=9502)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(UnityBridge, "start_heartbeat"):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        await bridge._reconnect()
        # After first reconnect, _pinned_pid=None (read_pid_from_port_file→None)
        # so pinned_is_live=False → discoverer called again
        await bridge._reconnect()

    assert discoverer.call_count == 2
