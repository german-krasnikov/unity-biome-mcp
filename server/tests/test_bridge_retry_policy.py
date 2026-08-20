"""Unit tests for RetryPolicy -- no UnityBridge/socket mocking required (the
whole point of extracting this from UnityBridge, per C8 / SOLID finding S2)."""
import asyncio
import time
from unittest.mock import Mock

from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.bridge_reload_state import DomainReloadTracker
from unity_mcp.bridge_socket import DomainReloadError


def _make_policy(is_retry_safe=None, probe=None) -> RetryPolicy:
    return RetryPolicy(
        probe=probe or Mock(has_strong_busy_signal=Mock(return_value=False)),
        reload=DomainReloadTracker(),
        is_retry_safe=is_retry_safe or (lambda cmd: False),
        max_retries=3,
    )


def test_decide_blocks_unsafe_command_on_timeout():
    policy = _make_policy(is_retry_safe=lambda cmd: False)
    do_retry, _, reason = policy.decide(
        asyncio.TimeoutError(), attempt=0,
        session_deadline=time.monotonic() + 60, cmd="execute_code")
    assert do_retry is False and reason == "unsafe_to_retry"


def test_decide_allows_safe_command_on_timeout():
    policy = _make_policy(is_retry_safe=lambda cmd: cmd == "get_console")
    do_retry, _, _ = policy.decide(
        asyncio.TimeoutError(), attempt=0,
        session_deadline=time.monotonic() + 60, cmd="get_console")
    assert do_retry is True


def test_allow_hint_retry_delegates_to_is_retry_safe():
    """This identity -- allow_hint_retry() and decide()'s TimeoutError branch
    sharing one is_retry_safe callable -- is the actual fix for C1/A1: there is
    now structurally only ONE place a retry-safety decision can be made."""
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
