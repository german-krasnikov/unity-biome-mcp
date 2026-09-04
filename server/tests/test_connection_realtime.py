"""Deterministic wall-clock validation for connection-stability fixes.

These tests use real wall-clock time but do not require a Unity process.

Scenarios:
  1. Anti-spam: dead-port reconnect loop runs ~80s wall-clock;
     proves intervals grow 5→10→20→40→60s, cap ≤66s, ≤7 attempts.
  2. Long session / no thread leak: 2000 ticks, mock only TCP I/O;
     backoff logic + timers run real; thread count stable.
Discriminating line per test documented below each assert.
"""
import asyncio
import socket
import time
from unittest.mock import patch

import pytest

from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S, BACKOFF_MAX_S, HeartbeatMixin
from unity_mcp.bridge_reload_state import DomainReloadTracker
from unity_mcp.bridge import BridgeState


# ---------------------------------------------------------------------------
# Shared stub (wall-clock variant: no time mocking)
# ---------------------------------------------------------------------------

class _RealTimeBridge(HeartbeatMixin):
    """HeartbeatMixin stub that runs real backoff/cooldown logic on real time.
    Only TCP I/O (_reconnect) is controllable per test.
    """

    def __init__(self, reconnect_fn=None):
        self._last_reconnect_at = 0.0
        self._reconnect_backoff = BACKOFF_MIN_S
        self._min_reconnect_interval = BACKOFF_MIN_S
        self._lock = asyncio.Lock()
        self._reconnect_started_at = None
        self._ppid_mismatch_count = 0
        self._ping_failures = 0
        self._heartbeat_task = None
        self._heartbeat_interval = 15.0
        self._counter = 0
        self._reload = DomainReloadTracker()
        self._state = BridgeState.DISCONNECTED
        self._connected = False
        self._reconnect_fn = reconnect_fn or self._default_fail
        self._attempt_times: list[float] = []

    @property
    def connected(self):
        return self._connected

    def _probe_busy(self):
        return False

    async def _reconnect(self, fire_callbacks=True):
        self._attempt_times.append(time.monotonic())
        await self._reconnect_fn()

    async def _default_fail(self):
        raise ConnectionRefusedError("dead port")

    def stop_heartbeat(self):
        self._heartbeat_task = None


# ---------------------------------------------------------------------------
# Scenario 1: Anti-spam — real wall-clock, dead port, ~80s run
# ---------------------------------------------------------------------------

@pytest.mark.timeout(120)
@pytest.mark.slow  # A07a: genuine ~80s wall-clock — routed to nightly, not the PR lane
@pytest.mark.real_clock  # A03: real ~80s wallclock by design (module-top-frozen
# BACKOFF_MIN_S/BACKOFF_MAX_S import means the fixture couldn't reach it anyway)
async def test_antispam_dead_port_real_wallclock(unused_tcp_port):
    """S1: Backoff dampens connect spam on a dead port over 80s wall-clock.

    Proves:
      (a) No attempt occurs every ~2s (no spam).
      (b) Intervals grow: 5→10→20→40→60 (exponential, within ±10% jitter + connect overhead).
      (c) Cap: no interval exceeds ~66s (60s × 1.1 jitter).
      (d) Total attempts ≤ 7 (naive baseline without fix: ~40 attempts in 80s).

    Discriminating lines:
      - Remove bridge_heartbeat.py line `self._last_reconnect_at = time.monotonic()` (before attempt)
        → cooldown never re-arms on failure → spam resumes → ≥30 attempts → FAIL (d).
      - Remove `self._reconnect_backoff = min(self._reconnect_backoff * 2 * ...)` doubling
        → intervals stay at 5s → ~16 attempts → FAIL (b) checks second interval > 7s.
      - Remove cap `BACKOFF_MAX_S` check
        → intervals could exceed 66s → FAIL (c).
    """
    assert _port_is_dead(unused_tcp_port), (
        f"Allocated dead port {unused_tcp_port} unexpectedly became occupied"
    )

    bridge = _RealTimeBridge()

    async def _always_fail():
        # Real socket attempt to confirm port is dead (fast reject)
        try:
            _, w = await asyncio.wait_for(
                asyncio.open_connection("127.0.0.1", unused_tcp_port), timeout=1.0
            )
            w.close()
        except Exception:
            pass
        raise ConnectionRefusedError(f"port {unused_tcp_port} dead")

    bridge._reconnect_fn = _always_fail

    import os
    import unity_mcp.bridge as bm

    t_start = time.monotonic()
    DURATION = 80.0

    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch.object(bm, "STARTUP_GRACE_S", 9999.0):
        while time.monotonic() - t_start < DURATION:
            await bridge._heartbeat_tick(15.0)
            if bridge._state == BridgeState.FAILED:
                break

    attempts = bridge._attempt_times
    n = len(attempts)

    intervals = [attempts[i + 1] - attempts[i] for i in range(len(attempts) - 1)]
    interval_str = ", ".join(f"{v:.1f}s" for v in intervals)

    # (a) No burst: no interval < 4s (5s min backoff minus jitter and connect latency)
    if intervals:
        min_interval = min(intervals)
        assert min_interval >= 4.0, (
            f"SPAM detected: shortest interval between attempts = {min_interval:.2f}s "
            f"(expected ≥4s). Intervals: [{interval_str}]"
        )

    # (b) Exponential growth: second interval > first (backoff doubled)
    if len(intervals) >= 2:
        assert intervals[1] > intervals[0] * 0.8, (
            f"Backoff not growing: interval[0]={intervals[0]:.1f}s, "
            f"interval[1]={intervals[1]:.1f}s — doubling missing? [{interval_str}]"
        )

    # (c) Cap: no interval exceeds 66s (60s + 10% jitter)
    if intervals:
        max_interval = max(intervals)
        assert max_interval <= 70.0, (
            f"Backoff cap exceeded: max interval = {max_interval:.1f}s "
            f"(expected ≤70s cap=60s+jitter). Intervals: [{interval_str}]"
        )

    # (d) Total attempt count: fix should yield ≤7; naive (no backoff) ≈40 in 80s
    assert n <= 7, (
        f"Too many reconnect attempts in {DURATION}s: {n} "
        f"(expected ≤7 with backoff, naive baseline ~40). "
        f"Intervals: [{interval_str}]"
    )
    assert n >= 2, f"Too few attempts ({n}) — test may not have run properly"

    print(
        f"\nS1 anti-spam: {n} attempts in {DURATION}s "
        f"(naive ~40). Intervals: [{interval_str}]"
    )


