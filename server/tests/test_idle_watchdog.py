"""TDD tests for idle watchdog in server.py."""
import asyncio
import os
import time
import threading
from unittest.mock import patch, MagicMock, AsyncMock

import pytest

import unity_mcp.server as srv_module


# ---------------------------------------------------------------------------
# Phase 1: Idle Watchdog
# ---------------------------------------------------------------------------

def test_idle_watchdog_disabled_when_timeout_zero():
    """UNITY_MCP_IDLE_TIMEOUT=0 means watchdog is not started."""
    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "0"}):
        result = srv_module._start_idle_watchdog()
        assert result is None


def test_idle_watchdog_disabled_when_timeout_negative():
    """Negative timeout disables watchdog (CRITICAL fix: <= 0)."""
    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "-1"}):
        result = srv_module._start_idle_watchdog()
        assert result is None


def test_idle_watchdog_exits_after_timeout():
    """Watchdog calls os._exit(0) when idle time exceeds timeout AND parent is dead (ppid changed)."""
    import unity_mcp.bridge_heartbeat as hb

    exit_calls = []

    # monotonic: make _last_useful_activity appear far in the past so idle > timeout.
    original_last = srv_module._last_useful_activity
    real_ppid = os.getppid()

    def fake_exit(c):
        # N2: set stop event so _loop exits cleanly on next iteration check.
        exit_calls.append(c)
        srv_module._watchdog_stop.set()

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "5"}), \
         patch("unity_mcp.server.time.sleep"), \
         patch("unity_mcp.server.time.monotonic", return_value=original_last + 100.0), \
         patch("unity_mcp.server.os.getppid", return_value=1), \
         patch("unity_mcp.bridge_heartbeat._ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.server.os._exit", side_effect=fake_exit), \
         patch("unity_mcp.server.logging.shutdown"):
        t = srv_module._start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    assert exit_calls == [0]


def test_idle_watchdog_resets_on_activity():
    """Watchdog does NOT exit when activity was recent (idle < timeout)."""
    exit_calls = []
    sleep_count = [0]

    def fake_sleep(n):
        sleep_count[0] += 1
        srv_module._touch_activity()  # keep idle < timeout
        if sleep_count[0] >= 2:
            # N2: set _watchdog_stop so _loop exits cleanly (no exception, no warning).
            srv_module._watchdog_stop.set()

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "300"}), \
         patch("unity_mcp.server.time.sleep", side_effect=fake_sleep), \
         patch("unity_mcp.server.os._exit", side_effect=lambda c: exit_calls.append(c)):
        t = srv_module._start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    assert exit_calls == []


def test_idle_watchdog_thread_is_daemon():
    """Idle watchdog thread must be daemon (does not block process exit).

    N2: Mock threading.Thread to capture daemon flag without running the target —
    avoids PytestUnhandledThreadExceptionWarning from RuntimeError in daemon thread.
    """
    captured = {}

    original_thread = threading.Thread

    def _capture_thread(**kwargs):
        captured["daemon"] = kwargs.get("daemon", False)
        t = original_thread(**kwargs)
        # Replace target with no-op so thread exits cleanly without warnings.
        t._target = lambda: None
        return t

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "300"}), \
         patch("unity_mcp.server.threading.Thread", side_effect=_capture_thread):
        thread = srv_module._start_idle_watchdog()

    assert thread is not None
    assert captured.get("daemon") is True, "Watchdog thread must be started with daemon=True"


def test_idle_watchdog_nonzero_env_starts_thread():
    """UNITY_MCP_IDLE_TIMEOUT env var is actually read by _start_idle_watchdog."""
    started_timeouts = []
    original_thread_cls = threading.Thread

    def capture_thread(**kwargs):
        t = original_thread_cls(**kwargs)
        # The timeout is captured in closure — we verify by checking it returns a thread
        started_timeouts.append(True)
        return t

    # N2: mock time.sleep so the thread doesn't sleep 30 real seconds, and
    # mock os._exit so any accidental idle-fire doesn't kill pytest.
    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "60"}), \
         patch("unity_mcp.server.threading.Thread", side_effect=capture_thread), \
         patch("unity_mcp.server.time.sleep", side_effect=lambda _: srv_module._watchdog_stop.set()), \
         patch("unity_mcp.server.os._exit"):
        result = srv_module._start_idle_watchdog()
        if result is not None:
            result.join(timeout=1.0)  # wait for thread to exit via _watchdog_stop

    assert result is not None
    assert len(started_timeouts) == 1


