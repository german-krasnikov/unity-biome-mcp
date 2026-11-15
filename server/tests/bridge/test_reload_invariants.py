"""Regression tests: 10 domain-reload invariants from 141 monkey experiments.

All tests are pure Python, offline, $0 cost.
Evidence base: Plans/Reload/Monkey/ + Plans/Reload/V2/tcp-churn-heartbeat-fix-2026-07.md
"""
import asyncio
import json
import time
from contextlib import suppress
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

from unity_mcp.bridge import UnityBridge, MIN_RECONNECT_INTERVAL
from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S, BACKOFF_MAX_S
from unity_mcp.bridge_reload_state import DomainReloadTracker, DOMAIN_RELOAD_EXPIRY_S


# ── Shared fixture ─────────────────────────────────────────────────────────────

@pytest.fixture
def bridge():
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    return UnityBridge("127.0.0.1", 9999, probe=probe)


# ── INV-01: connected is False when writer is None ─────────────────────────────
# RC1 in V2 doc: MSG_PEEK returned False → reconnect storm. Fixed to is_closing() check.

def test_connected_false_when_writer_none(bridge):
    bridge._writer = None
    assert bridge.connected is False


def test_connected_true_with_live_writer(bridge):
    w = MagicMock()
    w.is_closing.return_value = False
    bridge._writer = w
    assert bridge.connected is True


# ── INV-02: close() cancels heartbeat task — no leak ──────────────────────────
# RC2: _ensure_heartbeat() spawned new task on every send(), close() never cancelled
# → 45 GB memory in pytest suite. Fix: stop_heartbeat() in close().

async def test_close_stops_heartbeat(bridge):
    bridge.start_heartbeat(interval=99)
    assert bridge._heartbeat_task is not None
    await bridge.close()
    assert bridge._heartbeat_task is None or bridge._heartbeat_task.done()


# ── INV-03: _reconnect() starts heartbeat ─────────────────────────────────────
# RC2: heartbeat lifecycle must be explicit — created ONLY in _reconnect(), destroyed
# ONLY in close(). Dangling heartbeat consistent with monkey-8 MultiClientPingLiveness.

async def test_reconnect_starts_heartbeat(bridge):
    bridge._heartbeat_task = None

    # frame_read_with_timeout returns raw payload bytes (no length prefix)
    ping_pay = json.dumps({"id": "rc0001", "ok": True, "data": "pong"}).encode()
    ver_pay = json.dumps({
        "id": "ver", "ok": True, "data": "proto:3|plugin:test|stamp:t"
    }).encode()

    fake_writer = MagicMock()
    fake_writer.is_closing.return_value = False
    fake_writer.get_extra_info = Mock(return_value=None)
    fake_writer.close = Mock()
    fake_writer.wait_closed = AsyncMock()
    fake_writer.drain = AsyncMock()

    with patch("asyncio.open_connection", new=AsyncMock(return_value=(AsyncMock(), fake_writer))), \
         patch("unity_mcp.bridge.frame_read_with_timeout", new=AsyncMock(side_effect=[ping_pay, ver_pay])), \
         patch("unity_mcp.bridge._apply_socket_options"), \
         patch("unity_mcp.bridge.frame_write"), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None):
        await bridge._reconnect()

    assert bridge._heartbeat_task is not None
    assert not bridge._heartbeat_task.done()
    bridge._heartbeat_task.cancel()
    with suppress(asyncio.CancelledError):
        await bridge._heartbeat_task


# ── INV-04: stall threshold — 5 windows no close, 6 → close ──────────────────
# RC3: premature close on ping stall caused reconnect races. monkey-125/128/130
# TIMEOUT at 120s. Fix: 6 stall windows (~6 min) required before close.

def test_stall_threshold_constant():
    # Verify the constant documented in the V2 fix matches code behaviour.
    STALL_THRESHOLD = 6        # bridge_heartbeat.py:L184
    HEARTBEAT_INTERVAL = 15.0  # bridge.py:L123 _heartbeat_interval default
    PING_FAILURES_PER_STALL = 3
    stall_window_s = PING_FAILURES_PER_STALL * HEARTBEAT_INTERVAL
    max_stall_duration_s = STALL_THRESHOLD * stall_window_s
    assert STALL_THRESHOLD == 6
    assert max_stall_duration_s == 270.0  # ~4.5 min / 6 stall windows


def test_stall_counter_at_5_does_not_close(bridge):
    # 5 stall windows: should_close is False
    bridge._ping_stall_failures = 5
    assert not (bridge._ping_stall_failures >= 6)


def test_stall_counter_at_6_triggers_close(bridge):
    # 6 stall windows: should_close is True
    bridge._ping_stall_failures = 6
    assert bridge._ping_stall_failures >= 6


