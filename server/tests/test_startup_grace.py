"""RC-11 startup-grace deadline — bounds heartbeat reconnect loop.

Tests: is_startup_in_progress, has_strong_busy_signal fold,
       heartbeat grace expiry, send() raises after grace, x-plat PID check.
"""
import asyncio
import time
from unittest.mock import MagicMock, patch, AsyncMock

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from unity_mcp.compile_state import CompileStateProbe
from helpers import make_writer


# ---------------------------------------------------------------------------
# #3: is_startup_in_progress — no state-file + PID alive → True
# ---------------------------------------------------------------------------

def test_is_startup_in_progress_no_state_file_pid_alive():
    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is True


# ---------------------------------------------------------------------------
# #4: is_startup_in_progress — state-file present → False
# ---------------------------------------------------------------------------

def test_is_startup_in_progress_state_file_present():
    from unittest.mock import MagicMock
    from unity_mcp.unity_state import UnityState
    state = MagicMock(spec=UnityState)
    state.is_stale = False
    state.is_busy = False
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is False


# ---------------------------------------------------------------------------
# #2: has_strong_busy_signal — startup in progress → True (no infinite skip)
# ---------------------------------------------------------------------------

def test_heartbeat_startup_grace_pid_alive_busy():
    """state=None (no state-file) + PID alive → is_startup_in_progress=True
    → has_strong_busy_signal=True → grace does NOT expire (busy signal holds)."""
    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is True


# ---------------------------------------------------------------------------
# #6 (x-plat): is_startup_in_progress routes through is_pid_alive, not os.kill
# ---------------------------------------------------------------------------

def test_is_startup_in_progress_uses_xplat_pid_check():
    """PID liveness goes through is_pid_alive(), not a direct os.kill call."""
    import unity_mcp.compile_state as cs_mod
    calls = []

    def fake_is_pid_alive(pid):
        calls.append(pid)
        return True

    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=42), \
         patch.object(cs_mod, "is_pid_alive", side_effect=fake_is_pid_alive), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(port=9500)
        result = probe.is_startup_in_progress()

    assert result is True
    assert 42 in calls, "is_pid_alive must have been called with the PID"


# ---------------------------------------------------------------------------
# #1: heartbeat grace expires when PID dead — _startup_grace_expired set
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_heartbeat_no_infinite_loop_pid_dead():
    """After STARTUP_GRACE_S with dead PID, _startup_grace_expired=True within grace+1 tick."""
    import unity_mcp.bridge_heartbeat as hb_mod

    orig_grace = bridge_mod.STARTUP_GRACE_S
    bridge_mod.STARTUP_GRACE_S = 0.0  # grace = 0 → expires immediately

    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = True
    probe.is_startup_in_progress = MagicMock(return_value=False)

    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    bridge._reconnect_started_at = time.monotonic() - 1.0  # already past grace

    # Run one tick directly
    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=ConnectionRefusedError("refused")):
        with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock):
            await bridge._heartbeat_tick(15.0)

    bridge_mod.STARTUP_GRACE_S = orig_grace
    assert bridge._startup_grace_expired is True


# ---------------------------------------------------------------------------
# #5: send() raises ConnectionError after grace expiry
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_post_grace_send_raises():
    """_startup_grace_expired=True → send() raises ConnectionError immediately."""
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = True
    probe.is_startup_in_progress = MagicMock(return_value=False)
    probe.is_unity_busy.return_value = False
    probe.estimated_remaining_s.return_value = 5.0

    from unity_mcp.bridge import BridgeState
    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    bridge._startup_grace_expired = True
    bridge._state = BridgeState.FAILED

    with pytest.raises(ConnectionError):
        await bridge.send("ping", {})
