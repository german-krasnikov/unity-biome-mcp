"""TDD tests for 3 SonarCloud reliability fixes in bridge.py / bridge_retry.py.

Bug 1: _read_response() non-dict JSON → AttributeError (should be ConnectionError)
Bug 2: close() queue drain loses op_id → ledger not updated to FAILED
Bug 3: RetryPolicy.decide() calls _reload.mark() — duplicate of _send_with_retry's call
"""
import asyncio
import json
import struct
import time
from unittest.mock import AsyncMock, Mock, patch
import pytest

from unity_mcp.bridge import UnityBridge, CommandStatus, DomainReloadError
from unity_mcp.bridge_retry import RetryPolicy


def _frame(data: bytes) -> tuple[bytes, bytes]:
    return struct.pack("!I", len(data)), data


# ---------------------------------------------------------------------------
# Bug 1: non-dict JSON must raise ConnectionError, not AttributeError
# ---------------------------------------------------------------------------

async def test_read_response_non_dict_json_raises_connection_error():
    """_read_response() with JSON null raises ConnectionError, not AttributeError."""
    bridge = UnityBridge(port=9999)

    null_header, null_payload = _frame(b"null")
    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(side_effect=[null_header, null_payload])
    bridge._reader = mock_reader

    with pytest.raises(ConnectionError, match="non-dict"):
        await bridge._read_response()


# ---------------------------------------------------------------------------
# Bug 2: close() drain must update ledger to FAILED for drained op_ids
# ---------------------------------------------------------------------------

async def test_close_drain_updates_ledger_to_failed():
    """Items drained from queue during close() must be recorded as FAILED in the ledger."""
    bridge = UnityBridge(port=9999)

    op_id_1 = "op-aaa"
    op_id_2 = "op-bbb"
    future1: asyncio.Future = asyncio.get_event_loop().create_future()
    future2: asyncio.Future = asyncio.get_event_loop().create_future()

    # Tuple: (cmd, payload, msg_id, timeout, session_deadline, operation_id, future)
    await bridge._send_queue.put(("cmd1", b"pay1", "m1", 30.0, 99999.0, op_id_1, future1))
    await bridge._send_queue.put(("cmd2", b"pay2", "m2", 30.0, 99999.0, op_id_2, future2))

    bridge._writer = None
    bridge._reader = None

    await bridge.close()

    status1, _ = bridge.get_command_status(op_id_1)
    status2, _ = bridge.get_command_status(op_id_2)

    assert status1 == CommandStatus.FAILED, f"Expected FAILED for {op_id_1}, got {status1}"
    assert status2 == CommandStatus.FAILED, f"Expected FAILED for {op_id_2}, got {status2}"
    assert future1.done() and isinstance(future1.exception(), ConnectionError)
    assert future2.done() and isinstance(future2.exception(), ConnectionError)


# ---------------------------------------------------------------------------
# Bug 3: RetryPolicy.decide() must NOT call _reload.mark() (side-effect-free)
# ---------------------------------------------------------------------------

def test_retry_policy_decide_does_not_call_reload_mark():
    """RetryPolicy.decide() must be side-effect-free for mark() calls.

    _send_with_retry is the sole owner of _reload.mark() on DomainReloadError.
    Having it in both places causes double-marking.
    """
    mock_reload = Mock()
    mock_probe = Mock()
    mock_probe.has_strong_busy_signal.return_value = False
    mock_probe.is_process_dead.return_value = False

    policy = RetryPolicy(
        probe=mock_probe,
        reload=mock_reload,
        is_retry_safe=lambda _: True,
        max_retries=3,
    )

    error = DomainReloadError("reload")
    policy.decide(error, attempt=0, session_deadline=time.monotonic() + 100)

    mock_reload.mark.assert_not_called()
    mock_probe.mark_recompile_issued.assert_not_called()