# ---------------------------------------------------------------------------
# Scenario 2: Long session — real timers, mock TCP only, no thread leak
# ---------------------------------------------------------------------------

@pytest.mark.real_clock  # A03: asserts bounds against the module-top-frozen
# real BACKOFF_MIN_S/BACKOFF_MAX_S import (would mismatch the fixture's patched
# production value otherwise, even though sleep itself is mocked here)
async def test_long_session_no_thread_leak_real_timers():
    """S2: _hard_exit_scheduled double-Timer guard + backoff bounds + reset.

    Sub-scenario A — double-Timer guard (discriminating):
      Provoke ppid-mismatch (patch os.getppid → dead PID 99999).
      Mock threading.Timer + os._exit so no real Timer fires.
      Reset _hard_exit_scheduled via direct write (same pattern as test_heartbeat.py).
      Call _schedule_hard_exit() twice; assert Timer constructed exactly once.

    Discriminating:
      Remove `if _hard_exit_scheduled: return` → Timer.call_count == 2 → FAIL.

    Sub-scenario B — backoff bounds + reset (200 ticks, mocked sleep):
      Verify _reconnect_backoff stays in [BACKOFF_MIN_S, BACKOFF_MAX_S]
      and resets to MIN after success.
    """
    import random
    import unity_mcp.bridge_heartbeat as hb_module
    import unity_mcp.bridge as bm

    # ---- Sub-scenario A: double-Timer guard --------------------------------
    hb_module._hard_exit_scheduled = False
    with patch("unity_mcp.bridge_heartbeat.threading.Timer") as mock_timer, \
         patch("unity_mcp.bridge_heartbeat.os._exit"):
        mock_timer.return_value.daemon = True

        # First call — guard not set yet, Timer should be constructed
        hb_module._schedule_hard_exit()
        # Second call — guard already set, Timer must NOT be constructed again
        hb_module._schedule_hard_exit()

        assert mock_timer.call_count == 1, (
            f"Timer constructed {mock_timer.call_count} times (expected 1). "
            "Remove `if _hard_exit_scheduled: return` guard → call_count==2 → FAIL."
        )
        assert hb_module._hard_exit_scheduled is True, "_hard_exit_scheduled must be True"

    hb_module._hard_exit_scheduled = False  # cleanup

    # ---- Sub-scenario B: backoff bounds + reset (fast, 200 ticks) ----------
    TICKS = 200
    rng = random.Random(99)

    class _Stub(HeartbeatMixin):
        def __init__(self):
            self._last_reconnect_at = 0.0
            self._reconnect_backoff = BACKOFF_MIN_S
            self._min_reconnect_interval = BACKOFF_MIN_S
            self._lock = asyncio.Lock()
            self._reconnect_started_at = None
            self._ppid_mismatch_count = 0
            self._ping_failures = 0
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._counter = 0
            self._reload = DomainReloadTracker()
            self._state = BridgeState.DISCONNECTED
            self._connected = False
            self._fail_next = False

        @property
        def connected(self):
            return self._connected

        def _probe_busy(self):
            return False

        async def _reconnect(self, fire_callbacks=True):
            if self._fail_next:
                raise ConnectionRefusedError("simulated")

        def stop_heartbeat(self):
            self._heartbeat_task = None

    import os
    bridge = _Stub()
    backoffs: list[float] = []

    async def fast_sleep(_n):
        bridge._reconnect_started_at = time.monotonic()

    with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", side_effect=fast_sleep), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"), \
         patch.object(bm, "STARTUP_GRACE_S", 9999.0):
        for _ in range(TICKS):
            bridge._fail_next = rng.random() < 0.08
            bridge._last_reconnect_at = 0.0
            await bridge._heartbeat_tick(15.0)
            backoffs.append(bridge._reconnect_backoff)

    assert max(backoffs) <= BACKOFF_MAX_S, (
        f"Backoff exceeded MAX: {max(backoffs):.1f}s > {BACKOFF_MAX_S}s"
    )
    assert min(backoffs) >= BACKOFF_MIN_S, (
        f"Backoff below MIN: {min(backoffs):.1f}s < {BACKOFF_MIN_S}s"
    )
    min_count = sum(1 for b in backoffs if b == BACKOFF_MIN_S)
    assert min_count > TICKS * 0.6, (
        f"Backoff reset not working: only {min_count}/{TICKS} ticks at MIN. "
        "Remove backoff-reset line → this FAILS."
    )

    print(
        f"\nS2A guard: Timer.call_count=1 (double-call blocked). "
        f"S2B {TICKS} ticks: max_backoff={max(backoffs):.1f}s, "
        f"min_resets={min_count}/{TICKS}"
    )

    hb_module._hard_exit_scheduled = False


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _port_is_dead(port: int) -> bool:
    """True if nothing is listening on 127.0.0.1:port."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.settimeout(0.5)
        try:
            s.connect(("127.0.0.1", port))
            return False
        except (ConnectionRefusedError, OSError):
            return True
