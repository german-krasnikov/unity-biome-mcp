"""MCP-TRANS-008: CommandLedger recovery cycle edge cases.

Tests the full admitted→executed→queried-after-reconnect cycle not covered
by the basic delivery-state tests in test_bridge_delivery_state.py.

All tests are pure ledger operations — no live TCP connection required.
"""
import pytest

from unity_mcp.bridge import CommandStatus, UnityBridge
import unity_mcp.bridge as bridge_mod


def _bridge() -> UnityBridge:
    return UnityBridge(port=9999)


def test_ledger_accepted_then_responded_is_terminal():
    """ACCEPTED → COMPLETED transition is terminal; result is cached."""
    bridge = _bridge()
    op_id = "op-responded-1"
    result_payload = {"ok": True, "data": "hierarchy_result"}

    bridge._ledger.record(op_id, CommandStatus.ACCEPTED)
    bridge._ledger.record(op_id, CommandStatus.COMPLETED, result_payload)

    status, cached = bridge.get_command_status(op_id)
    assert status == CommandStatus.COMPLETED
    assert cached == result_payload


def test_ledger_accepted_then_disconnect_is_unknown():
    """Writer accepted the frame but no response arrived — fate is unknown."""
    bridge = _bridge()
    op_id = "op-disconnect-2"

    bridge._ledger.record(op_id, CommandStatus.ACCEPTED)
    # No COMPLETED record — simulates transport drop before response

    status, result = bridge.get_command_status(op_id)
    assert status == CommandStatus.ACCEPTED, (
        "In-flight command must stay ACCEPTED (not NOT_FOUND, not COMPLETED)"
    )
    assert result is None


async def test_ledger_full_recovery_cycle():
    """Ledger entry survives bridge.close() + state reset; op is ACCEPTED after reconnect."""
    bridge = _bridge()
    op_id = "op-recovery-3"

    bridge._ledger.record(op_id, CommandStatus.ACCEPTED)

    # Simulate transport disconnect
    await bridge.close()
    bridge._state = bridge_mod.BridgeState.DISCONNECTED

    # Query after reconnect — ledger lives on the bridge instance, not the socket
    status, result = bridge.get_command_status(op_id)
    assert status == CommandStatus.ACCEPTED, (
        "Ledger must survive close/reconnect; in-flight op not silently lost"
    )
    assert result is None