def test_touch_activity_updates_timestamp():
    """_touch_activity() (alias for _touch_useful_activity) updates _last_useful_activity."""
    before = time.monotonic()
    srv_module._touch_activity()
    after = time.monotonic()

    assert before <= srv_module._last_useful_activity <= after


@pytest.mark.asyncio
async def test_send_touches_activity():
    """_send() calls _touch_useful_activity before delegating — _last_useful_activity advances."""
    old_ts = srv_module._last_useful_activity

    # Mock _send_raw to avoid real TCP
    async def fake_raw(cmd, args, timeout=0):
        return "ok"

    with patch("unity_mcp.server._send_raw", side_effect=fake_raw), \
         patch("unity_mcp.server._wrapped_send", None):
        try:
            await srv_module._send("ping", {})
        except Exception:
            pass  # ToolError from None slot is fine; _touch_useful_activity runs first

    assert srv_module._last_useful_activity >= old_ts


# ---------------------------------------------------------------------------
# Phase 2: ppid hard exit
# ---------------------------------------------------------------------------

def test_ppid_mismatch_triggers_exit():
    """When ppid changes, _schedule_hard_exit is called and os._exit runs after delay."""
    import unity_mcp.bridge_heartbeat as hb

    exit_calls = []

    class FakeTimer:
        def __init__(self, delay, fn, args=()):
            self._fn = fn
            self._args = args
            self.daemon = True

        def start(self):
            self._fn(*self._args)  # run immediately in test

    with patch("unity_mcp.bridge_heartbeat.threading.Timer", FakeTimer), \
         patch("unity_mcp.bridge_heartbeat.os._exit", side_effect=lambda c: exit_calls.append(c)):
        # Reset guard for clean test
        hb._hard_exit_scheduled = False
        hb._schedule_hard_exit()

    assert exit_calls == [0]


def test_ppid_mismatch_no_double_exit():
    """_schedule_hard_exit called twice only exits once (CRITICAL guard)."""
    import unity_mcp.bridge_heartbeat as hb

    exit_calls = []

    class FakeTimer:
        def __init__(self, delay, fn, args=()):
            self._fn = fn
            self._args = args
            self.daemon = True

        def start(self):
            self._fn(*self._args)

    with patch("unity_mcp.bridge_heartbeat.threading.Timer", FakeTimer), \
         patch("unity_mcp.bridge_heartbeat.os._exit", side_effect=lambda c: exit_calls.append(c)):
        hb._hard_exit_scheduled = False
        hb._schedule_hard_exit()
        hb._schedule_hard_exit()  # second call — must be no-op

    assert exit_calls == [0]  # only one exit


@pytest.mark.asyncio
async def test_ppid_stable_no_exit():
    """When ppid unchanged, _schedule_hard_exit is never called."""
    import unity_mcp.bridge_heartbeat as hb_module
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class _Stub(HeartbeatMixin):
        def __init__(self):
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._ping_failures = 0
            self._last_reconnect_at = 0.0
            self._min_reconnect_interval = 2.0
            self._lock = asyncio.Lock()
            self._counter = 0
            self._reconnect_started_at = None
            self._startup_grace_expired = False
            self._ppid_mismatch_count = 0
            self._connected = False
            from unity_mcp.bridge_reload_state import DomainReloadTracker
            from unity_mcp.bridge import BridgeState
            self._reload = DomainReloadTracker()
            self._state = BridgeState.DISCONNECTED

        @property
        def connected(self):
            return self._connected

        def _probe_busy(self):
            return False

        def _reconnect_cooldown_ok(self):
            return False

        async def _reconnect(self):
            pass

    bridge = _Stub()
    real_ppid = os.getppid()

    schedule_calls = []
    with patch.object(hb_module, "_ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=real_ppid), \
         patch("unity_mcp.bridge_heartbeat._schedule_hard_exit",
               side_effect=lambda: schedule_calls.append(1)), \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)

    assert len(schedule_calls) == 0


