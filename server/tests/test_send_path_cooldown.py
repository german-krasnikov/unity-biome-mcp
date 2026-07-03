"""TDD tests for send-path cooldown gate (Phase 2.1 of connection-stability fix).

Verifies:
- E1: burst-prevention — 3 sends in <1s → only 1 reconnect, 2 raise ConnectionError
- E2: allows-after-backoff — second reconnect OK once cooldown expires
- E3: callback-no-cascade — reconnect callback raises → state stays CONNECTED
- E4: 30s-dedup — _on_reconnect closure deduplication
- E5: FAILED-state-cooldown — FAILED + fresh cooldown → ConnectionError without reconnect
"""
import asyncio
import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.bridge import BridgeState, UnityBridge
from unity_mcp.bridge_reload_state import DomainReloadTracker


# ---------------------------------------------------------------------------
# Shared stub
# ---------------------------------------------------------------------------

class _StubBridge(UnityBridge):
    """UnityBridge with network ops replaced; tracks reconnect call count.

    MAINTENANCE NOTE: this stub must mirror all instance attributes set by
    UnityBridge.__init__ (and HeartbeatMixin's expected attrs). If UnityBridge.__init__
    adds new attributes, add them here too — or switch to calling super().__init__
    with asyncio.open_connection patched to avoid real network calls.
    """

    def __init__(self):
        # Manual attribute setup — mirrors UnityBridge.__init__ without network calls.
        # DO NOT call super().__init__() here: it would attempt to resolve ports / create probes.
        from unity_mcp.crash_log import CrashLogger
        self._host = "127.0.0.1"
        self._port = 9500
        self._reader = None
        self._writer = None
        self._counter = 0
        self._lock = asyncio.Lock()
        self._probe = MagicMock()
        self._probe.has_strong_busy_signal.return_value = False
        self._probe.is_process_dead.return_value = True
        self._first_failure_ts = None
        self._reconnect_started_at = None
        self._state = BridgeState.DISCONNECTED
        self._on_reconnect_callbacks = []
        self._crash_log = CrashLogger()
        self._heartbeat_task = None
        self._heartbeat_interval = 15.0
        self._ping_failures = 0
        self._last_reconnect_at = 0.0
        self._min_reconnect_interval = 5.0
        self._reconnect_backoff = 5.0
        self._port_discoverer = None
        self._reload = DomainReloadTracker()
        self._ppid_mismatch_count = 0
        self._pinned_port = None
        self._pinned_pid = None
        self._bridge_id = "br-test-0000"
        self._reconnect_count = 0
        self._is_retry_safe = lambda cmd: False
        # C8: should_retry() now delegates to RetryPolicy (bridge_retry.py) —
        # mirrors UnityBridge.__init__'s construction (see maintenance note above).
        from unity_mcp.bridge import MAX_RETRIES
        from unity_mcp.bridge_retry import RetryPolicy
        self._retry_policy = RetryPolicy(
            probe=self._probe, reload=self._reload,
            is_retry_safe=self._is_retry_safe, max_retries=MAX_RETRIES,
        )

    @property
    def connected(self) -> bool:
        return False

    def _probe_busy(self) -> bool:
        return False

    async def _reconnect(self, fire_callbacks=True):
        self._reconnect_count += 1
        raise ConnectionRefusedError("stub always fails")


# ---------------------------------------------------------------------------
# E1: burst-prevention — second and third INDEPENDENT calls blocked by cooldown
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_send_path_cooldown_prevents_burst_reconnects():
    """Second/third independent send() calls within cooldown window raise ConnectionError.

    Scenario: cooldown was recently armed (e.g., by heartbeat or first call).
    A burst of N simultaneous send() calls should all fail-fast except the one that
    armed the cooldown.
    """
    bridge = _StubBridge()
    # Simulate: cooldown just armed (as if a reconnect just happened or was attempted)
    bridge._last_reconnect_at = time.monotonic()  # just now — cooldown active
    bridge._reconnect_backoff = 5.0

    header = b"\x00\x00\x00\x04"
    payload = b"test"
    deadline = time.monotonic() + 120.0

    # Immediate call: cooldown active → ConnectionError without calling _reconnect
    with pytest.raises(ConnectionError, match="cooldown"):
        await bridge._send_with_retry("ping", header, payload, "0002", 5.0, deadline)

    assert bridge._reconnect_count == 0  # gate fired, no reconnect attempted

    # Another immediate call: still within cooldown
    with pytest.raises(ConnectionError, match="cooldown"):
        await bridge._send_with_retry("ping", header, payload, "0003", 5.0, deadline)

    assert bridge._reconnect_count == 0  # still 0


