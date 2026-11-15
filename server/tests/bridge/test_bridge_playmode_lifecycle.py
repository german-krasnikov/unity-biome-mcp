"""PlayMode lifecycle — going_away → reload → fast reconnect.

Simulates the full sequence without real Unity:
- T-PM1: going_away marks DOMAIN_RELOADING state
- T-PM2: send() fast-fails during reload (DomainReloadError)
- T-PM3: heartbeat skips reconnect when reload + busy (no thrash during compile)
- T-PM4: full sequence: going_away → reconnect → CONNECTED
"""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from unity_mcp.bridge import UnityBridge, BridgeState

from unity_mcp.bridge_socket import DomainReloadError
from helpers import make_writer, make_idle_probe, reconnect_preamble

import unity_mcp.bridge as bridge_mod


def _connected_bridge() -> UnityBridge:
    """Bridge with live writer — simulates pre-PlayMode state."""
    probe = make_idle_probe()
    bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
    bridge._writer = make_writer()
    bridge._reader = MagicMock()
    return bridge


# T-PM1: going_away sets DOMAIN_RELOADING state via should_retry()
def test_going_away_sets_domain_reloading_state():
    """should_retry(DomainReloadError) from CONNECTED → bridge._state == DOMAIN_RELOADING.

    Corresponds to bridge.py: DomainReloadError caught in should_retry()
    sets _state = BridgeState.DOMAIN_RELOADING and marks the reload tracker.
    Reverting that production code change makes this test fail (double-red rule).
    """
    bridge = _connected_bridge()
    deadline = time.monotonic() + 60.0

    bridge.should_retry(DomainReloadError("going_away"), 0, deadline)

    assert bridge._state == BridgeState.DOMAIN_RELOADING
    assert bridge._reload.is_active() is True


# T-PM2: send() raises DomainReloadError immediately during reload
async def test_send_fails_fast_during_domain_reload():
    """During PlayMode reload, send() must raise DomainReloadError without retry.

    Guard in bridge.py: if _reload.is_active() → raise DomainReloadError.
    """
    bridge = _connected_bridge()
    bridge._reload.mark()

    with pytest.raises(DomainReloadError, match="Domain reload in progress"):
        await bridge.send("get_hierarchy", {})


# T-PM4: busy + reload → heartbeat does NOT attempt reconnect (avoids thrash)
async def test_heartbeat_skips_reconnect_during_compile_after_playmode():
    """After PlayMode exit, Unity is compiling → probe busy → no reconnect attempt.

    Prevents hammering a Unity that's mid-compilation. Once compile finishes,
    probe.has_strong_busy_signal() returns False → heartbeat proceeds to reconnect.
    """
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = True   # compiling
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    # Bridge starts disconnected (no writer)
    bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
    bridge._reload.mark()  # going_away already received

    with patch.object(bridge, "_reconnect", new=AsyncMock()) as mock_reconnect, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    mock_reconnect.assert_not_called()


# T-PM5: full sequence — going_away → disconnect → reconnect → CONNECTED
async def test_full_playmode_exit_reconnect_sequence():
    """Full PlayMode exit sequence:
      1. going_away received → _reload.mark(), state=DOMAIN_RELOADING
      2. probe not busy → heartbeat calls _reconnect()
      3. _reconnect() succeeds → state=CONNECTED, _reload cleared

    This tests the integration of the reload state and reconnect path.
    """
    probe = make_idle_probe()
    bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
    bridge._reload.mark()
    bridge._reload_gate.clear()

    preamble = reconnect_preamble()
    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(side_effect=preamble)
    mock_writer = make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      new=AsyncMock(return_value=(mock_reader, mock_writer))), \
         patch("unity_mcp.bridge._apply_socket_options"), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(bridge, "start_heartbeat"):
        await bridge._reconnect()

    assert bridge._state == BridgeState.CONNECTED
    assert bridge._reload.is_active() is False
    assert bridge._reload_gate.is_set() is True
