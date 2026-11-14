"""Tests for domain reload reconnect path — deadlock fix and expiry mechanics.

Covers gaps in existing test_bridge_heartbeat.py + test_reload_stability.py:
- T4/T5: deadlock fix — reconnect proceeds when reload active but probe NOT busy
- T6: cooldown regression guard
- T7: _reconnect() clears _reload state and opens _reload_gate on success
- T8: send() top guard fast-fails with DomainReloadError
- T11: multiple sequential reloads — tracker consistency
- T12: expiry safety net — auto-clears without reconnect
- T13: heartbeat marks reload on DomainReloadError from connected ping

Note: T1 (constant=45s), T2 (active at 30s), T3 (expired at 46s) live in
test_reload_stability.py — not duplicated here.
"""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from unity_mcp.bridge import UnityBridge, BridgeState
from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S
from unity_mcp.bridge_reload_state import DomainReloadTracker, DOMAIN_RELOAD_EXPIRY_S
from unity_mcp.bridge_socket import DomainReloadError


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_bridge_disconnected(busy: bool = False) -> UnityBridge:
    """Disconnected bridge with mocked probe. _writer=None → connected==False."""
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = busy
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    return UnityBridge("127.0.0.1", 9999, probe=probe)


def _make_connected_bridge() -> UnityBridge:
    """Bridge with a mock writer so connected==True."""
    bridge = _make_bridge_disconnected(busy=False)
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    bridge._writer = mock_writer
    bridge._reader = MagicMock()
    return bridge


# ---------------------------------------------------------------------------
# T4 — deadlock fix: reload active but probe NOT busy → reconnect proceeds
# ---------------------------------------------------------------------------

async def test_heartbeat_reconnects_when_reload_active_but_probe_not_busy():
    """CRITICAL: reload active + NOT busy → _reconnect() must be called.

    Old code: if _reload.is_active(): return  ← deadlock
    Fix:      if _reload.is_active() and _probe_busy(): return
    When probe not busy, condition is False → fall through to reconnect.
    """
    bridge = _make_bridge_disconnected(busy=False)
    bridge._reload.mark()  # simulate active reload

    with patch.object(bridge, "_reconnect", new=AsyncMock()) as mock_reconnect, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    mock_reconnect.assert_called_once()


# ---------------------------------------------------------------------------
# T5 — reload active AND busy → skip reconnect (correct behavior)
# ---------------------------------------------------------------------------

async def test_heartbeat_skips_reconnect_when_reload_active_and_probe_busy():
    """Reload + busy: condition True → early return, no reconnect attempt.

    Prevents hammering during compilation (Unity not ready for TCP).
    """
    bridge = _make_bridge_disconnected(busy=True)
    bridge._reload.mark()

    with patch.object(bridge, "_reconnect", new=AsyncMock()) as mock_reconnect, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    mock_reconnect.assert_not_called()


# ---------------------------------------------------------------------------
# T6 — cooldown still active → no reconnect regardless of reload state
# ---------------------------------------------------------------------------

async def test_heartbeat_skips_reconnect_when_cooldown_not_ok():
    """Cooldown active → _reconnect_cooldown_ok() returns False → skip.

    Regression guard: ensures cooldown check runs before reload check.
    """
    bridge = _make_bridge_disconnected(busy=False)
    # Just reconnected — cooldown window still active
    bridge._last_reconnect_at = time.monotonic()
    bridge._reconnect_backoff = BACKOFF_MIN_S  # 5s cooldown

    with patch.object(bridge, "_reconnect", new=AsyncMock()) as mock_reconnect, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    mock_reconnect.assert_not_called()


# ---------------------------------------------------------------------------
# T7 — _reconnect() clears reload state on success
# ---------------------------------------------------------------------------

async def test_reconnect_clears_reload_state_on_success():
    """Successful _reconnect(): _reload.clear() + _reload_gate.set() + CONNECTED.

    This is the mechanism that breaks the deadlock: once reconnect succeeds,
    _reload.is_active() returns False so send() callers are unblocked.
    """
    from tests.helpers import reconnect_preamble

    bridge = _make_bridge_disconnected()
    bridge._reload.mark()
    bridge._reload_gate.clear()  # simulate send() waiting

    preamble = reconnect_preamble()  # [ping_hdr, ping_pay, ver_hdr, ver_pay]
    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(side_effect=preamble)

    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.get_extra_info.return_value = None
    mock_writer.write = MagicMock()
    mock_writer.drain = AsyncMock()
    mock_writer.wait_closed = AsyncMock()

    with patch("asyncio.open_connection", new=AsyncMock(return_value=(mock_reader, mock_writer))), \
         patch("unity_mcp.bridge._apply_socket_options"), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(bridge, "start_heartbeat"):  # avoid background task leak
        await bridge._reconnect()

    assert bridge._reload.is_active() is False, "_reload must be cleared after reconnect"
    assert bridge._reload_gate.is_set() is True, "_reload_gate must be opened after reconnect"
    assert bridge._state == BridgeState.CONNECTED


