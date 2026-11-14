"""MCP-TRANS-008: CommandLedger tracks command state across transport lifecycle.

TDD: Tests written FIRST — all must fail until implementation is added.
"""
import asyncio
import time
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

from unity_mcp.bridge import CommandStatus, UnityBridge
import unity_mcp.bridge as bridge_mod


def _make_bridge() -> UnityBridge:
    b = UnityBridge(port=9999)
    return b


# ---------------------------------------------------------------------------
# Test 1 — unknown op_id returns NOT_FOUND
# ---------------------------------------------------------------------------

def test_delivery_state_unknown_op_id_returns_not_found():
    """Querying an op_id that was never sent must return NOT_FOUND.

    Red: CommandStatus and get_command_status don't exist yet.
    """
    bridge = _make_bridge()
    status, result = bridge.get_command_status("nonexistent-op-id")
    assert status == CommandStatus.NOT_FOUND
    assert result is None


# ---------------------------------------------------------------------------
# Test 2 — state transitions to ACCEPTED after writer acceptance
# ---------------------------------------------------------------------------

async def test_delivery_state_tracks_accepted():
    """After the writer accepts a frame, op_id ledger state is ACCEPTED.

    Red: _send_with_retry doesn't update CommandLedger yet.
    """
    bridge = _make_bridge()
    captured_op_id = []

    # Intercept: write succeeds but read hangs → we check state mid-flight
    async def controlled_read():
        # Writer acceptance precedes response handling → ACCEPTED must be set.
        raise ConnectionError("simulated mid-flight disconnect")

    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()

    # Spy on ledger.record to capture the ACCEPTED call specifically
    accepted_op_ids = []
    original_record = bridge._ledger.record

    def spy_record(op_id, status, result=None):
        if status == CommandStatus.ACCEPTED:
            accepted_op_ids.append(op_id)
        original_record(op_id, status, result)

    bridge._ledger.record = spy_record

    with (
        patch.object(bridge_mod, "frame_write"),
        patch.object(bridge, "_read_response", side_effect=controlled_read),
        patch.object(bridge, "_reconnect", new_callable=AsyncMock),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        bridge._writer = writer
        bridge._state = bridge_mod.BridgeState.CONNECTED
        bridge._reader = MagicMock()
        bridge._is_retry_safe = lambda cmd: False  # no retry

        try:
            await bridge.send("create_object", {"name": "Cube"})
        except Exception:
            pass  # expected — connection error after write

    assert accepted_op_ids, (
        "Expected CommandLedger.record(ACCEPTED) after writer accepted frame"
    )


# ---------------------------------------------------------------------------
# Test 3 — state transitions to COMPLETED after response received
# ---------------------------------------------------------------------------

async def test_delivery_state_tracks_completed():
    """After a successful round-trip, op_id ledger state is COMPLETED.

    Red: CommandLedger doesn't track COMPLETED yet.
    """
    bridge = _make_bridge()

    captured_payload = []

    async def _intercept(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        captured_payload.append((operation_id, msg_id))
        return {"id": msg_id, "ok": True, "data": "hierarchy_result"}

    bridge._send_with_retry = _intercept

    await bridge.send("get_hierarchy", {})

    # get_command_status with the captured op_id should return COMPLETED
    assert captured_payload, "No send intercepted"
    op_id, _ = captured_payload[0]
    assert op_id, "operation_id must be passed to _send_with_retry"

    status, result = bridge.get_command_status(op_id)
    assert status == CommandStatus.COMPLETED, f"Expected COMPLETED, got {status}"
    assert result is not None, "COMPLETED entry should cache the result"


# ---------------------------------------------------------------------------
# Test 4 — state persists across reconnect (ledger survives close/reconnect)
# ---------------------------------------------------------------------------

async def test_delivery_state_after_disconnect():
    """CommandLedger entries persist after bridge close/reconnect.

    The ledger lives on the bridge instance and must survive a TCP reconnect
    so a caller can query op fate after disconnect.

    Red: Without a persistent ledger, any state is lost after reconnect.
    """
    bridge = _make_bridge()

    captured_payload = []

    async def _intercept(cmd, payload, msg_id, timeout, deadline, operation_id=""):
        captured_payload.append(operation_id)
        return {"id": msg_id, "ok": True, "data": "ok"}

    bridge._send_with_retry = _intercept

    # Send a command — it succeeds and is recorded as COMPLETED
    await bridge.send("get_hierarchy", {})
    op_id = captured_payload[0]
    assert op_id

    # Simulate disconnect + reconnect by calling close() and resetting state
    await bridge.close()
    bridge._state = bridge_mod.BridgeState.DISCONNECTED

    # The ledger must still have the COMPLETED entry
    status, _ = bridge.get_command_status(op_id)
    assert status == CommandStatus.COMPLETED, (
        f"Ledger entry must survive disconnect; got {status}"
    )
