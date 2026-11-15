import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from helpers import make_mock_bridge


async def test_initial_state():
    from unity_mcp.connection_slot import ConnectionSlot
    s = ConnectionSlot()
    assert s.bridge is None
    assert s.connected is False
    assert s.port == 9500


async def test_connect_creates_bridge():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        result = await s.connect(9500)
    assert s.bridge is b
    assert s.port == 9500
    assert "Connected" in result
    b.connect.assert_awaited_once()


async def test_connect_stores_port():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9501)
    assert s.port == 9501


async def test_connected_reflects_bridge_state():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge(connected=True)
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    assert s.connected is True


async def test_connect_unavailable_returns_registered():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.connect = AsyncMock(side_effect=OSError("refused"))
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        result = await s.connect(9500)
    assert "9500" in result
    assert s.bridge is b  # bridge still stored


async def test_reconnect_closes_previous():
    from unity_mcp.connection_slot import ConnectionSlot
    b1 = make_mock_bridge()
    b2 = make_mock_bridge()
    bridges = iter([b1, b2])
    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=lambda h, p, **_: next(bridges)):
        s = ConnectionSlot()
        await s.connect(9500)
        await s.connect(9501)
    b1.stop_heartbeat.assert_called_once()
    b1.close.assert_awaited_once()
    assert s.bridge is b2


async def test_close_clears_bridge():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
        await s.close()
    assert s.bridge is None
    b.stop_heartbeat.assert_called()
    b.close.assert_awaited()


# no-assert: crash guard
async def test_close_noop_when_empty():
    """Verifies ConnectionSlot.close() does not raise when no bridge is set."""
    from unity_mcp.connection_slot import ConnectionSlot
    s = ConnectionSlot()
    await s.close()  # must not raise


async def test_slot_syncs_port_from_bridge():
    """_sync_port reconnect callback updates slot.port to match bridge._port."""
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.add_reconnect_callback = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    # simulate bridge changing port then the _sync_port callback firing
    b._port = 9501
    # grab the _sync_port callback (last registered)
    sync_cb = b.add_reconnect_callback.call_args_list[-1][0][0]
    sync_cb()
    assert s.port == 9501


async def test_slot_fires_on_port_change():
    """on_port_change(old, new) fires when bridge._port differs from slot._port."""
    from unity_mcp.connection_slot import ConnectionSlot
    changes = []
    b = make_mock_bridge()
    b.add_reconnect_callback = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot(on_port_change=lambda old, new: changes.append((old, new)))
        await s.connect(9500)
    b._port = 9501
    sync_cb = b.add_reconnect_callback.call_args_list[-1][0][0]
    sync_cb()
    assert changes == [(9500, 9501)]


# ── PY1.test.4: connect() must start heartbeat ───────────────────────────────

async def test_connect_starts_heartbeat():
    """connect() must call start_heartbeat() on the bridge after successful connect."""
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.start_heartbeat = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    b.start_heartbeat.assert_called_once()


# ── reconnect callbacks on new bridge ────────────────────────────────────────

def test_connection_slot_stores_callbacks():
    """ConnectionSlot must expose a way to register/re-register reconnect callbacks."""
    from unity_mcp.connection_slot import ConnectionSlot
    from unittest.mock import MagicMock
    s = ConnectionSlot()
    cb = MagicMock()
    s.add_reconnect_callback(cb)
    assert cb in s._reconnect_callbacks


async def test_connection_slot_registers_callbacks_on_new_bridge():
    """Callbacks registered on slot are applied to every new bridge created by connect()."""
    from unity_mcp.connection_slot import ConnectionSlot
    from unittest.mock import AsyncMock, MagicMock
    from unittest.mock import patch

    b1 = MagicMock()
    b1.connect = AsyncMock()
    b1.close = AsyncMock()
    b1.connected = True
    b1.stop_heartbeat = MagicMock()
    b1.add_reconnect_callback = MagicMock()

    b2 = MagicMock()
    b2.connect = AsyncMock()
    b2.close = AsyncMock()
    b2.connected = True
    b2.stop_heartbeat = MagicMock()
    b2.add_reconnect_callback = MagicMock()

    bridges = iter([b1, b2])
    cb = MagicMock()

    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=lambda h, p, **_: next(bridges)):
        s = ConnectionSlot()
        s.add_reconnect_callback(cb)
        await s.connect(9500)
        assert b1.add_reconnect_callback.call_count == 2
        b1.add_reconnect_callback.assert_any_call(cb)

        await s.connect(9501)
        assert b2.add_reconnect_callback.call_count == 2
        b2.add_reconnect_callback.assert_any_call(cb)


