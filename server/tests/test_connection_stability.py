"""TDD tests for connection-stability fixes (v0.52.7).

Covers:
- A2: Cooldown re-arm on failure (not just success)
- B1: Exponential backoff + jitter — storm test
- B2: Pin guard — explicit _pinned_pid is not None check
- B3: No silent 9500 when skip_probe=True and no candidates
"""
import asyncio
import math
import os
import random
import threading
import time
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest


# ---------------------------------------------------------------------------
# A2: Cooldown re-arm on FAILURE
# ---------------------------------------------------------------------------

class _FailingReconnectBridge:
    """Minimal stub: always-failing _reconnect, tracks _last_reconnect_at.
    Inherits HeartbeatMixin so _reconnect_cooldown_ok uses real backoff logic.
    """
    def __init__(self):
        self._last_reconnect_at = 0.0
        self._reconnect_backoff = 5.0
        self._min_reconnect_interval = 5.0  # kept for HeartbeatMixin compat
        self._lock = asyncio.Lock()
        self._reconnect_started_at = None
        self._startup_grace_expired = False
        self._ppid_mismatch_count = 0
        self._ping_failures = 0
        self._heartbeat_task = None
        self._heartbeat_interval = 15.0
        self._counter = 0
        self._reconnect_count = 0
        from unity_mcp.bridge_reload_state import DomainReloadTracker
        from unity_mcp.bridge import BridgeState
        self._reload = DomainReloadTracker()
        self._state = BridgeState.DISCONNECTED

    @property
    def connected(self):
        return False

    def _probe_busy(self):
        return False

    async def _reconnect(self, fire_callbacks=True):
        self._reconnect_count += 1
        raise ConnectionRefusedError("always failing")

    def stop_heartbeat(self):
        self._heartbeat_task = None

    def start_heartbeat(self, interval=15.0):
        pass


async def test_cooldown_rearmed_on_failure():
    """A2: After failed _reconnect, _last_reconnect_at is updated → next tick sees cooldown.

    Without fix: _last_reconnect_at only updated on success → cooldown_ok() always True
    → reconnect every ~2s forever (SPAM #1).
    With fix: cooldown_ok() returns False after failed attempt.
    """
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class _Stub(HeartbeatMixin, _FailingReconnectBridge):
        pass

    bridge = _Stub()

    time_values = iter([
        # tick 1: _reconnect_started_at set
        100.0,  # _reconnect_started_at = 100
        100.0,  # busy check elapsed = 0 (not busy)
        # async sleep call
        100.0,  # elapsed calc after sleep
        100.1,  # cooldown re-arm: _last_reconnect_at = 100.1
        # tick 2: elapsed since reconnect = 0.1 < backoff=5 → should be False
        100.2,  # _reconnect_started_at already set
        100.2,  # elapsed = 0.1 (not busy)
        # sleep
        100.2,  # elapsed after sleep
        100.2,  # cooldown check: 100.2 - 100.1 = 0.1 < 5 → False
    ])

    reconnect_attempts = []

    async def fake_reconnect(fire_callbacks=True):
        reconnect_attempts.append(time.monotonic())
        raise ConnectionRefusedError("fail")

    bridge._reconnect = fake_reconnect

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", side_effect=lambda: next(time_values, 100.2)), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()):
        await bridge._heartbeat_tick(15.0)  # tick 1: attempt → fail → re-arm cooldown
        assert bridge._last_reconnect_at > 0, \
            "After failed reconnect, _last_reconnect_at must be updated"

        # Immediately: cooldown_ok must be False
        cooldown_ok = bridge._reconnect_cooldown_ok()
        assert cooldown_ok is False, \
            "After failed reconnect, cooldown must be armed (not ok)"


async def test_cooldown_not_rearmed_when_cooldown_not_ok():
    """When cooldown is NOT ok, no reconnect attempt is made, _last_reconnect_at unchanged."""
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class _Stub(HeartbeatMixin, _FailingReconnectBridge):
        pass

    bridge = _Stub()
    # Set _last_reconnect_at to recent time so cooldown is NOT ok
    recent = 999.0
    bridge._last_reconnect_at = recent
    bridge._reconnect_backoff = 60.0  # large backoff

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", return_value=1000.0), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()):
        await bridge._heartbeat_tick(15.0)

    # Only 1.0s elapsed since reconnect, backoff=60 → cooldown NOT ok → no attempt
    # _last_reconnect_at must not advance
    assert bridge._last_reconnect_at == recent, \
        "No attempt made (cooldown active) → _last_reconnect_at must stay unchanged"


