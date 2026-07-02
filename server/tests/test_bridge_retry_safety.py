"""TDD: retry-safety gating via ToolAnnotations (Phase 3, Task 3.2).

A TimeoutError means the request may have already reached Unity and started
executing before the client gave up waiting. Blindly retrying a
non-idempotent command risks duplicate execution (e.g. execute_code running
twice). should_retry() must refuse to retry TimeoutError-class errors for
commands the caller marks unsafe via is_retry_safe.
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


def test_should_retry_blocks_non_idempotent_command_on_timeout():
    bridge = _make_bridge(is_retry_safe=lambda cmd: cmd == "get_console")
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is False
    assert reason == "unsafe_to_retry"


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


def test_should_retry_default_denies_unknown_commands():
    """No is_retry_safe passed -> fail-closed default: unknown/unannotated
    commands are NOT retried on TimeoutError, since a TimeoutError means the
    command may have already reached Unity and started executing."""
    bridge = _make_bridge()
    do_retry, delay, reason = bridge.should_retry(
        asyncio.TimeoutError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is False
    assert reason == "unsafe_to_retry"


def test_should_retry_connection_refused_ignores_retry_safety():
    """ConnectionRefusedError means the command never reached Unity -- always
    safe to retry regardless of idempotency."""
    bridge = _make_bridge(is_retry_safe=lambda cmd: False)  # nothing is "safe"
    do_retry, delay, reason = bridge.should_retry(
        ConnectionRefusedError(), attempt=0, session_deadline=_far_deadline(),
        cmd="execute_code",
    )
    assert do_retry is True
