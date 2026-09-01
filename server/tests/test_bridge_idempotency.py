"""P-322: Mutation retry must carry op_id for Unity-side dedup.

TDD: Tests for DeliveryState tracking and retry_op_id on post-SENT retry.
"""
import asyncio
import json
import time
import uuid
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import unity_mcp.bridge as bridge_module
from unity_mcp.bridge import BridgeState, DeliveryState, UnityBridge


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
    """When a retry happens after the payload was SENT (writer accepted it,
    response failed),
    the second attempt must include 'retry_op_id' == first attempt's 'op_id'.

    Red: _send_with_retry does not track DeliveryState or rebuild payload.
    """
    bridge = UnityBridge(
        port=9999,
        is_retry_safe=lambda cmd: cmd == "get_hierarchy",
    )
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()
    bridge._writer = writer
    bridge._reader = MagicMock()
    bridge._state = BridgeState.CONNECTED

    sent_raws: list[bytes] = []

    def capture_write(_writer, data):
        sent_raws.append(data)

    read_results = [
        OSError("lost ACK"),
        {"id": "0001", "ok": True, "data": "ok"},
    ]

    async def mock_read_response():
        val = read_results.pop(0)
        if isinstance(val, Exception):
            raise val
        return val

    payload = json.dumps({
        "id": "0001", "cmd": "get_hierarchy", "args": {},
        "op_id": "safe-op",
    }).encode("utf-8")

    with (
        patch.object(bridge_module, "frame_write", side_effect=capture_write),
        patch.object(bridge, "_read_response", side_effect=mock_read_response),
        patch.object(bridge, "close", new_callable=AsyncMock),
        patch.object(bridge_module.asyncio, "sleep", new_callable=AsyncMock),
    ):
        result = await bridge._send_with_retry(
            "get_hierarchy", payload, "0001", 1.0,
            time.monotonic() + 60, "safe-op",
        )

    assert result["ok"] is True
    assert len(sent_raws) == 2
    first = json.loads(sent_raws[0].decode("utf-8"))
    second = json.loads(sent_raws[1].decode("utf-8"))

    assert "op_id" in first, "First payload missing op_id"
    assert "retry_op_id" in second, f"Retry payload missing retry_op_id: {second}"
    assert second["retry_op_id"] == first["op_id"], (
        f"retry_op_id {second['retry_op_id']!r} != original op_id {first['op_id']!r}"
    )


async def test_unsent_unsafe_command_may_reconnect_then_write_exactly_once():
    """A reconnect failure before writer acceptance cannot duplicate a mutation."""
    bridge = _make_bridge()  # default predicate is fail-closed/unsafe
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()
    reconnect_count = 0
    frames: list[dict] = []

    async def reconnect(*, fire_callbacks=False):
        nonlocal reconnect_count
        reconnect_count += 1
        if reconnect_count == 1:
            raise OSError("connect failed before send")
        bridge._writer = writer
        bridge._reader = MagicMock()
        bridge._state = BridgeState.CONNECTED

    def count_frame(_writer, raw: bytes) -> None:
        frames.append(json.loads(raw.decode("utf-8")))

    payload = json.dumps({
        "id": "0001",
        "cmd": "source_patch_write",
        "args": {"path": "Assets/Target.cs"},
        "op_id": "unsafe-op",
    }).encode("utf-8")

    with (
        patch.object(bridge, "_reconnect", side_effect=reconnect),
        patch.object(bridge_module, "frame_write", side_effect=count_frame),
        patch.object(
            bridge, "_read_response", new_callable=AsyncMock,
            return_value={"id": "0001", "ok": True, "data": "ok"},
        ),
        patch.object(bridge, "close", new_callable=AsyncMock),
        patch.object(bridge_module.asyncio, "sleep", new_callable=AsyncMock),
    ):
        result = await bridge._send_with_retry(
            "source_patch_write", payload, "0001", 1.0,
            time.monotonic() + 60, "unsafe-op",
        )

    assert result["ok"] is True
    assert reconnect_count == 2
    assert len(frames) == 1
    assert frames[0]["op_id"] == "unsafe-op"
    assert "retry_op_id" not in frames[0]


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