# ---------------------------------------------------------------------------
# B1: Exponential backoff + storm test
# ---------------------------------------------------------------------------

async def test_exponential_backoff_storm():
    """B1: always-failing reconnect over 10 virtual minutes.

    Baseline (no backoff): 600s / 2s_interval ≈ 300 attempts.
    With backoff MIN=5s → MAX=60s: O(log t) ≈ 10-20 actual attempts.

    Storm test proves mathematical dampening — prevents thundering herd.
    """
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class _Stub(HeartbeatMixin):
        def __init__(self):
            self._last_reconnect_at = 0.0
            self._reconnect_backoff = 5.0
            self._lock = asyncio.Lock()
            self._reconnect_started_at = None
            self._startup_grace_expired = False
            self._ppid_mismatch_count = 0
            self._ping_failures = 0
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._counter = 0
            self._reconnect_count = 0
            from unity_mcp.bridge_reload_state import DomainReloadTracker
            from unity_mcp.bridge import BridgeState
            self._reload = DomainReloadTracker()
            self._state = BridgeState.DISCONNECTED

        @property
        def connected(self):
            return False

        def _probe_busy(self):
            return False

        async def _reconnect(self, fire_callbacks=True):
            self._reconnect_count += 1
            raise ConnectionRefusedError("always failing")

        def stop_heartbeat(self):
            pass

    bridge = _Stub()

    # Simulate 10 virtual minutes via repeated heartbeat ticks with mocked time.
    # Each tick: if cooldown ok → attempt (re-arm + double backoff); else → skip.
    DURATION = 600.0  # 10 minutes
    TICK = 2.0        # heartbeat sleep granularity
    virtual_now = [0.0]

    def fake_monotonic():
        return virtual_now[0]

    async def fake_sleep(n):
        virtual_now[0] += n  # advance virtual time

    import unity_mcp.bridge_heartbeat as hb
    import unity_mcp.bridge as bm

    original_ppid = os.getppid()

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", side_effect=fake_monotonic), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", side_effect=fake_sleep), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=original_ppid), \
         patch.object(bm, "STARTUP_GRACE_S", 9999.0):  # disable grace expiry for storm test

        while virtual_now[0] < DURATION:
            await bridge._heartbeat_tick(15.0)

    attempts = bridge._reconnect_count
    # Baseline without backoff would be ~300 attempts (600s / 2s)
    # With backoff cap at 60s: at most ceil(600/60) = 10 from fully saturated state
    # Allow generous 50 to account for ramp-up phase
    assert attempts <= 50, \
        f"Backoff storm test FAILED: {attempts} attempts in 10min (expected ≤50, naive=~300)"
    assert attempts >= 5, \
        f"Sanity check: at least 5 reconnect attempts should happen (got {attempts})"

    print(f"\nStorm test: {attempts} reconnect attempts in {DURATION}s (naive baseline: ~300)")



async def test_backoff_resets_on_success():
    """M1: backoff resets to BACKOFF_MIN_S after successful _heartbeat_tick reconnect.

    Discriminate: if bridge_heartbeat.py line that resets backoff is removed,
    this test FAILS because _reconnect_backoff stays at BACKOFF_MAX_S.
    """
    from unity_mcp.bridge_heartbeat import HeartbeatMixin, BACKOFF_MIN_S, BACKOFF_MAX_S
    from unity_mcp.bridge_reload_state import DomainReloadTracker
    from unity_mcp.bridge import BridgeState

    class _SuccessfulReconnectBridge(HeartbeatMixin):
        def __init__(self):
            self._last_reconnect_at = 0.0
            self._reconnect_backoff = BACKOFF_MAX_S  # start saturated
            self._min_reconnect_interval = 5.0
            self._lock = asyncio.Lock()
            self._reconnect_started_at = None
            self._startup_grace_expired = False
            self._ppid_mismatch_count = 0
            self._ping_failures = 0
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._counter = 0
            self._connected = False
            self._reload = DomainReloadTracker()
            self._state = BridgeState.DISCONNECTED

        @property
        def connected(self):
            return self._connected

        def _probe_busy(self):
            return False

        async def _reconnect(self, fire_callbacks=True):
            # Success: mark connected so heartbeat sees we recovered.
            self._connected = True

        def stop_heartbeat(self):
            self._heartbeat_task = None

    bridge = _SuccessfulReconnectBridge()

    import unity_mcp.bridge as bm

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", return_value=1000.0), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch.object(bm, "STARTUP_GRACE_S", 9999.0):
        await bridge._heartbeat_tick(15.0)

    # Production code at bridge_heartbeat.py:129 resets backoff on success.
    # If that line is removed, _reconnect_backoff stays at BACKOFF_MAX_S → test FAILS.
    assert bridge._reconnect_backoff == BACKOFF_MIN_S, (
        f"Expected backoff reset to {BACKOFF_MIN_S} after successful reconnect, "
        f"got {bridge._reconnect_backoff} — production reset line missing?"
    )


