"""Tests for HeartbeatMixin — P3: PPID mismatch / orphan grace period."""
import asyncio
import contextlib
import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import unity_mcp.bridge_heartbeat as hb_module
from unity_mcp.bridge_heartbeat import _ORIGINAL_PPID, BACKOFF_MIN_S, HeartbeatMixin


@pytest.fixture(autouse=True)
def _reset_hard_exit():
    """Reset _hard_exit_scheduled so each test starts clean."""
    hb_module._hard_exit_scheduled = False
    yield
    hb_module._hard_exit_scheduled = False


def _cfg_mock(terminate: bool = True, grace_s: int = 120) -> MagicMock:
    """Return a mock GlobalConfig with controlled effective_* results."""
    cfg = MagicMock()
    cfg.effective_terminate_orphan.return_value = (terminate, "config")
    cfg.effective_orphan_grace_s.return_value = (grace_s, "config")
    return cfg


class _FakeBridge(HeartbeatMixin):
    """Minimal concrete subclass for testing heartbeat tick logic."""

    def __init__(self):
        self._heartbeat_task = None
        self._heartbeat_interval = 15.0
        self._ping_failures = 0
        self._last_reconnect_at = 0.0
        self._min_reconnect_interval = 0.0
        self._reconnect_backoff = BACKOFF_MIN_S
        self._lock = asyncio.Lock()
        self._probe = MagicMock()
        self._probe.has_strong_busy_signal.return_value = False
        self._probe.is_process_dead.return_value = False
        self._probe.mark_recompile_issued = MagicMock()
        self._writer = None
        self._reader = None
        self._counter = 0
        self._ping_stall_failures = 0
        self._reconnect_started_at = None
        self._startup_grace_expired = False
        self._ppid_mismatch_count = 0
        self._orphan_detected_at = None
        from unity_mcp.bridge import BridgeState
        from unity_mcp.bridge_reload_state import DomainReloadTracker
        self._reload = DomainReloadTracker()
        self._state = BridgeState.DISCONNECTED

    @property
    def connected(self):
        return self._writer is not None

    def _probe_busy(self):
        return False

    async def _reconnect(self, fire_callbacks=True):
        pass

    async def _read_response(self):
        return {"id": "ping", "ok": True, "data": "pong"}


def test_fake_bridge_has_ping_stall_failures():
    """_FakeBridge must expose _ping_stall_failures like the real bridge."""
    bridge = _FakeBridge()
    assert bridge._ping_stall_failures == 0


# P3 tests ──────────────────────────────────────────────────────────────────

async def test_p3_single_ppid_mismatch_does_not_exit():
    """Single PPID mismatch within grace period → sets _orphan_detected_at, no exit."""
    bridge = _FakeBridge()
    schedule_calls = []

    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_mock(grace_s=120)), \
         patch("unity_mcp.bridge_heartbeat._schedule_hard_exit",
               side_effect=lambda: schedule_calls.append(1)), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
        await bridge._heartbeat_tick(15.0)

    assert bridge._ppid_mismatch_count == 1
    assert bridge._orphan_detected_at is not None
    assert len(schedule_calls) == 0  # grace not expired


async def test_p3_grace_expired_stops_heartbeat():
    """PPID mismatch + grace already expired → _schedule_hard_exit and stop heartbeat."""
    bridge = _FakeBridge()
    bridge._orphan_detected_at = time.monotonic() - 200  # detected 200s ago

    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_mock(grace_s=0)), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
        await bridge._heartbeat_tick(15.0)

    assert bridge._heartbeat_task is None  # stop_heartbeat() was called


async def test_p3_ppid_recovery_resets_orphan():
    """PPID mismatch then match → both _ppid_mismatch_count and _orphan_detected_at reset."""
    bridge = _FakeBridge()

    # First: mismatch
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_mock(grace_s=120)), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
        await bridge._heartbeat_tick(15.0)
    assert bridge._ppid_mismatch_count == 1
    assert bridge._orphan_detected_at is not None

    # Second: PPID matches → resets; reconnect path may raise — only check reset
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()), \
         contextlib.suppress(Exception):
        await bridge._heartbeat_tick(15.0)
    assert bridge._ppid_mismatch_count == 0
    assert bridge._orphan_detected_at is None


async def test_p3_no_os_exit_used():
    """_schedule_hard_exit (not os._exit) is called when grace expires."""
    bridge = _FakeBridge()
    bridge._orphan_detected_at = time.monotonic() - 200  # already expired
    schedule_calls = []

    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_mock(grace_s=0)), \
         patch("unity_mcp.bridge_heartbeat._schedule_hard_exit",
               side_effect=lambda: schedule_calls.append(1)), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
        await bridge._heartbeat_tick(15.0)

    assert len(schedule_calls) == 1


async def test_p3_terminate_orphan_false_never_exits():
    """bridge_terminate_orphan=False → permanent bridge mode, never schedules exit."""
    bridge = _FakeBridge()
    bridge._orphan_detected_at = time.monotonic() - 9999  # way past any grace
    schedule_calls = []

    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_mock(terminate=False)), \
         patch("unity_mcp.bridge_heartbeat._schedule_hard_exit",
               side_effect=lambda: schedule_calls.append(1)), \
         patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
        await bridge._heartbeat_tick(15.0)

    assert len(schedule_calls) == 0