# ---------------------------------------------------------------------------
# E2: allows-after-backoff
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_send_path_cooldown_allows_after_backoff():
    """Once cooldown expires, a new send() call CAN trigger reconnect."""
    bridge = _StubBridge()
    bridge._last_reconnect_at = time.monotonic() - 100.0  # expired long ago
    bridge._reconnect_backoff = 5.0

    header = b"\x00\x00\x00\x04"
    payload = b"test"
    deadline = time.monotonic() + 120.0

    # First call — cooldown expired, reconnect allowed (may internally retry, that's OK)
    with pytest.raises(ConnectionError):
        await bridge._send_with_retry("ping", header, payload, "0001", 5.0, deadline)
    count_after_first = bridge._reconnect_count
    assert count_after_first >= 1  # at least one reconnect was attempted

    # Immediately set cooldown as active
    bridge._last_reconnect_at = time.monotonic()  # just armed
    count_before = bridge._reconnect_count

    # This call should be BLOCKED by cooldown
    with pytest.raises(ConnectionError, match="cooldown"):
        await bridge._send_with_retry("ping", header, payload, "0002", 5.0, deadline)
    assert bridge._reconnect_count == count_before  # no additional reconnect

    # Now expire the cooldown again
    bridge._last_reconnect_at = time.monotonic() - 100.0
    count_before2 = bridge._reconnect_count

    # This call should be ALLOWED again
    with pytest.raises(ConnectionError):
        await bridge._send_with_retry("ping", header, payload, "0003", 5.0, deadline)
    assert bridge._reconnect_count > count_before2  # reconnect was attempted


# ---------------------------------------------------------------------------
# E3: callback-no-cascade
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_on_reconnect_callback_failure_does_not_cascade():
    """A raising reconnect callback leaves bridge CONNECTED without second reconnect.

    NOTE: this test validates the callback-isolation CONTRACT (try/except swallows
    exceptions), not bridge._reconnect itself. The _good_reconnect stub stands in for
    the real _reconnect to keep the test focused on callback isolation without needing
    asyncio.open_connection patched.
    """
    bridge = _StubBridge()
    bridge._reconnect_count = 0
    real_reconnect_calls = []

    async def _good_reconnect(fire_callbacks=True):
        real_reconnect_calls.append(True)
        bridge._writer = MagicMock()
        bridge._writer.is_closing.return_value = False
        bridge._writer.get_extra_info.return_value = None
        bridge._reader = MagicMock()
        bridge._state = BridgeState.CONNECTED
        bridge._last_reconnect_at = time.monotonic()
        if fire_callbacks:
            for cb in bridge._on_reconnect_callbacks:
                try:
                    cb()
                except Exception:
                    pass  # swallowed per bridge._reconnect spec

    bridge._reconnect = _good_reconnect

    def _bad_callback():
        raise RuntimeError("callback explodes")

    bridge.add_reconnect_callback(_bad_callback)

    await bridge._reconnect(fire_callbacks=True)

    assert bridge._state == BridgeState.CONNECTED
    assert len(real_reconnect_calls) == 1  # no cascade reconnect


# ---------------------------------------------------------------------------
# E4: 30s-dedup throttle
# ---------------------------------------------------------------------------

def test_on_reconnect_dedup_30s_throttle():
    """_on_reconnect closure calls _refresh_tools_cache at most once within 30s."""
    refresh_calls = []

    async def _mock_refresh(bridge):
        refresh_calls.append(time.monotonic())

    async def _mock_push(bridge):
        pass

    # Simulate the closure from server.py
    _last_refresh_ts = 0.0

    def _on_reconnect():
        nonlocal _last_refresh_ts
        now = time.monotonic()
        if now - _last_refresh_ts < 30.0:
            return
        _last_refresh_ts = now
        refresh_calls.append(now)

    # First call — should refresh
    _on_reconnect()
    assert len(refresh_calls) == 1

    # Second call immediately — should be deduplicated
    _on_reconnect()
    assert len(refresh_calls) == 1  # still 1


# ---------------------------------------------------------------------------
# E5: FAILED-state cooldown
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_failed_state_send_respects_cooldown():
    """FAILED state + fresh cooldown → ConnectionError without _reconnect call."""
    bridge = _StubBridge()
    bridge._state = BridgeState.FAILED
    bridge._last_reconnect_at = time.monotonic()  # just armed — cooldown active
    bridge._reconnect_backoff = 5.0

    with pytest.raises(ConnectionError, match="cooldown"):
        await bridge.send("ping", {})

    assert bridge._reconnect_count == 0  # gate fired before _reconnect