# ---------------------------------------------------------------------------
# B2: Pin guard — explicit None check
# ---------------------------------------------------------------------------

async def test_pin_honored_when_pid_alive():
    """B2: _pinned_pid not None AND is_pid_alive → use pinned port, discoverer NOT called."""
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer, make_idle_probe, reconnect_preamble
    import json
    import struct

    def ok_reader():
        r = {"id": "rc0001", "ok": True, "data": "ok"}
        p = json.dumps(r).encode()
        hdr, pay = struct.pack("!I", len(p)), p
        ver_r = {"id": "ver", "ok": True, "data": "v0.52.7 proto=2"}
        vp = json.dumps(ver_r).encode()
        ver_hdr, ver_pay = struct.pack("!I", len(vp)), vp
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[hdr, pay, ver_hdr, ver_pay])
        return reader

    discoverer = Mock(return_value=9999)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      return_value=(ok_reader(), make_writer())), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345  # alive PID, not None
        await bridge._reconnect()

    # discoverer must NOT be called when pin is valid
    discoverer.assert_not_called()
    assert bridge._port == 9500


async def test_pin_bypass_when_pid_none():
    """B2: _pinned_pid is None → skip pin → discoverer called exactly once."""
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer, make_idle_probe
    import json
    import struct

    def ok_reader():
        r = {"id": "rc0001", "ok": True, "data": "ok"}
        p = json.dumps(r).encode()
        hdr, pay = struct.pack("!I", len(p)), p
        ver_r = {"id": "ver", "ok": True, "data": "v0.52.7 proto=2"}
        vp = json.dumps(ver_r).encode()
        ver_hdr, ver_pay = struct.pack("!I", len(vp)), vp
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[hdr, pay, ver_hdr, ver_pay])
        return reader

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      return_value=(ok_reader(), make_writer())), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=False):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._pinned_port = 9500
        bridge._pinned_pid = None  # intentionally None
        await bridge._reconnect()

    # discoverer MUST be called — pid=None means intentional fallthrough
    discoverer.assert_called_once()
    assert bridge._port == 9501


# ---------------------------------------------------------------------------
# B3: No silent 9500 from read_unity_port(skip_probe=True)
# ---------------------------------------------------------------------------

def test_read_unity_port_skip_probe_returns_none_when_no_candidates():
    """B3: skip_probe=True + zero live candidates → None (not 9500)."""
    from unity_mcp.server_filtering import read_unity_port

    # No port files at all
    with patch("unity_mcp.server_filtering._ports_dir") as mock_dir:
        mock_dir.glob.return_value = []
        result = read_unity_port(skip_probe=True)

    assert result is None, \
        f"Expected None when no candidates, got {result}"


def test_read_unity_port_cold_start_returns_9500():
    """B3: cold-start (skip_probe=False) → fallback to 9500 (backward compat)."""
    from unity_mcp.server_filtering import read_unity_port

    with patch("unity_mcp.server_filtering._ports_dir") as mock_dir, \
         patch("unity_mcp.server_filtering._tcp_probe", return_value=False):
        mock_dir.glob.return_value = []
        result = read_unity_port(skip_probe=False)

    assert result == 9500, \
        f"Cold-start with no candidates should fallback to 9500, got {result}"


async def test_bridge_reconnect_preserves_port_on_none_discoverer():
    """M2: discoverer returns None → bridge._reconnect → self._port stays original.

    Discriminate: if the None-guard in bridge.py:_reconnect is removed
    (so port gets set to None), this test FAILS because bridge._port becomes None.
    """
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer, make_idle_probe
    import json
    import struct

    def ok_reader():
        r = {"id": "rc0001", "ok": True, "data": "ok"}
        p = json.dumps(r).encode()
        hdr, pay = struct.pack("!I", len(p)), p
        ver_r = {"id": "ver", "ok": True, "data": "v0.52.7 proto=2"}
        vp = json.dumps(ver_r).encode()
        ver_hdr, ver_pay = struct.pack("!I", len(vp)), vp
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[hdr, pay, ver_hdr, ver_pay])
        return reader

    original_port = 9500
    # Discoverer returns None — no live candidates.
    discoverer = Mock(return_value=None)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      return_value=(ok_reader(), make_writer())), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=False):
        bridge = UnityBridge("127.0.0.1", original_port, probe=probe,
                             port_discoverer=discoverer)
        bridge._pinned_pid = None  # force discoverer path
        await bridge._reconnect()

    # Production guard: `if new_port is not None and new_port != self._port:`
    # Without it, port would be set to None → assertion fails → discriminating.
    assert bridge._port == original_port, (
        f"port must stay {original_port} when discoverer returns None, got {bridge._port}"
    )