# ---------------------------------------------------------------------------
# Reconnect spam fix: add_reconnect_callback wires to existing bridge (Fix 2)
# ---------------------------------------------------------------------------

async def test_add_reconnect_callback_wires_to_existing_bridge():
    """add_reconnect_callback() must wire cb to current bridge if one exists.

    This ensures the initial bridge (B1) gets callbacks — not just future ones.
    Previously, callbacks were only wired inside connect() so B1 never got them.
    """
    from unity_mcp.connection_slot import ConnectionSlot
    from unittest.mock import MagicMock, patch

    b = make_mock_bridge()
    b.add_reconnect_callback = MagicMock()

    cb = MagicMock()

    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
        # b.add_reconnect_callback was called once inside connect() for _sync_port.
        call_count_before = b.add_reconnect_callback.call_count

        # Now add a callback AFTER connect — should also wire to existing bridge
        s.add_reconnect_callback(cb)

    assert b.add_reconnect_callback.call_count == call_count_before + 1, \
        "add_reconnect_callback must wire cb to current bridge immediately"
    b.add_reconnect_callback.assert_called_with(cb)


# ── P-427: Failed connect must not update slot.port ──────────────────────────

async def test_connect_failure_preserves_old_port():
    """P-427: failed connect must not update slot.port to the failed port."""
    from unity_mcp.connection_slot import ConnectionSlot

    b_ok = make_mock_bridge()
    b_fail = make_mock_bridge()
    b_fail.connect = AsyncMock(side_effect=OSError("refused"))
    bridges = iter([b_ok, b_fail])

    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=lambda h, p, **_: next(bridges)):
        s = ConnectionSlot()
        await s.connect(9500)
        assert s.port == 9500
        # Try connecting to 9600 — fails
        result = await s.connect(9600)

    assert s.port == 9500, "slot.port must preserve old value on failed connect"
    assert "not yet available" in result


# ── Race condition / CancelledError fixes ────────────────────────────────────

async def test_concurrent_connect_only_one_bridge():
    """Two concurrent connect() calls must create exactly one bridge (lock guard)."""
    from unity_mcp.connection_slot import ConnectionSlot

    started = asyncio.Event()
    go = asyncio.Event()

    async def slow_connect(*_):
        started.set()
        await go.wait()

    bridge_instances = []

    def make_bridge(h, p, **kw):
        b = make_mock_bridge()
        b.connect = AsyncMock(side_effect=slow_connect)
        bridge_instances.append(b)
        return b

    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=make_bridge):
        s = ConnectionSlot()
        t1 = asyncio.create_task(s.connect(9500))
        await started.wait()              # t1 holds lock, waiting inside connect()
        t2 = asyncio.create_task(s.connect(9500))
        await asyncio.sleep(0)            # let t2 try to acquire lock
        go.set()                          # release t1
        await asyncio.gather(t1, t2)

    assert len(bridge_instances) == 1, f"Expected 1 bridge, got {len(bridge_instances)}"
    assert s.bridge is bridge_instances[0]


async def test_cancelled_connect_cleans_up_bridge():
    """CancelledError during connect() must close bridge and leave self._bridge=None."""
    from unity_mcp.connection_slot import ConnectionSlot

    async def hang(*_):
        await asyncio.sleep(100)

    b = make_mock_bridge()
    b.connect = AsyncMock(side_effect=hang)

    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        task = asyncio.create_task(s.connect(9500))
        await asyncio.sleep(0)   # let task enter connect()
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task

    assert s.bridge is None, "Cancelled connect must not leave orphaned bridge"
    b.close.assert_awaited()


async def test_connect_after_lock_skip_if_already_connected():
    """Second concurrent connect() skips when first has already set the bridge."""
    from unity_mcp.connection_slot import ConnectionSlot

    started = asyncio.Event()
    go = asyncio.Event()

    async def slow_connect(*_):
        started.set()
        await go.wait()

    bridge_instances = []

    def make_bridge(h, p, **kw):
        b = make_mock_bridge()
        b.connect = AsyncMock(side_effect=slow_connect)
        bridge_instances.append(b)
        return b

    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=make_bridge):
        s = ConnectionSlot()
        t1 = asyncio.create_task(s.connect(9500))
        await started.wait()
        t2 = asyncio.create_task(s.connect(9500))
        await asyncio.sleep(0)
        go.set()
        await asyncio.gather(t1, t2)

    # Only the first bridge was created; second task reused it
    assert len(bridge_instances) == 1
    assert s.bridge is bridge_instances[0]
