"""Tests for HeartbeatMixin — hard deadline timer behavior."""
import asyncio
import time
import pytest
from unittest.mock import MagicMock, patch, AsyncMock, Mock

from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_heartbeat import HARD_DEADLINE_S
from unity_mcp.bridge_socket import DomainReloadError
from helpers import make_bridge_disconnected as _make_bridge_disconnected


# ── Item 8: hard deadline uses separate clock, unaffected by busy resets ────


async def test_hard_deadline_started_at_not_reset_on_busy():
    """_hard_deadline_started_at must keep its initial value when busy=True.

    Fix: separate _hard_deadline_started_at variable that is set once and never
    reset by the busy-state logic (which resets _reconnect_started_at for STARTUP_GRACE).
    """
    bridge = _make_bridge_disconnected(busy=True)

    # Simulate: hard deadline clock was set 10s ago
    initial = time.monotonic() - 10.0
    bridge._hard_deadline_started_at = initial

    with patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    # Hard deadline clock must NOT have been reset
    assert bridge._hard_deadline_started_at == initial, (
        f"_hard_deadline_started_at was reset from {initial} to {bridge._hard_deadline_started_at}"
    )


async def test_hard_deadline_fires_even_when_busy():
    """Hard deadline must trigger even if Unity is permanently busy.

    Bug: old code used _reconnect_started_at for hard deadline — resetting it
    on every busy tick made elapsed always ~0, so HARD_DEADLINE_S never fired.
    Fix: _hard_deadline_started_at is set once and never reset while busy.
    """
    bridge = _make_bridge_disconnected(busy=True)

    # Set hard deadline clock to past the threshold
    bridge._hard_deadline_started_at = time.monotonic() - (HARD_DEADLINE_S + 5.0)

    with patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert bridge._startup_grace_expired is True, (
        "Hard deadline did not fire despite hard_elapsed > HARD_DEADLINE_S while busy"
    )


# ---------------------------------------------------------------------------
# RC3 — alive-stall branch: do NOT close on process-alive ping failure
# ---------------------------------------------------------------------------

def _make_connected_bridge() -> UnityBridge:
    """Return a UnityBridge with a live writer mock so connected == True."""
    from unity_mcp.compile_state import CompileStateProbe
    from unittest.mock import MagicMock
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    bridge._writer = mock_writer
    bridge._reader = MagicMock()
    return bridge


async def test_heartbeat_does_not_close_on_alive_stall():
    """RC3: ping times out 3×, process alive → stall counter increments, NO close().

    TimeoutError = Unity alive but unresponsive (App Nap / heavy compile).
    Bug: old code called close() in both dead AND alive branches after 3 failures.
    """
    bridge = _make_connected_bridge()
    bridge._probe.is_process_dead.return_value = False
    bridge._ping_failures = 2  # += 1 inside tick → 3 → triggers check

    async def fail_ping(*a, **kw):
        raise asyncio.TimeoutError("simulated timeout")

    with patch.object(bridge, "_raw_ping", new=fail_ping), \
         patch.object(bridge, "close", new=AsyncMock()) as mock_close, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    mock_close.assert_not_called()
    assert bridge._ping_stall_failures == 1


async def test_heartbeat_closes_on_confirmed_dead():
    """RC3: ping times out 3×, process dead → close() IS called."""
    bridge = _make_connected_bridge()
    bridge._probe.is_process_dead.return_value = True
    bridge._ping_failures = 2  # += 1 → 3

    async def fail_ping(*a, **kw):
        raise asyncio.TimeoutError("simulated timeout")

    with patch.object(bridge, "_raw_ping", new=fail_ping), \
         patch.object(bridge, "close", new=AsyncMock()) as mock_close, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    mock_close.assert_called_once()


async def test_heartbeat_closes_after_6_stall_windows():
    """RC3: after 6 alive-stall windows, close() IS called (safety net)."""
    bridge = _make_connected_bridge()
    bridge._probe.is_process_dead.return_value = False
    bridge._ping_failures = 2  # += 1 → 3
    bridge._ping_stall_failures = 5  # one more → 6 → close

    async def fail_ping(*a, **kw):
        raise asyncio.TimeoutError("simulated timeout")

    with patch.object(bridge, "_raw_ping", new=fail_ping), \
         patch.object(bridge, "close", new=AsyncMock()) as mock_close, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    mock_close.assert_called_once()


async def test_heartbeat_closes_immediately_on_connection_error():
    """RC3/Fix2: OSError/connection errors close immediately (not stall logic).

    Non-timeout exceptions = dead TCP → close at once, no stall counter.
    """
    bridge = _make_connected_bridge()
    bridge._probe.is_process_dead.return_value = False
    bridge._ping_failures = 0  # below stall threshold

    async def fail_ping(*a, **kw):
        raise OSError("connection reset")

    with patch.object(bridge, "_raw_ping", new=fail_ping), \
         patch.object(bridge, "close", new=AsyncMock()) as mock_close, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    # Connection error → close immediately, no stall counter
    mock_close.assert_called_once()
    assert bridge._ping_stall_failures == 0