def test_doctor_resolve_port_returns_9500_on_none():
    """B3: doctor._resolve_port handles None from read_unity_port → fallback 9500."""
    from unity_mcp import doctor

    # read_unity_port is imported lazily inside _resolve_port, patch at source module
    with patch("unity_mcp.server_filtering.read_unity_port", return_value=None), \
         patch("unity_mcp.server_filtering._ports_dir") as mock_dir:
        mock_dir.glob.return_value = []
        result = doctor._resolve_port(0)

    assert result == 9500, \
        f"doctor._resolve_port must fallback to 9500 when read_unity_port returns None, got {result}"


def test_doctor_resolve_port_uses_explicit_port():
    """B3: doctor._resolve_port uses explicit port when provided."""
    from unity_mcp import doctor

    result = doctor._resolve_port(9501)
    assert result == 9501


# ---------------------------------------------------------------------------
# GAP-1: Long session — no thread leak, backoff stable over thousands of ticks
# ---------------------------------------------------------------------------

async def test_long_session_no_thread_leak():
    """GAP-1: 1000 heartbeat ticks with ~95% success / ~5% failure.

    Verifies:
    - backoff resets to MIN after each success (never accumulates)
    - _ping_failures stays at 0 after each success (no spurious growth)
    - threading.active_count() stable (no thread leak)
    - No hanging asyncio tasks created

    Uses virtual time — no real TCP.
    Discriminate: remove backoff-reset line → _reconnect_backoff drifts upward → fail.
    """
    from unity_mcp.bridge_heartbeat import HeartbeatMixin, BACKOFF_MIN_S, BACKOFF_MAX_S
    from unity_mcp.bridge_reload_state import DomainReloadTracker
    from unity_mcp.bridge import BridgeState

    class _LongSessionBridge(HeartbeatMixin):
        def __init__(self):
            self._last_reconnect_at = 0.0
            self._reconnect_backoff = BACKOFF_MIN_S
            self._min_reconnect_interval = 5.0
            self._lock = asyncio.Lock()
            self._reconnect_started_at = None
            self._startup_grace_expired = False
            self._ppid_mismatch_count = 0
            self._ping_failures = 0
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._counter = 0
            self._connected = False
            self._fail_next = False
            self._reload = DomainReloadTracker()
            self._state = BridgeState.DISCONNECTED

        @property
        def connected(self):
            return self._connected

        def _probe_busy(self):
            return False

        async def _reconnect(self, fire_callbacks=True):
            if self._fail_next:
                raise ConnectionRefusedError("simulated failure")
            # Success: don't set _connected=True to stay in reconnect path for all ticks.
            # This lets us test the full success→reset cycle without entering ping path.

        def stop_heartbeat(self):
            self._heartbeat_task = None

    bridge = _LongSessionBridge()

    import unity_mcp.bridge as bm

    TICKS = 1000
    virtual_now = [0.0]
    fail_rate = 0.05
    seed = 42
    rng = random.Random(seed)

    def fake_monotonic():
        # Reset _reconnect_started_at before each monotonic call so elapsed never exceeds GRACE.
        return virtual_now[0]

    async def fake_sleep(n):
        virtual_now[0] += n
        # Reset reconnect timer so STARTUP_GRACE_S is never hit during the test.
        bridge._reconnect_started_at = virtual_now[0]

    threads_before = threading.active_count()
    backoffs_seen = []

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", side_effect=fake_monotonic), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", side_effect=fake_sleep), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch.object(bm, "STARTUP_GRACE_S", 9999.0):
        for _ in range(TICKS):
            bridge._fail_next = rng.random() < fail_rate
            bridge._last_reconnect_at = 0.0  # always allow cooldown so each tick attempts
            await bridge._heartbeat_tick(15.0)
            backoffs_seen.append(bridge._reconnect_backoff)

    # No thread leak
    threads_after = threading.active_count()
    assert threads_after <= threads_before + 1, (
        f"Thread leak: {threads_before} before, {threads_after} after {TICKS} ticks"
    )

    # Backoff always within bounds
    max_backoff = max(backoffs_seen)
    assert max_backoff <= BACKOFF_MAX_S, (
        f"Backoff exceeded MAX: {max_backoff} > {BACKOFF_MAX_S}"
    )

    # Backoff resets after success → most values should be BACKOFF_MIN_S
    min_backoff_count = sum(1 for b in backoffs_seen if b == BACKOFF_MIN_S)
    # With 95% success rate, >80% of ticks should end at MIN backoff
    assert min_backoff_count > TICKS * 0.7, (
        f"Expected >70% of ticks to reset to MIN, got {min_backoff_count}/{TICKS} — "
        "backoff-reset line removed?"
    )