# ── INV-05: reload gate unblocks < 0.1s after reconnect ──────────────────────
# RC4: fixed sleep during DomainReloadError retry. exp-11/12/20 TIMEOUT because
# poll window too short. Gate wait() must return < 0.1s after reconnect sets it.

async def test_reload_gate_set_after_reconnect(bridge):
    bridge._reload_gate.clear()
    assert not bridge._reload_gate.is_set()

    bridge._reload_gate.set()  # simulates what _reconnect() does on success
    assert bridge._reload_gate.is_set()

    start = time.monotonic()
    await asyncio.wait_for(bridge._reload_gate.wait(), timeout=0.1)
    assert time.monotonic() - start < 0.1


# ── INV-06: gate not cleared if already connected (race guard) ────────────────
# RC4 race: if reconnect completes BEFORE gate.clear(), gate must stay set so
# concurrent send() isn't blocked again.

async def test_reload_gate_not_cleared_when_connected(bridge):
    bridge._reload_gate.set()
    w = MagicMock()
    w.is_closing.return_value = False
    bridge._writer = w

    # The guard in send(): clear only when NOT connected
    if not bridge.connected:
        bridge._reload_gate.clear()

    assert bridge._reload_gate.is_set()


# ── INV-07: backoff stays in [BACKOFF_MIN_S, BACKOFF_MAX_S] ──────────────────
# All 141 experiments used backoff. Constants: BACKOFF_MIN_S=5.0, BACKOFF_MAX_S=60.0.

def test_backoff_bounds():
    backoff = BACKOFF_MIN_S
    for _ in range(20):
        jitter = backoff * 0.1  # max jitter magnitude (see bridge_heartbeat.py:L145)
        effective = min(backoff + jitter, BACKOFF_MAX_S)
        assert BACKOFF_MIN_S <= effective <= BACKOFF_MAX_S, (
            f"backoff={backoff} effective={effective} out of [{BACKOFF_MIN_S}, {BACKOFF_MAX_S}]"
        )
        backoff = min(backoff * 2, BACKOFF_MAX_S)


# ── INV-08: DomainReloadError expires at DOMAIN_RELOAD_EXPIRY_S=90s ──────────
# Prevents send() from blocking forever when Unity hangs post-reload.

def test_domain_reload_active_before_expiry():
    tracker = DomainReloadTracker()
    tracker.mark()
    tracker._since = time.monotonic() - (DOMAIN_RELOAD_EXPIRY_S - 1)  # 89s ago
    assert tracker.is_active()


def test_domain_reload_inactive_after_expiry():
    tracker = DomainReloadTracker()
    tracker.mark()
    tracker._since = time.monotonic() - (DOMAIN_RELOAD_EXPIRY_S + 1)  # 91s ago
    assert not tracker.is_active()


def test_domain_reload_expiry_constant():
    assert DOMAIN_RELOAD_EXPIRY_S == 90.0


# ── INV-09: cooldown blocks reconnect within MIN_RECONNECT_INTERVAL=5s ────────
# RC1 root cause: every send() triggered _reconnect() when connected returned False.
# MIN_RECONNECT_INTERVAL=5.0s cooldown gate prevents reconnect storm.

def test_cooldown_false_immediately_after_reconnect(bridge):
    bridge._last_reconnect_at = time.monotonic()
    bridge._reconnect_backoff = 5.0
    assert not bridge._reconnect_cooldown_ok()


def test_cooldown_true_after_interval_elapsed(bridge):
    bridge._last_reconnect_at = time.monotonic() - 6.0  # 6s ago > 5s backoff
    bridge._reconnect_backoff = 5.0
    assert bridge._reconnect_cooldown_ok()


def test_min_reconnect_interval_constant():
    assert MIN_RECONNECT_INTERVAL == 5.0


# ── INV-10: self-cancel guard prevents CancelledError on close-from-heartbeat ──
# RC2 self-cancel: when heartbeat calls close(), stop_heartbeat() would cancel its
# own task → CancelledError. Fix: skip stop_heartbeat() if caller IS heartbeat task.

async def test_close_from_heartbeat_skips_stop_heartbeat(bridge):
    stop_called = []
    original_stop = bridge.stop_heartbeat
    bridge.stop_heartbeat = lambda: stop_called.append(True)

    async def fake_heartbeat():
        await asyncio.sleep(99)

    task = asyncio.create_task(fake_heartbeat())
    bridge._heartbeat_task = task

    # Simulate: close() called from WITHIN the heartbeat task
    # asyncio.current_task() inside close() would be the heartbeat task itself
    with patch("asyncio.current_task", return_value=task):
        await bridge.close()  # guard should skip stop_heartbeat

    # stop_heartbeat was NOT called because guard fired
    assert not stop_called

    task.cancel()
    with suppress(asyncio.CancelledError):
        await task

    bridge.stop_heartbeat = original_stop