async def test_heartbeat_resets_stall_on_success():
    """RC3: successful ping resets _ping_stall_failures to 0."""
    bridge = _make_connected_bridge()
    bridge._ping_stall_failures = 3

    async def ok_ping(*a, **kw):
        pass

    with patch.object(bridge, "_raw_ping", new=ok_ping), \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    assert bridge._ping_stall_failures == 0


# ---------------------------------------------------------------------------
# MAJOR #1: Heartbeat self-cancel при reconnect
# ---------------------------------------------------------------------------

async def test_close_skips_stop_heartbeat_when_called_from_heartbeat_task():
    """close() must NOT call stop_heartbeat() when current_task is the heartbeat task.

    Bug: close() unconditionally calls stop_heartbeat(), which does hb_task.cancel()
    even when close() is invoked from WITHIN the heartbeat loop itself. The resulting
    CancelledError propagates up through the loop → heartbeat task exits prematurely.

    Fix: close() guards with `if asyncio.current_task() is not self._heartbeat_task`.
    """
    bridge = UnityBridge()
    bridge.start_heartbeat(interval=999.0)  # long interval so tick does not run
    hb_task = bridge._heartbeat_task

    stop_called = []

    def spy_stop():
        stop_called.append(True)
        # Don't call real stop_heartbeat to avoid side effects
    bridge.stop_heartbeat = spy_stop

    # Simulate: close() is called while current_task IS the heartbeat task
    with patch("asyncio.current_task", return_value=hb_task):
        await bridge.close()

    # Before fix: stop_called == [True] (unconditional stop_heartbeat)
    # After fix: stop_called == [] (skipped because current_task == heartbeat_task)
    assert not stop_called, (
        "close() called stop_heartbeat() even though current_task is the heartbeat task; "
        "this causes self-cancellation of the heartbeat loop"
    )
    # Clean up — heartbeat task still running (we spied but didn't cancel)
    if hb_task and not hb_task.done():
        hb_task.cancel()
        try:
            await hb_task
        except asyncio.CancelledError:
            pass


async def test_heartbeat_loop_reraises_cancelled_error():
    """S7497: CancelledError inside heartbeat loop must propagate, not be swallowed."""
    bridge = UnityBridge("127.0.0.1", 9999)
    with patch.object(bridge, "_heartbeat_tick", new=AsyncMock(side_effect=asyncio.CancelledError())):
        with pytest.raises(asyncio.CancelledError):
            await bridge._heartbeat_loop(1.0)


# ---------------------------------------------------------------------------
# MAJOR #2: _ping_stall_failures не сбрасывается
# ---------------------------------------------------------------------------

async def test_ping_stall_failures_reset_on_domain_reload():
    """_ping_stall_failures must be 0 after DomainReloadError in heartbeat tick."""
    from unity_mcp.bridge_heartbeat import ProtocolDesyncError
    bridge = _make_connected_bridge()
    bridge._ping_stall_failures = 4

    async def fake_ping(*a, **kw):
        raise DomainReloadError("reload")

    with patch.object(bridge, "_raw_ping", new=fake_ping), \
         patch.object(bridge, "close", new=AsyncMock()), \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    assert bridge._ping_stall_failures == 0, (
        f"_ping_stall_failures must be 0 after DomainReloadError, got {bridge._ping_stall_failures}"
    )


async def test_ping_stall_failures_reset_on_protocol_desync():
    """_ping_stall_failures must be 0 after ProtocolDesyncError in heartbeat tick."""
    from unity_mcp.bridge_heartbeat import ProtocolDesyncError
    bridge = _make_connected_bridge()
    bridge._ping_stall_failures = 3

    async def fake_ping(*a, **kw):
        raise ProtocolDesyncError("desync")

    with patch.object(bridge, "_raw_ping", new=fake_ping), \
         patch.object(bridge, "close", new=AsyncMock()), \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    assert bridge._ping_stall_failures == 0, (
        f"_ping_stall_failures must be 0 after ProtocolDesyncError, got {bridge._ping_stall_failures}"
    )


# ---------------------------------------------------------------------------
# Phase 3 — fast poll: reload active + not busy → RELOAD_BACKOFF_S sleep
# ---------------------------------------------------------------------------

async def test_heartbeat_uses_reload_backoff_when_reload_active_not_busy():
    """Reload active but probe NOT busy → sleep uses RELOAD_BACKOFF_S (1.0s), not 2.0s.

    This gives near-instant reconnect when Unity finishes compilation and writes
    state=ready to the state file, instead of waiting the default 2s poll interval.
    """
    from unity_mcp.bridge_heartbeat import RELOAD_BACKOFF_S

    bridge = _make_bridge_disconnected(busy=False)
    bridge._reload.mark()  # active domain reload

    sleep_args: list[float] = []

    async def capture_sleep(t: float) -> None:
        sleep_args.append(t)

    with patch.object(bridge, "_reconnect", new=AsyncMock()), \
         patch.object(asyncio, "sleep", new=capture_sleep):
        await bridge._heartbeat_tick(interval=15.0)

    assert sleep_args, "sleep() was not called in heartbeat tick"
    assert sleep_args[0] == RELOAD_BACKOFF_S, (
        f"Expected sleep({RELOAD_BACKOFF_S}) for reload+not-busy, got sleep({sleep_args[0]})"
    )
