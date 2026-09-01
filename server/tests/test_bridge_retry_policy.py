"""RetryPolicy unit tests plus the bridge's delivery-state retry boundary."""
import asyncio
import json
import struct
import time
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_module
from unity_mcp.bridge import BridgeState, CommandStatus, UnityBridge
from unity_mcp.bridge_reload_state import DomainReloadTracker
from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.bridge_socket import DomainReloadError
from unity_mcp.errors import UncertainDeliveryError


def _make_policy(is_retry_safe=None, probe=None) -> RetryPolicy:
    return RetryPolicy(
        probe=probe or Mock(has_strong_busy_signal=Mock(return_value=False)),
        reload=DomainReloadTracker(),
        is_retry_safe=is_retry_safe or (lambda cmd: False),
        max_retries=3,
    )


def test_decide_leaves_timeout_delivery_safety_to_bridge():
    """RetryPolicy has no delivery state, so it must not guess whether a
    timeout happened before or after writer acceptance. UnityBridge owns that gate.
    """
    policy = _make_policy(is_retry_safe=lambda cmd: False)
    do_retry, _, reason = policy.decide(
        asyncio.TimeoutError(), attempt=0,
        session_deadline=time.monotonic() + 60, cmd="execute_code")
    assert do_retry is True and reason == "transient"


def test_decide_allows_safe_command_on_timeout():
    policy = _make_policy(is_retry_safe=lambda cmd: cmd == "get_console")
    do_retry, _, _ = policy.decide(
        asyncio.TimeoutError(), attempt=0,
        session_deadline=time.monotonic() + 60, cmd="get_console")
    assert do_retry is True


def test_allow_hint_retry_delegates_to_is_retry_safe():
    """The hint path and UnityBridge SENT gate use the same annotation-derived
    predicate, so a second response path cannot invent retry safety."""
    policy = _make_policy(is_retry_safe=lambda cmd: cmd == "get_hierarchy")
    assert policy.allow_hint_retry("get_hierarchy") is True
    assert policy.allow_hint_retry("create_object") is False


def test_decide_does_not_mark_reload_tracker_on_domain_reload_error():
    """RetryPolicy.decide() is a pure decision function — it does not call mark().

    Mark side-effects are the caller's responsibility (UnityBridge.should_retry).
    Double-marking was the bug: both _send_with_retry and RetryPolicy called mark().
    """
    tracker = DomainReloadTracker()
    probe = Mock(mark_recompile_issued=Mock())
    policy = RetryPolicy(probe=probe, reload=tracker,
                          is_retry_safe=lambda cmd: True, max_retries=3)
    policy.decide(DomainReloadError("test"), attempt=0,
                  session_deadline=time.monotonic() + 60, cmd="x")
    assert tracker.is_active() is False  # decide() no longer marks; should_retry() does


@pytest.mark.parametrize(
    "failure",
    [
        DomainReloadError("reload after dispatch"),
        OSError("socket lost after dispatch"),
        asyncio.TimeoutError("response timeout after dispatch"),
    ],
    ids=["domain-reload", "os-error", "timeout"],
)
async def test_sent_unsafe_source_patch_is_never_resent(failure):
    """Independent oracle: count actual frames, not policy decisions/attempts.

    ``source_patch_write`` is an internal mutating command and is deliberately
    absent from the read/idempotent annotation set, so the default is unsafe.
    """
    probe = Mock()
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    bridge = UnityBridge(port=9999, probe=probe)
    bridge._crash_log = Mock()
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()
    bridge._writer = writer
    bridge._reader = MagicMock()
    bridge._state = BridgeState.CONNECTED

    frames: list[dict] = []

    def count_frame(_writer, raw: bytes) -> None:
        frames.append(json.loads(raw.decode("utf-8")))

    async def fail_after_send():
        raise failure

    payload = json.dumps({
        "id": "0001",
        "cmd": "source_patch_write",
        "args": {"path": "Assets/Target.cs"},
        "op_id": "unsafe-op",
    }).encode("utf-8")

    with (
        patch.object(bridge_module, "frame_write", side_effect=count_frame),
        patch.object(bridge, "_read_response", side_effect=fail_after_send),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        with pytest.raises(ConnectionError, match="outcome is uncertain"):
            await bridge._send_with_retry(
                "source_patch_write", payload, "0001", 1.0,
                time.monotonic() + 60, "unsafe-op",
            )

    assert len(frames) == 1
    assert frames[0]["op_id"] == "unsafe-op"
    assert "retry_op_id" not in frames[0]
    assert bridge.get_command_status("unsafe-op")[0] is CommandStatus.ACCEPTED
    if isinstance(failure, DomainReloadError):
        assert bridge._state is BridgeState.DOMAIN_RELOADING
        assert bridge._reload.is_active() is True
        probe.mark_recompile_issued.assert_called_once()
        assert bridge._crash_log.log_disconnect.call_args.kwargs["unity_busy"] is True


async def test_unsafe_write_becomes_sent_before_drain_failure():
    """writer.write may reach transport before drain reports its OSError."""
    bridge = UnityBridge(port=9999)
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock(side_effect=[OSError("drain failed"), None])
    bridge._writer = writer
    bridge._reader = MagicMock()
    bridge._state = BridgeState.CONNECTED
    frames: list[bytes] = []
    payload = json.dumps({
        "id": "0001", "cmd": "source_patch_write", "args": {},
        "op_id": "unsafe-op",
    }).encode("utf-8")

    with (
        patch.object(
            bridge_module, "frame_write",
            side_effect=lambda _writer, raw: frames.append(raw),
        ),
        patch.object(
            bridge, "_read_response", new_callable=AsyncMock,
            return_value={"id": "0001", "ok": True},
        ),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        with pytest.raises(ConnectionError, match="outcome is uncertain"):
            await bridge._send_with_retry(
                "source_patch_write", payload, "0001", 1.0,
                time.monotonic() + 60, "unsafe-op",
            )

    assert len(frames) == 1
    assert bridge.get_command_status("unsafe-op")[0] is CommandStatus.ACCEPTED


async def test_public_send_corrupt_utf_is_typed_uncertain_delivery():
    """A corrupt post-SENT response cannot bypass the no-resend contract."""
    probe = Mock()
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    bridge = UnityBridge(port=9999, probe=probe)
    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.drain = AsyncMock()
    writer.get_extra_info.return_value = None
    writer.wait_closed = AsyncMock()
    reader = MagicMock()
    corrupt = b"\xff"
    reader.readexactly = AsyncMock(
        side_effect=[struct.pack("!I", len(corrupt)), corrupt],
    )
    bridge._writer = writer
    bridge._reader = reader
    bridge._state = BridgeState.CONNECTED
    frames: list[dict] = []

    def capture_frame(_writer, raw: bytes) -> None:
        frames.append(json.loads(raw.decode("utf-8")))

    with (
        patch.object(bridge_module, "frame_write", side_effect=capture_frame),
        patch.object(bridge, "close", new_callable=AsyncMock),
    ):
        with pytest.raises(UncertainDeliveryError) as caught:
            await bridge.send("source_patch_write", {"path": "Assets/Target.cs"})

    assert len(frames) == 1
    assert caught.value.cmd == "source_patch_write"
    assert caught.value.op_id == frames[0]["op_id"]
    assert caught.value.delivery is CommandStatus.ACCEPTED
    assert bridge.get_command_status(caught.value.op_id)[0] is CommandStatus.ACCEPTED
    await bridge.close()
