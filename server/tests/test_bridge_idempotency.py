"""P-322: Mutation retry must carry op_id for Unity-side dedup.

TDD: Tests for DeliveryState tracking and retry_op_id on post-SENT retry.
"""
import asyncio
import json
import uuid

import pytest

from unity_mcp.bridge import DeliveryState, UnityBridge


def _make_bridge() -> UnityBridge:
    b = UnityBridge(port=9999)
    return b


# ---------------------------------------------------------------------------
# Test 1 — op_id present in every outgoing payload
# ---------------------------------------------------------------------------

async def test_operation_id_present_in_payload():
    """Every payload sent to Unity must include a valid UUID under key 'op_id'.

    Red: bridge.py does not yet generate operation_id or include op_id in payload.
    """
    bridge = _make_bridge()
    captured: list[bytes] = []

    async def capture(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        captured.append(payload)
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = capture

    await bridge.send("get_hierarchy", {})

    assert captured, "No payload was sent"
    data = json.loads(captured[0].decode("utf-8"))
    assert "op_id" in data, f"op_id missing from payload: {data}"
    op_id = data["op_id"]
    # Must be a valid UUID4
    parsed = uuid.UUID(op_id, version=4)
    assert str(parsed) == op_id


async def test_operation_id_unique_per_call():
    """Each send() call must generate a distinct op_id.

    Red: without uuid generation, op_id would be absent or constant.
    """
    bridge = _make_bridge()
    op_ids: list[str] = []

    async def capture(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        data = json.loads(payload.decode("utf-8"))
        op_ids.append(data.get("op_id", ""))
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = capture

    await bridge.send("cmd_a", {})
    await bridge.send("cmd_b", {})
    await bridge.send("cmd_c", {})

    assert len(op_ids) == 3
    assert len(set(op_ids)) == 3, f"op_ids not unique: {op_ids}"


# ---------------------------------------------------------------------------
# Test 2 — retry carries retry_op_id matching first op_id
# ---------------------------------------------------------------------------

async def test_retry_includes_retry_op_id():
    """When a retry happens after the payload was SENT (write OK, read failed),
    the second attempt must include 'retry_op_id' == first attempt's 'op_id'.

    Red: _send_with_retry does not track DeliveryState or rebuild payload.
    """
    bridge = _make_bridge()
    payloads: list[dict] = []
    call_count = 0

    async def flaky(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        nonlocal call_count
        call_count += 1
        payloads.append(json.loads(payload.decode("utf-8")))
        if call_count == 1:
            # Simulate: write succeeded (SENT), but response was lost
            raise ConnectionError("simulated lost ACK after write")
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = flaky

    # Mark cmd as retry-safe so _send_with_retry retries
    bridge._is_retry_safe = lambda cmd: True
    bridge._retry_policy._is_retry_safe = lambda cmd: True

    # We intercept at _send_with_retry level, so retry is inside bridge.send()
    # via the retry loop in _send_with_retry itself.
    # For this test: test that when _send_with_retry is called a second time by
    # the queue consumer after ConnectionError, the payload carries retry_op_id.
    #
    # Since we're mocking _send_with_retry, we can't test the internal retry loop
    # directly. Instead, test that the real _send_with_retry (not mocked) adds
    # retry_op_id to the payload when re-trying after SENT state.
    #
    # Restore real _send_with_retry and mock at socket level:
    del bridge._send_with_retry

    first_payload: list[dict] = []
    second_payload: list[dict] = []
    write_count = [0]

    async def mock_drain(): pass

    class MockWriter:
        def __init__(self):
            self.is_closing_val = False
        def is_closing(self): return self.is_closing_val

    import struct as _struct
    from unittest.mock import AsyncMock, MagicMock, patch

    # Intercept frame_write to capture raw payloads
    sent_raws: list[bytes] = []

    import unity_mcp.bridge as bridge_mod

    orig_frame_write = bridge_mod.frame_write

    def capture_write(writer, data):
        sent_raws.append(data)

    # Simulate: first write succeeds, drain OK, but read raises ConnectionError
    # second attempt: write + read succeed
    read_results = [
        ConnectionError("lost ACK"),                          # first read fails
        {"id": "0001", "ok": True, "data": "ok"},            # second read ok
    ]
    read_idx = [0]

    async def mock_read_response():
        idx = read_idx[0]
        read_idx[0] += 1
        val = read_results[idx]
        if isinstance(val, Exception):
            raise val
        return val

    bridge._writer = MagicMock()
    bridge._writer.is_closing.return_value = False
    bridge._state = bridge_mod.BridgeState.CONNECTED
    bridge._reader = MagicMock()

    with (
        patch.object(bridge_mod, "frame_write", side_effect=capture_write),
        patch.object(bridge, "_read_response", side_effect=mock_read_response),
        patch.object(bridge._writer, "drain", new_callable=AsyncMock),
        patch.object(bridge, "_reconnect", new_callable=AsyncMock),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        bridge._is_retry_safe = lambda cmd: True
        bridge._retry_policy._is_retry_safe = lambda cmd: True

        try:
            result = await bridge.send("create_object", {"name": "Cube"})
        except Exception:
            pass  # may fail on ID mismatch; we only care about payload content

    assert len(sent_raws) >= 2, f"Expected at least 2 writes (original + retry), got {len(sent_raws)}"
    first = json.loads(sent_raws[0].decode("utf-8"))
    second = json.loads(sent_raws[1].decode("utf-8"))

    assert "op_id" in first, "First payload missing op_id"
    assert "retry_op_id" in second, f"Retry payload missing retry_op_id: {second}"
    assert second["retry_op_id"] == first["op_id"], (
        f"retry_op_id {second['retry_op_id']!r} != original op_id {first['op_id']!r}"
    )


# ---------------------------------------------------------------------------
# Test 3 — DeliveryState enum is exported from bridge module
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# MCP-IDEMP-026: Op-ID-only dedup — no payload-similarity suppression
# ---------------------------------------------------------------------------

async def test_different_op_ids_same_payload_both_execute():
    """Identical cmd+args must produce distinct op_ids — no payload-based dedup on Python side."""
    bridge = _make_bridge()
    op_ids: list[str] = []

    async def capture(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        data = json.loads(payload.decode("utf-8"))
        op_ids.append(data["op_id"])
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = capture

    await bridge.send("set_property", {"path": "/Cube", "value": 42})
    await bridge.send("set_property", {"path": "/Cube", "value": 42})  # identical args

    assert len(set(op_ids)) == 2, f"Same payload must yield distinct op_ids; got {op_ids}"


async def test_same_op_id_returns_cached_result():
    """CommandLedger records COMPLETED with result after send(); get_command_status reflects it."""
    from unity_mcp.bridge import CommandStatus
    bridge = _make_bridge()
    captured_op: list[str] = []

    async def capture(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        captured_op.append(operation_id)
        return {"id": msg_id, "ok": True, "data": "cached-value"}

    bridge._send_with_retry = capture
    await bridge.send("set_property", {"path": "/Cube", "value": 42})

    op_id = captured_op[0]
    status, result = bridge.get_command_status(op_id)
    assert status == CommandStatus.COMPLETED
    assert result is not None
    assert result.get("data") == "cached-value"


async def test_dedup_applied_flag_present():
    """dedup_applied flag returned by C# passes through bridge dict unchanged."""
    bridge = _make_bridge()

    async def return_dedup(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        return {"id": msg_id, "ok": True, "data": "original", "dedup_applied": True}

    bridge._send_with_retry = return_dedup
    result = await bridge.send("set_property", {"path": "/Cube", "value": 42})

    assert result.get("dedup_applied") is True, (
        "dedup_applied flag from C# response must be preserved in bridge output"
    )


def test_delivery_state_enum_exported():
    """DeliveryState must be importable from unity_mcp.bridge and have UNSENT/SENT/DELIVERED/FAILED.

    Red: DeliveryState does not exist yet.
    """
    from unity_mcp.bridge import DeliveryState
    assert hasattr(DeliveryState, "UNSENT")
    assert hasattr(DeliveryState, "SENT")
    assert hasattr(DeliveryState, "DELIVERED")
    assert hasattr(DeliveryState, "FAILED")
    assert DeliveryState.UNSENT.value == "unsent"
    assert DeliveryState.SENT.value == "sent"
    assert DeliveryState.DELIVERED.value == "delivered"
    assert DeliveryState.FAILED.value == "failed"