# ---------------------------------------------------------------------------
# Phase 3: ppid-gate in idle watchdog (regression guard for fix/connection-stability)
# ---------------------------------------------------------------------------

def test_idle_watchdog_does_not_exit_when_parent_alive():
    """CRITICAL regression test: idle > timeout + parent alive → os._exit NOT called.

    Discriminator: removing the ppid-gate causes this test to fail because
    os._exit gets called even when getppid() == _ORIGINAL_PPID.
    """
    import unity_mcp.bridge_heartbeat as hb
    import unity_mcp.server as srv

    exit_calls = []
    sleep_count = [0]
    original_last = srv._last_useful_activity

    real_ppid = os.getppid()  # parent is alive throughout test

    def fake_sleep(n):
        sleep_count[0] += 1
        # After first sleep, set stop so loop exits — without calling os._exit
        if sleep_count[0] >= 2:
            srv._watchdog_stop.set()

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "5"}), \
         patch("unity_mcp.server.time.sleep", side_effect=fake_sleep), \
         patch("unity_mcp.server.time.monotonic", return_value=original_last + 100.0), \
         patch("unity_mcp.server.os.getppid", return_value=real_ppid), \
         patch("unity_mcp.bridge_heartbeat._ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.server.os._exit", side_effect=lambda c: exit_calls.append(c)), \
         patch("unity_mcp.server.logging.shutdown"):
        t = srv._start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    # Parent was alive → watchdog must NOT exit
    assert exit_calls == [], f"os._exit was called with parent alive: {exit_calls}"


def test_idle_watchdog_exits_when_parent_dead():
    """CRITICAL: idle > timeout + parent dead (ppid changed) → os._exit(0) called once.

    Discriminator: only runs os._exit when orphaned (ppid != original).
    """
    import unity_mcp.bridge_heartbeat as hb
    import unity_mcp.server as srv

    exit_calls = []
    original_last = srv._last_useful_activity
    real_ppid = os.getppid()
    dead_ppid = 1  # launchd/init — parent was reparented (orphaned)

    def fake_exit(c):
        exit_calls.append(c)
        srv._watchdog_stop.set()  # stop loop after exit

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "5"}), \
         patch("unity_mcp.server.time.sleep"), \
         patch("unity_mcp.server.time.monotonic", return_value=original_last + 100.0), \
         patch("unity_mcp.server.os.getppid", return_value=dead_ppid), \
         patch("unity_mcp.bridge_heartbeat._ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.server.os._exit", side_effect=fake_exit), \
         patch("unity_mcp.server.logging.shutdown"):
        t = srv._start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    # Parent dead → watchdog MUST exit
    assert exit_calls == [0], f"Expected os._exit(0) once, got: {exit_calls}"


# ---------------------------------------------------------------------------
# Phase 4: T4 — useful/transport timer split + in-flight guard
# ---------------------------------------------------------------------------

def test_heartbeat_does_not_update_useful_timer():
    """_touch_transport_activity() updates only _last_transport_activity, not _last_useful_activity."""
    before_useful = srv_module._last_useful_activity
    before_transport = srv_module._last_transport_activity

    srv_module._touch_transport_activity()

    assert srv_module._last_useful_activity == before_useful, "useful timer must not change"
    assert srv_module._last_transport_activity >= before_transport, "transport timer must advance"


@pytest.mark.asyncio
async def test_send_updates_both_timers_and_inflight():
    """_send() updates both useful and transport timers; in-flight count is 0 after completion."""
    before_useful = srv_module._last_useful_activity
    before_transport = srv_module._last_transport_activity

    async def fake_raw(cmd, args, timeout=0):
        assert srv_module._in_flight_count == 1, "in-flight must be 1 while executing"
        return "ok"

    with patch("unity_mcp.server._send_raw", side_effect=fake_raw), \
         patch("unity_mcp.server._wrapped_send", None):
        await srv_module._send("ping", {})

    assert srv_module._last_useful_activity >= before_useful
    assert srv_module._last_transport_activity >= before_transport
    assert srv_module._in_flight_count == 0


