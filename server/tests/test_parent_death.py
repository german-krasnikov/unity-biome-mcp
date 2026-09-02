"""TDD tests for parent-death detection in bridge_heartbeat.py."""
import asyncio
import contextlib
import os
import threading
import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import unity_mcp.bridge_heartbeat as hb_module


@pytest.fixture(autouse=True)
def _reset_hard_exit():
    """Reset _hard_exit_scheduled so _schedule_hard_exit fires once per test."""
    hb_module._hard_exit_scheduled = False
    yield
    hb_module._hard_exit_scheduled = False


def test_original_ppid_is_module_level_int():
    assert isinstance(hb_module._ORIGINAL_PPID, int)
    assert hb_module._ORIGINAL_PPID > 0


def test_original_ppid_matches_real_ppid_at_import():
    assert os.getppid() == hb_module._ORIGINAL_PPID


def _make_bridge_mixin():
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
            self._orphan_detected_at = None
            self._connected = True
            from unity_mcp.bridge import BridgeState
            from unity_mcp.bridge_reload_state import DomainReloadTracker
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

    return _Stub()


def _cfg_no_terminate():
    cfg = MagicMock()
    cfg.effective_terminate_orphan.return_value = (True, "config")
    cfg.effective_orphan_grace_s.return_value = (0, "config")  # grace=0 → instant
    return cfg


def _cfg_long_grace():
    cfg = MagicMock()
    cfg.effective_terminate_orphan.return_value = (True, "config")
    cfg.effective_orphan_grace_s.return_value = (120, "config")
    return cfg


@pytest.mark.asyncio
async def test_parent_death_stops_heartbeat_no_systemexit():
    """PPID mismatch + grace expired → heartbeat stops, no SystemExit/os._exit."""
    bridge = _make_bridge_mixin()
    # Pre-set orphan_detected_at so grace (0s) is already expired.
    bridge._orphan_detected_at = time.monotonic() - 10

    fake_original = os.getppid() + 9999
    with patch.object(hb_module, "_ORIGINAL_PPID", fake_original), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_no_terminate()), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"), \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)
        assert bridge._heartbeat_task is None  # stopped


@pytest.mark.asyncio
async def test_single_ppid_mismatch_does_not_stop():
    """Single mismatch within grace — heartbeat keeps running."""
    bridge = _make_bridge_mixin()
    bridge._heartbeat_task = asyncio.create_task(asyncio.sleep(999))

    fake_original = os.getppid() + 9999
    with patch.object(hb_module, "_ORIGINAL_PPID", fake_original), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
         patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_long_grace()), \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)
        assert bridge._ppid_mismatch_count == 1
        assert bridge._heartbeat_task is not None  # still running

    bridge._heartbeat_task.cancel()
    with contextlib.suppress(asyncio.CancelledError):
        await bridge._heartbeat_task


@pytest.mark.asyncio
async def test_ppid_match_resets_counter():
    """When ppid matches again, mismatch counter and _orphan_detected_at reset."""
    bridge = _make_bridge_mixin()
    bridge._ppid_mismatch_count = 1
    bridge._orphan_detected_at = time.monotonic() - 5
    bridge._connected = False

    real_ppid = os.getppid()
    with patch.object(hb_module, "_ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=real_ppid), \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)
        assert bridge._ppid_mismatch_count == 0
        assert bridge._orphan_detected_at is None


@pytest.mark.asyncio
async def test_parent_alive_no_exit():
    """When ppid unchanged, no exit called."""
    bridge = _make_bridge_mixin()
    bridge._connected = False

    real_ppid = os.getppid()
    with patch.object(hb_module, "_ORIGINAL_PPID", real_ppid), \
         patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=real_ppid), \
         patch("unity_mcp.bridge_heartbeat.os._exit") as mock_exit, \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)
        mock_exit.assert_not_called()


# ---------------------------------------------------------------------------
# DEV-54 [B3-#13] — orphan path must yield to the event loop
# ---------------------------------------------------------------------------


def _cfg_permanent_orphan():
    """Permanent-bridge-mode config: _check_orphan() returns True forever,
    with no grace/hard-exit bookkeeping (terminate_orphan=False short-circuits
    before the grace deadline check)."""
    cfg = MagicMock()
    cfg.effective_terminate_orphan.return_value = (False, "config")
    cfg.effective_orphan_grace_s.return_value = (999, "config")
    return cfg


def test_heartbeat_orphan_yields_event_loop():
    """_heartbeat_loop's orphan branch must suspend, not busy-loop.

    Bug: _check_orphan() is synchronous; when it returns True (parent dead,
    terminate_orphan=False), _heartbeat_tick used to return with no await on
    that path, so `while True: await self._heartbeat_tick(...)` in
    _heartbeat_loop never hit a suspension point — a pure CPU busy-loop that
    starves every other coroutine on the same event loop forever (including
    in-flight MCP request handling in production).

    Runs the loop in its own thread + event loop with a bounded join(): a
    regression makes the thread hang forever (as the real bug does — nothing
    in the buggy code path ever yields, so no asyncio-level timeout can fire
    either), and join(timeout=...) still returns on schedule so this test
    fails fast instead of hanging the whole suite.
    """
    bridge = _make_bridge_mixin()
    fake_original = os.getppid() + 9999
    counter = {"n": 0}

    async def _sibling() -> None:
        while True:
            counter["n"] += 1
            await asyncio.sleep(0)

    async def _run() -> None:
        loop_task = asyncio.ensure_future(bridge._heartbeat_loop(15.0))
        sibling_task = asyncio.ensure_future(_sibling())
        await asyncio.sleep(0.2)
        loop_task.cancel()
        sibling_task.cancel()
        with contextlib.suppress(asyncio.CancelledError):
            await loop_task
        with contextlib.suppress(asyncio.CancelledError):
            await sibling_task

    def _thread_main() -> None:
        with patch.object(hb_module, "_ORIGINAL_PPID", fake_original), \
             patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=os.getppid()), \
             patch("unity_mcp.global_config.GlobalConfig.load", return_value=_cfg_permanent_orphan()):
            loop = asyncio.new_event_loop()
            try:
                loop.run_until_complete(_run())
            finally:
                loop.close()

    t = threading.Thread(target=_thread_main, daemon=True)
    t.start()
    t.join(timeout=1.0)

    assert counter["n"] > 0, (
        "sibling task never ran — orphan path busy-loops the event loop"
    )
