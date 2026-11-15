"""P-092: Queue-serialized send() — concurrent requests must be processed serially.

TDD: RED phase — all tests must fail without the asyncio.Queue in bridge.py.
"""
import asyncio
import pytest

from unity_mcp.bridge import UnityBridge, BridgeState


def _make_bridge() -> UnityBridge:
    """Bridge in DISCONNECTED (non-FAILED) state; reload not active."""
    b = UnityBridge(port=9999)
    # Default: BridgeState.DISCONNECTED, reload not active — send() won't
    # trip state checks; we replace _send_with_retry so no real TCP needed.
    return b


async def test_concurrent_sends_are_serialized():
    """_send_with_retry must never be entered by more than one caller at a time.

    Without a Queue every gather'd task can call _send_with_retry concurrently.
    With the Queue the consumer serialises them — max_concurrent == 1.
    """
    bridge = _make_bridge()

    concurrent_count = 0
    max_concurrent = 0

    async def tracked(cmd, payload, msg_id, timeout, deadline, op_id=""):
        nonlocal concurrent_count, max_concurrent
        concurrent_count += 1
        max_concurrent = max(max_concurrent, concurrent_count)
        await asyncio.sleep(0.05)  # yield so other tasks can enter concurrently
        concurrent_count -= 1
        return {"id": msg_id, "ok": True, "data": cmd}

    bridge._send_with_retry = tracked

    await asyncio.gather(*[bridge.send(f"cmd{i}", {}) for i in range(5)])

    assert max_concurrent == 1, (
        f"Expected serial execution (max_concurrent=1), got {max_concurrent}. "
        "P-092: add asyncio.Queue to serialize send()."
    )


async def test_queue_preserves_result_per_caller():
    """Each concurrent caller must receive its own response, not a neighbour's."""
    bridge = _make_bridge()

    async def echo(cmd, payload, msg_id, timeout, deadline, op_id=""):
        await asyncio.sleep(0.01)
        return {"id": msg_id, "ok": True, "data": f"reply:{cmd}"}

    bridge._send_with_retry = echo

    results = await asyncio.gather(
        bridge.send("alpha", {}),
        bridge.send("beta", {}),
        bridge.send("gamma", {}),
    )

    data = [r["data"] for r in results]
    assert "reply:alpha" in data
    assert "reply:beta" in data
    assert "reply:gamma" in data


async def test_circuit_breaker_sees_serial_probes():
    """Never more than one in-flight request — circuit breaker probe is serial.

    This is equivalent to test_concurrent_sends_are_serialized but framed as
    the circuit-breaker scenario described in P-092: 7 concurrent requests must
    never simultaneously probe a HALF_OPEN breaker.
    """
    bridge = _make_bridge()

    concurrent = 0
    max_concurrent = 0

    async def probe_tracker(cmd, payload, msg_id, timeout, deadline, op_id=""):
        nonlocal concurrent, max_concurrent
        concurrent += 1
        max_concurrent = max(max_concurrent, concurrent)
        await asyncio.sleep(0.02)
        concurrent -= 1
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = probe_tracker

    await asyncio.gather(*[bridge.send("lint", {}) for _ in range(7)])

    assert max_concurrent == 1, (
        f"Circuit breaker saw {max_concurrent} concurrent probes; expected 1."
    )


async def test_queue_consumer_stops_on_close():
    """After close() the consumer task must be done (not still running)."""
    bridge = _make_bridge()

    async def instant(cmd, payload, msg_id, timeout, deadline, op_id=""):
        return {"id": msg_id, "ok": True, "data": cmd}

    bridge._send_with_retry = instant

    # First send — starts the queue consumer.
    await bridge.send("first", {})

    # Consumer task must have been created.
    assert bridge._queue_consumer_task is not None, (
        "send() should create _queue_consumer_task"
    )

    # After close the task should be done or None.
    await bridge.close()

    task = bridge._queue_consumer_task
    assert task is None or task.done(), (
        "close() should stop the queue consumer task"
    )


async def test_queue_consumer_cancelled_error_propagates():
    """Bug 3 regression: CancelledError in _queue_consumer must propagate, not be swallowed.

    Pre-fix: `except asyncio.CancelledError: break` silently exited the loop
    (task completed normally).  Post-fix: `raise` → task is properly cancelled.
    """
    bridge = _make_bridge()
    # Start consumer directly — it blocks waiting on the empty queue.
    consumer = asyncio.create_task(bridge._queue_consumer())
    await asyncio.sleep(0)  # yield so consumer enters await get()

    consumer.cancel()
    await asyncio.sleep(0)  # let cancellation propagate

    assert consumer.cancelled(), (
        "Bug 3: _queue_consumer must re-raise CancelledError (not break)"
    )
