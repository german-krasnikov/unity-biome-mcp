"""Tests for DORMANT/WAKING states: suspend(), heartbeat guard, status, watchdog."""
import asyncio
import os
import time
import threading
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

from unity_mcp.bridge import BridgeState, UnityBridge
from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S

from helpers import make_writer


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _connected_bridge() -> UnityBridge:
    """UnityBridge with live writer mock (connected=True, state=CONNECTED)."""
    from unity_mcp.compile_state import CompileStateProbe

    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()

    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    bridge._writer = make_writer()
    bridge._reader = MagicMock()
    bridge._state = BridgeState.CONNECTED
    return bridge


# ---------------------------------------------------------------------------
# suspend() — happy path
# ---------------------------------------------------------------------------

async def test_suspend_transitions_to_dormant():
    """CONNECTED + empty queue → returns True, state=DORMANT, writer=None."""
    bridge = _connected_bridge()

    result = await bridge.suspend()

    assert result is True
    assert bridge._state == BridgeState.DORMANT
    assert bridge._writer is None
    assert bridge._reader is None


async def test_suspend_resets_cooldown():
    """suspend() resets _last_reconnect_at to 0.0 and backoff to BACKOFF_MIN_S."""
    bridge = _connected_bridge()
    bridge._last_reconnect_at = time.monotonic()
    bridge._reconnect_backoff = 60.0

    await bridge.suspend()

    assert bridge._last_reconnect_at == 0.0
    assert bridge._reconnect_backoff == BACKOFF_MIN_S


# ---------------------------------------------------------------------------
# suspend() — postcondition: must return False if bridge reconnects during wait_closed
# ---------------------------------------------------------------------------

async def test_suspend_returns_false_if_reconnected_during_teardown():
    """suspend() returns False when a concurrent send reconnects during wait_closed().

    Race: state=DORMANT set under lock → writer=None (sync) →
    await wait_closed() yields → _send_with_retry reconnects → state=CONNECTED.
    suspend() must see the new state and return False (not True).
    Discriminating: fails without the `return self._state == BridgeState.DORMANT` fix.
    """
    bridge = _connected_bridge()
    new_writer = make_writer()

    async def simulate_reconnect():
        # Mimics what _accept_candidate does on successful reconnect.
        bridge._writer = new_writer
        bridge._state = BridgeState.CONNECTED

    bridge._writer.wait_closed = AsyncMock(side_effect=simulate_reconnect)

    result = await bridge.suspend()

    assert result is False, "suspend() must return False when bridge reconnected mid-teardown"
    assert bridge._state == BridgeState.CONNECTED
    assert bridge._writer is new_writer


# ---------------------------------------------------------------------------
# suspend() — guard conditions
# ---------------------------------------------------------------------------

async def test_suspend_aborts_when_queue_nonempty():
    """Queue non-empty → returns False, state unchanged."""
    bridge = _connected_bridge()
    future = asyncio.get_running_loop().create_future()
    await bridge._send_queue.put(("ping", b"x", "001", 30.0, 0.0, "op", future))

    result = await bridge.suspend()

    assert result is False
    assert bridge._state == BridgeState.CONNECTED


async def test_suspend_aborts_when_not_connected():
    """State != CONNECTED → returns False, no TCP teardown."""
    bridge = UnityBridge()
    # Default state is DISCONNECTED, writer is None
    result = await bridge.suspend()
    assert result is False


# ---------------------------------------------------------------------------
# suspend() — race: item arrives before lock is granted
# ---------------------------------------------------------------------------

async def test_suspend_race_request_arrives_before_lock():
    """Item enqueued while suspend() waits for lock → guard sees non-empty queue."""
    bridge = _connected_bridge()

    hold = asyncio.Event()
    release = asyncio.Event()

    async def hold_lock():
        async with bridge._lock:
            hold.set()
            await release.wait()

    lock_task = asyncio.create_task(hold_lock())
    await hold.wait()

    # Enqueue an item while the lock is held by the other task
    future = asyncio.get_running_loop().create_future()
    await bridge._send_queue.put(("ping", b"x", "001", 30.0, 0.0, "op", future))

    # suspend() must wait for the lock, then see the non-empty queue
    suspend_task = asyncio.create_task(bridge.suspend())
    release.set()
    result = await suspend_task
    await lock_task

    assert result is False
    assert bridge._state == BridgeState.CONNECTED


# ---------------------------------------------------------------------------
# Heartbeat — DORMANT guard
# ---------------------------------------------------------------------------

async def test_heartbeat_skips_reconnect_in_dormant():
    """Heartbeat tick with DORMANT state returns without attempting reconnect."""
    bridge = UnityBridge()
    bridge._state = BridgeState.DORMANT
    # _writer is None → connected=False → would normally try reconnect

    reconnect_called = []
    bridge._reconnect = AsyncMock(side_effect=lambda **kw: reconnect_called.append(1))

    with patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert not reconnect_called, "heartbeat must not reconnect in DORMANT state"


# ---------------------------------------------------------------------------
# status / transport_status properties
# ---------------------------------------------------------------------------

def test_status_returns_dormant():
    """BridgeState.DORMANT → status == 'dormant'."""
    bridge = UnityBridge()
    bridge._state = BridgeState.DORMANT
    assert bridge.status == "dormant"


def test_status_returns_waking():
    """BridgeState.WAKING → status == 'waking'."""
    bridge = UnityBridge()
    bridge._state = BridgeState.WAKING
    assert bridge.status == "waking"