# ---------------------------------------------------------------------------
# T8 — send() fast-fails when reload active (top guard)
# ---------------------------------------------------------------------------

async def test_send_raises_immediately_when_reload_active():
    """send() guard at bridge.py:183-184 raises DomainReloadError without entering retry.

    The fast-fail prevents send() from attempting TCP when Unity is reloading.
    """
    bridge = _make_bridge_disconnected()
    bridge._reload.mark()

    with pytest.raises(DomainReloadError, match="Domain reload in progress"):
        await bridge.send("ping", {})


# ---------------------------------------------------------------------------
# T11 — multiple sequential reloads: tracker stays consistent
# ---------------------------------------------------------------------------

def test_multiple_rapid_reloads_tracker_consistency():
    """mark/clear/mark/clear cycle leaves tracker in clean state each time.

    Scenario: test run starts → reload → test ends → new reload.
    No time mocking needed — only state transitions exercised.
    """
    tracker = DomainReloadTracker()

    tracker.mark()
    assert tracker.is_active() is True

    tracker.clear()
    assert tracker.is_active() is False
    assert tracker._active is False
    assert tracker._since is None

    tracker.mark()
    assert tracker.is_active() is True

    tracker.clear()
    assert tracker.is_active() is False


# ---------------------------------------------------------------------------
# T12 — expiry auto-clears without any reconnect (safety net)
# ---------------------------------------------------------------------------

def test_expiry_auto_clears_without_reconnect():
    """After DOMAIN_RELOAD_EXPIRY_S seconds, is_active() auto-clears and returns False.

    Safety net: if _reconnect() never fires (Unity crashed), the tracker
    eventually expires so subsequent heartbeat ticks escape the reload path.
    """
    tracker = DomainReloadTracker()
    tracker.mark()
    # Backdate _since past expiry
    tracker._since = time.monotonic() - (DOMAIN_RELOAD_EXPIRY_S + 1.0)

    result = tracker.is_active()

    assert result is False
    assert tracker._active is False
    assert tracker._since is None


# ---------------------------------------------------------------------------
# T13 — heartbeat marks reload when connected ping raises DomainReloadError
# ---------------------------------------------------------------------------

async def test_heartbeat_marks_reload_on_domain_reload_error_ping():
    """Connected heartbeat receives DomainReloadError → marks reload + calls probe.

    Corresponds to bridge_heartbeat.py:157-162 (DomainReloadError except clause).
    Also resets _ping_stall_failures to 0 (covered by test_bridge_heartbeat.py).
    """
    bridge = _make_connected_bridge()
    bridge._reload._active = False  # start clean

    async def fake_ping(*a, **kw):
        raise DomainReloadError("going_away")

    with patch.object(bridge, "_raw_ping", new=fake_ping), \
         patch.object(bridge, "close", new=AsyncMock()), \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    assert bridge._reload.is_active() is True, "_reload must be marked after DomainReloadError"
    bridge._probe.mark_recompile_issued.assert_called_once()


# ---------------------------------------------------------------------------
# Phase 3 — fast reconnect: backoff reset on DomainReloadError
# ---------------------------------------------------------------------------

def test_backoff_reset_on_domain_reload_error():
    """DomainReloadError in should_retry() resets _reconnect_backoff to RELOAD_BACKOFF_S.

    Expected reloads (PlayMode enter) should reconnect fast (1s), not at the
    standard exponential backoff ceiling (5-60s for unexpected disconnects).
    """
    from unity_mcp.bridge_heartbeat import RELOAD_BACKOFF_S

    bridge = _make_bridge_disconnected()
    bridge._reconnect_backoff = 30.0  # simulating high backoff from previous failures

    deadline = time.monotonic() + 60.0
    bridge.should_retry(DomainReloadError("domain reload test"), 0, deadline)

    assert bridge._reconnect_backoff == RELOAD_BACKOFF_S, (
        f"Expected _reconnect_backoff == {RELOAD_BACKOFF_S} after DomainReloadError, "
        f"got {bridge._reconnect_backoff}"
    )
