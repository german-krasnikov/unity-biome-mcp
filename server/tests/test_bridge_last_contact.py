"""ARC-7 T2: last_contact_age_s / pending_queue_depth diagnostic fields on UnityBridge.

`_last_contact_at` marks the last *successful* contact with Unity — a pong on
the heartbeat ping, or a completed send() round-trip. It must NOT be touched
by a failed/timed-out attempt. `pending_queue_depth` is a plain getter over
the existing send-queue depth; no new instrumentation.
"""
import asyncio
import time
from unittest.mock import patch

from helpers import make_writer
from test_bridge_heartbeat import _make_connected_bridge


async def test_updated_on_successful_ping():
    """_raw_ping() on a confirmed pong sets _last_contact_at to "now", not to a
    stale/construction-time value (Arm B guard: `is not None` alone would also
    pass a bug that sets the field once in __init__)."""
    bridge = _make_connected_bridge()
    bridge._writer = make_writer()
    assert bridge.last_contact_age_s is None

    async def echo_ping_id():
        return {"id": f"hb{bridge._counter:04x}", "ok": True, "data": "pong"}

    before = time.monotonic()
    with patch.object(bridge, "_read_response", new=echo_ping_id):
        await bridge._raw_ping(timeout=1.0)

    assert bridge._last_contact_at is not None
    assert bridge._last_contact_at >= before
    assert bridge.last_contact_age_s >= 0.0


async def test_not_updated_on_ping_timeout():
    """A timed-out ping must leave _last_contact_at untouched (still None)."""
    bridge = _make_connected_bridge()
    bridge._writer = make_writer()

    async def raise_timeout():
        raise TimeoutError("simulated ping timeout")

    with patch.object(bridge, "_read_response", new=raise_timeout):
        try:
            await bridge._raw_ping(timeout=1.0)
        except TimeoutError:
            pass

    assert bridge._last_contact_at is None
    assert bridge.last_contact_age_s is None


async def test_updated_on_successful_send():
    """A completed send() round-trip (consumer success path) sets _last_contact_at."""
    bridge = _make_connected_bridge()
    before = time.monotonic()

    async def fake_send_with_retry(cmd, payload, msg_id, timeout, deadline, op_id=""):
        return {"id": msg_id, "ok": True, "data": "ok"}

    with patch.object(bridge, "_send_with_retry", new=fake_send_with_retry):
        result = await bridge.send("ping", {})

    assert result["ok"] is True
    assert bridge._last_contact_at is not None
    assert bridge._last_contact_at >= before


async def test_pending_queue_depth_reflects_qsize():
    """pending_queue_depth wraps _send_queue.qsize() — no consumer running."""
    bridge = _make_connected_bridge()
    assert bridge.pending_queue_depth == 0

    for i in range(2):
        bridge._send_queue.put_nowait(
            ("get_status", b"{}", f"{i:04x}", 30.0, time.monotonic() + 30.0, f"op-{i}", asyncio.Future())
        )

    assert bridge.pending_queue_depth == 2
