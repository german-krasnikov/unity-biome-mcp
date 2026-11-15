"""TDD: retry-safety gating via ToolAnnotations (Phase 3, Task 3.2).

UnityBridge owns the DeliveryState-aware exception gate: unsafe SENT frames
never retry, while UNSENT transport recovery is allowed. RetryPolicy remains
delivery-agnostic. The in-band Unity retry hint still uses ToolAnnotations.
"""
import asyncio
import time

from unity_mcp.bridge import UnityBridge, SESSION_TIMEOUT
from helpers import make_idle_probe


def _make_bridge(is_retry_safe=None) -> UnityBridge:
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = False
    return UnityBridge(probe=probe, is_retry_safe=is_retry_safe)


def _far_deadline() -> float:
    return time.monotonic() + SESSION_TIMEOUT


def test_should_retry_policy_does_not_guess_unsafe_timeout_delivery():
    bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "get_console")
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is True
    assert reason == "transient"


def test_should_retry_allows_read_only_command_on_timeout():
    bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "get_console")
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="get_console",
    )
    assert do_retry is True


def test_should_retry_explicit_all_safe_allows_retry():
    """Caller explicitly opts every command in via is_retry_safe=lambda cmd: True
    -> TimeoutError retry proceeds to the existing "transient" branch."""
    bridge = _make_bridge(is_retry_safe=lambda cmd: True)
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is True  # attempt=0 -> falls through to existing "transient" branch


def test_should_retry_default_still_leaves_delivery_gate_to_bridge():
    """The default remains unsafe, but should_retry has no delivery state."""
    bridge = _make_bridge()
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is True
    assert reason == "transient"


def test_should_retry_connection_refused_ignores_retry_safety():
    """ConnectionRefusedError means the command never reached Unity -- always
    safe to retry regardless of idempotency."""
    bridge = _make_bridge(is_retry_safe=lambda cmd: False)  # nothing is "safe"
    do_retry, delay, reason = bridge.should_retry(
        ConnectionRefusedError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is True


# --- A1: hint-path (Unity's {ok:false, retry:N} busy sentinel) retry-safety ---
#
# The in-band "busy, retry me" hint arrives after dispatch, so it has the same
# safety requirement as the bridge exception path's SENT boundary. Both use
# the one annotation-derived is_retry_safe predicate.

async def test_hint_retry_denied_for_unsafe_command():
    """Hint-path retry must respect is_retry_safe -- must NOT loop for a
    command the caller has not marked safe, even though the response looks
    like harmless 'still compiling' backpressure, not a network error."""
    import json
    import struct
    from unittest.mock import AsyncMock, patch
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer

    reader = AsyncMock()
    writer = make_writer()

    busy_response = {"id": "0001", "ok": False, "err": "Unity is compiling", "retry": 100}
    busy_payload = json.dumps(busy_response).encode("utf-8")
    busy_header = struct.pack("!I", len(busy_payload))
    reader.readexactly.side_effect = [busy_header, busy_payload]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
            bridge = UnityBridge(is_retry_safe=lambda cmd: False)
            await bridge.connect()
            result = await bridge.send("execute_code", {})

    assert result["ok"] is False
    assert result.get("retry") == 100
    mock_sleep.assert_not_called()


async def test_hint_retry_allowed_for_safe_command():
    """Hint-path retry proceeds normally for a command explicitly marked safe
    via is_retry_safe -- unchanged behavior, verifies the new gate doesn't
    break the existing auto-retry-on-busy-hint flow."""
    import json
    import struct
    from unittest.mock import AsyncMock, patch
    from unity_mcp.bridge import UnityBridge
    from helpers import make_writer

    reader = AsyncMock()
    writer = make_writer()

    busy_response = {"id": "0001", "ok": False, "err": "Unity is compiling", "retry": 100}
    busy_payload = json.dumps(busy_response).encode("utf-8")
    busy_header = struct.pack("!I", len(busy_payload))

    ok_response = {"id": "0001", "ok": True, "data": "pong"}
    ok_payload = json.dumps(ok_response).encode("utf-8")
    ok_header = struct.pack("!I", len(ok_payload))

    reader.readexactly.side_effect = [busy_header, busy_payload, ok_header, ok_payload]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
            bridge = UnityBridge(is_retry_safe=lambda cmd: True)
            await bridge.connect()
            result = await bridge.send("get_hierarchy", {})

    assert result["ok"] is True
    assert result["data"] == "pong"
    mock_sleep.assert_called_once_with(0.1)
