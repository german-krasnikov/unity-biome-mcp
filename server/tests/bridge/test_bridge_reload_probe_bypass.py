"""Defect 1 (LIVE-DEFECTS reload guard): send()'s pre-queue reload guard at
bridge.py must let retry-safe probes (compile_status/sync_status/get_status)
reach the transport while a domain reload is marked active -- they are the
only way callers like runtime._await_reload_idle can detect reload-end.
Non-retry-safe (mutating) commands must still be blocked outright (S8:
no unsafe send while a reload is active).
"""
import json
import struct
import time
from unittest.mock import AsyncMock, patch

from unity_mcp.bridge import DomainReloadError, UnityBridge
from helpers import make_idle_probe, make_writer


def _frame(obj: dict) -> tuple[bytes, bytes]:
    payload = json.dumps(obj).encode("utf-8")
    return struct.pack("!I", len(payload)), payload


def _make_bridge(is_retry_safe) -> UnityBridge:
    return UnityBridge(probe=make_idle_probe(), is_retry_safe=is_retry_safe)


async def test_send_retry_safe_probe_bypasses_active_reload_guard():
    """compile_status is retry-safe: an active DomainReloadTracker must NOT
    short-circuit it at the pre-queue guard -- it must reach the transport
    and return the response. This is the assertion that flips red if the
    bypass condition is removed from bridge.py's send()."""
    reader = AsyncMock()
    writer = make_writer()
    header, payload = _frame({"id": "0001", "ok": True, "data": "idle|1.0"})
    reader.readexactly.side_effect = [header, payload]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "compile_status")
        await bridge.connect()
        bridge._reload.mark()
        assert bridge._reload.is_active() is True

        result = await bridge.send("compile_status", {})

    assert result == {"id": "0001", "ok": True, "data": "idle|1.0"}
    writer.write.assert_called()  # transport was touched, not short-circuited


async def test_send_non_retry_safe_command_still_blocked_during_reload():
    """set_property is a mutating write, never retry-safe: an active reload
    must raise DomainReloadError immediately without touching the transport
    at all (S8 no-unsafe-send-during-reload negative control)."""
    reader = AsyncMock()
    writer = make_writer()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "compile_status")
        await bridge.connect()
        bridge._reload.mark()

        raised = None
        try:
            await bridge.send("set_property", {"path": "/Foo", "field": "x", "value": "1"})
        except DomainReloadError as exc:
            raised = exc

    assert raised is not None
    writer.write.assert_not_called()


async def test_send_retry_safe_probe_bypasses_guard_after_heartbeat_remark():
    """Heartbeat re-marks the tracker via should_retry() on a ping
    DomainReloadError; the retry-safe probe must still bypass the guard
    afterward -- the fix is not sensitive to who called mark()."""
    reader = AsyncMock()
    writer = make_writer()
    header, payload = _frame({"id": "0001", "ok": True, "data": "idle|1.0"})
    reader.readexactly.side_effect = [header, payload]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "compile_status")
        await bridge.connect()

        # Simulate the heartbeat's ping-failure handling re-marking the tracker.
        bridge.should_retry(
            DomainReloadError("ping saw reload"), attempt=0,
            session_deadline=time.monotonic() + 30, cmd="get_status",
        )
        assert bridge._reload.is_active() is True

        result = await bridge.send("compile_status", {})

    assert result == {"id": "0001", "ok": True, "data": "idle|1.0"}