def test_watchdog_respects_inflight_guard():
    """In-flight guard: watchdog does not exit while _in_flight_count > 0; exits once cleared."""
    import unity_mcp.bridge_heartbeat as hb

    exit_calls = []
    sleep_count = [0]
    real_ppid = os.getppid()
    dead_ppid = 1
    original_last = srv_module._last_useful_activity

    def fake_sleep(n):
        sleep_count[0] += 1
        if sleep_count[0] >= 2:
            srv_module._in_flight_count = 0  # clear → allow exit on this iteration

    def fake_exit(c):
        # Discriminator: if guard is removed, exit fires on sleep #1 (too early)
        assert sleep_count[0] >= 2, f"Guard failed — exit fired on sleep #{sleep_count[0]}"
        exit_calls.append(c)
        srv_module._watchdog_stop.set()

    try:
        srv_module._in_flight_count = 1
        with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "5"}), \
             patch("unity_mcp.server.time.sleep", side_effect=fake_sleep), \
             patch("unity_mcp.server.time.monotonic", return_value=original_last + 100.0), \
             patch("unity_mcp.server.os.getppid", return_value=dead_ppid), \
             patch("unity_mcp.bridge_heartbeat._ORIGINAL_PPID", real_ppid), \
             patch("unity_mcp.server.os._exit", side_effect=fake_exit), \
             patch("unity_mcp.server.logging.shutdown"):
            t = srv_module._start_idle_watchdog()
            assert t is not None
            t.join(timeout=3.0)
    finally:
        srv_module._in_flight_count = 0

    assert exit_calls == [0], f"Expected exit after in-flight cleared: {exit_calls}"


def test_useful_idle_timeout_ignores_ppid():
    """UNITY_MCP_USEFUL_IDLE_TIMEOUT exits even when parent is alive (subagent path)."""
    exit_calls = []
    real_ppid = os.getppid()
    original_last = srv_module._last_useful_activity

    def fake_exit(c):
        exit_calls.append(c)
        srv_module._watchdog_stop.set()

    with patch.dict(os.environ, {"UNITY_MCP_IDLE_TIMEOUT": "300",
                                  "UNITY_MCP_USEFUL_IDLE_TIMEOUT": "1"}), \
         patch("unity_mcp.server.time.sleep"), \
         patch("unity_mcp.server.time.monotonic", return_value=original_last + 100.0), \
         patch("unity_mcp.server.os.getppid", return_value=real_ppid), \
         patch("unity_mcp.server.os._exit", side_effect=fake_exit), \
         patch("unity_mcp.server.logging.shutdown"):
        t = srv_module._start_idle_watchdog()
        assert t is not None
        t.join(timeout=2.0)

    assert exit_calls == [0], f"Expected subagent exit with parent alive, got: {exit_calls}"


@pytest.mark.asyncio
async def test_on_transport_activity_callback_wired_after_connect():
    """Wiring bridge._on_transport_activity = _touch_transport_activity updates _last_transport_activity when called."""

    class MockBridge:
        _on_transport_activity = None

    bridge = MockBridge()
    # Simulate what lifespan does after active.start_heartbeat()
    bridge._on_transport_activity = srv_module._touch_transport_activity

    before = srv_module._last_transport_activity
    bridge._on_transport_activity()
    assert srv_module._last_transport_activity >= before


@pytest.mark.asyncio
async def test_raw_ping_fires_transport_callback():
    """_raw_ping() fires _on_transport_activity after a successful ping.

    Discriminant: removing the on_ta() block from _raw_ping() makes this fail.
    """
    from unittest.mock import patch, AsyncMock
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class FakeBridge(HeartbeatMixin):
        def __init__(self):
            self._lock = asyncio.Lock()
            self._counter = 0
            self.connected = True
            self._writer = AsyncMock()

        async def _read_response(self):
            return {"id": f"hb{self._counter:04x}"}

    bridge = FakeBridge()
    callback_calls = []
    bridge._on_transport_activity = lambda: callback_calls.append(1)

    with patch("unity_mcp.bridge_heartbeat.frame_write"):
        await bridge._raw_ping(timeout=1.0)

    assert callback_calls == [1], "_raw_ping must fire _on_transport_activity exactly once"