def test_transport_status_dormant():
    """BridgeState.DORMANT → transport_status == 'tcp:dormant'."""
    bridge = UnityBridge()
    bridge._state = BridgeState.DORMANT
    assert bridge.transport_status == "tcp:dormant"


def test_transport_status_waking():
    """BridgeState.WAKING → transport_status == 'tcp:waking'."""
    bridge = UnityBridge()
    bridge._state = BridgeState.WAKING
    assert bridge.transport_status == "tcp:waking"


# ---------------------------------------------------------------------------
# _send_with_retry: DORMANT → WAKING transition
# ---------------------------------------------------------------------------

async def test_send_wakes_from_dormant_via_waking_state():
    """_send_with_retry transitions DORMANT → WAKING before calling _reconnect."""
    bridge = UnityBridge()
    bridge._state = BridgeState.DORMANT
    bridge._last_reconnect_at = 0.0  # cooldown already reset by suspend()

    states_at_reconnect: list[BridgeState] = []

    async def mock_reconnect(**kw):
        states_at_reconnect.append(bridge._state)
        bridge._writer = make_writer()
        bridge._reader = AsyncMock()
        bridge._state = BridgeState.CONNECTED

    async def mock_read_response():
        return {"id": "0001", "ok": True, "data": "pong"}

    bridge._reconnect = mock_reconnect
    bridge._read_response = mock_read_response

    payload = b'{"id":"0001","cmd":"ping","args":{},"op_id":"t"}'
    with patch("unity_mcp.bridge.frame_write"):
        await bridge._send_with_retry("ping", payload, "0001", 5.0,
                                      time.monotonic() + 60, "t")

    assert states_at_reconnect == [BridgeState.WAKING], (
        f"Expected WAKING before reconnect, got: {states_at_reconnect}"
    )


# ---------------------------------------------------------------------------
# Concurrent sends from DORMANT: only one reconnect
# ---------------------------------------------------------------------------

async def test_concurrent_sends_on_dormant_single_reconnect():
    """Two concurrent sends from DORMANT trigger exactly one reconnect."""
    bridge = UnityBridge()
    bridge._state = BridgeState.DORMANT
    bridge._last_reconnect_at = 0.0

    reconnect_count = 0

    async def mock_reconnect(**kw):
        nonlocal reconnect_count
        reconnect_count += 1
        bridge._writer = make_writer()
        bridge._reader = AsyncMock()
        bridge._state = BridgeState.CONNECTED

    async def mock_read_response():
        return {"id": "0001", "ok": True, "data": "pong"}

    bridge._reconnect = mock_reconnect
    bridge._read_response = mock_read_response

    payload = b'{"id":"0001","cmd":"ping","args":{},"op_id":"t"}'
    with patch("unity_mcp.bridge.frame_write"):
        t1 = asyncio.create_task(
            bridge._send_with_retry("ping", payload, "0001", 5.0,
                                    time.monotonic() + 60, "t1")
        )
        t2 = asyncio.create_task(
            bridge._send_with_retry("ping", payload, "0001", 5.0,
                                    time.monotonic() + 60, "t2")
        )
        await asyncio.gather(t1, t2, return_exceptions=True)

    assert reconnect_count == 1, f"Expected 1 reconnect, got {reconnect_count}"


# ---------------------------------------------------------------------------
# server.py: _schedule_dormant TOCTOU guard
# ---------------------------------------------------------------------------

async def test_schedule_dormant_toctou_guard(monkeypatch):
    """_schedule_dormant does not suspend when recent useful activity occurred."""
    import unity_mcp.server as srv

    bridge_mock = AsyncMock()
    bridge_mock.connected = True
    bridge_mock.suspend = AsyncMock(return_value=True)

    mock_slot = MagicMock()
    mock_slot.bridge = bridge_mock

    loop = asyncio.get_running_loop()
    monkeypatch.setattr(srv, "_sigterm_state", {"loop": loop})
    monkeypatch.setattr(srv, "_last_useful_activity", time.monotonic())  # just now
    monkeypatch.setattr(srv, "slot", mock_slot)

    srv._schedule_dormant(idle=350.0, timeout=300)
    # Yield to event loop so the scheduled coroutine can run
    await asyncio.sleep(0.05)

    bridge_mock.suspend.assert_not_called()


# ---------------------------------------------------------------------------
# server.py: watchdog calls _schedule_dormant when parent alive
# ---------------------------------------------------------------------------

def test_watchdog_schedules_dormant_when_parent_alive(monkeypatch):
    """When parent is alive and idle > timeout, watchdog calls _schedule_dormant."""
    import unity_mcp.server as srv

    called = threading.Event()
    dormant_calls: list = []

    def fake_schedule_dormant(idle, timeout):
        dormant_calls.append((idle, timeout))
        called.set()
        srv._watchdog_stop.set()

    monkeypatch.setattr(srv, "_last_useful_activity", time.monotonic() - 400)
    monkeypatch.setattr(srv, "_in_flight_count", 0)
    monkeypatch.setattr(srv, "_schedule_dormant", fake_schedule_dormant)
    monkeypatch.setenv("UNITY_MCP_IDLE_TIMEOUT", "300")
    monkeypatch.setenv("UNITY_MCP_USEFUL_IDLE_TIMEOUT", "0")

    with patch("time.sleep"):  # skip the 30s sleep
        srv._watchdog_stop.clear()
        t = srv._start_idle_watchdog()

    called.wait(timeout=2.0)
    srv._watchdog_stop.set()
    if t:
        t.join(timeout=2.0)

    assert dormant_calls, "_schedule_dormant must be called when parent is alive"