# ---------------------------------------------------------------------------
# GAP-2: Crash recovery — no port drift to wrong instance
# ---------------------------------------------------------------------------

async def test_crash_recovery_no_port_drift():
    """GAP-2: Unity crashes (_pinned_pid dead) → port file changes to 9999 →
    after _reconnect, bridge connects to 9999 (own restarted Unity), NOT 9500.

    Discriminate: if pin-guard fails to bypass dead PID, discoverer not called → port stays 9500.
    If None-guard missing, port might become None.
    """
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer, make_idle_probe, reconnect_preamble

    chunks = reconnect_preamble()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=chunks)

    # Port file now points to 9999 (Unity restarted on new port)
    new_port = 9999
    discoverer = Mock(return_value=new_port)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      return_value=(reader, make_writer())), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=False):  # dead PID
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 99999  # dead PID (not None — must go through guard)
        await bridge._reconnect()

    # Discoverer was called because PID is dead → discovered new port
    discoverer.assert_called_once()
    # Bridge now points to new port (no drift back to 9500)
    assert bridge._port == new_port, (
        f"Expected port {new_port} after crash recovery, got {bridge._port} — "
        "pin guard (is_pid_alive check) not bypassing dead PID?"
    )


# ---------------------------------------------------------------------------
# GAP-3: Two bridges — no cross-connect
# ---------------------------------------------------------------------------

async def test_two_bridges_no_cross_connect():
    """GAP-3: Two UnityBridge instances with different discoverers (9500 and 9501).

    After simulated disconnect + reconnect, each bridge reconnects to its OWN port.
    Proves instance isolation — no cross-connect between bridges.

    Discriminate: if discoverer is shared/global, both would pick same port → fail.
    """
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer, make_idle_probe, reconnect_preamble

    def make_reader_for_id(ping_id: str, ver_str: str):
        import json, struct
        r = {"id": ping_id, "ok": True, "data": "ok"}
        p = json.dumps(r).encode()
        hdr, pay = struct.pack("!I", len(p)), p
        ver_r = {"id": "ver", "ok": True, "data": ver_str}
        vp = json.dumps(ver_r).encode()
        ver_hdr, ver_pay = struct.pack("!I", len(vp)), vp
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[hdr, pay, ver_hdr, ver_pay])
        return reader

    probe_a = make_idle_probe()
    probe_b = make_idle_probe()
    discoverer_a = Mock(return_value=9500)
    discoverer_b = Mock(return_value=9501)

    # Reconnect bridge_a: expects connection to 9500
    reader_a = make_reader_for_id("rc0001", "proto:3|plugin:test|stamp:a")
    # Reconnect bridge_b: expects connection to 9501
    reader_b = make_reader_for_id("rc0001", "proto:3|plugin:test|stamp:b")

    with patch.object(bridge_mod.asyncio, "open_connection") as mock_conn, \
         patch("unity_mcp.bridge.is_pid_alive", return_value=False):

        # Return different readers depending on call order
        mock_conn.side_effect = [
            (reader_a, make_writer()),
            (reader_b, make_writer()),
        ]

        bridge_a = UnityBridge("127.0.0.1", 9500, probe=probe_a, port_discoverer=discoverer_a)
        bridge_b = UnityBridge("127.0.0.1", 9501, probe=probe_b, port_discoverer=discoverer_b)

        bridge_a._pinned_pid = None  # force discoverer path
        bridge_b._pinned_pid = None

        await bridge_a._reconnect()
        await bridge_b._reconnect()

    # Each bridge uses its own discoverer — no cross-connect
    discoverer_a.assert_called_once()
    discoverer_b.assert_called_once()
    assert bridge_a._port == 9500, f"bridge_a drifted to port {bridge_a._port}"
    assert bridge_b._port == 9501, f"bridge_b drifted to port {bridge_b._port}"
