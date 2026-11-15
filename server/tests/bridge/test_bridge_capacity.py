"""Tests for CLIENT_CAPACITY_BUSY rejection handling (MCP-CAP-025)."""
import time

import pytest

from unity_mcp.errors import CapacityBusyError
from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.bridge_reload_state import DomainReloadTracker
from unittest.mock import Mock


def _make_policy() -> RetryPolicy:
    return RetryPolicy(
        probe=Mock(has_strong_busy_signal=Mock(return_value=False),
                   is_process_dead=Mock(return_value=False)),
        reload=DomainReloadTracker(),
        is_retry_safe=lambda cmd: False,
        max_retries=3,
    )


def test_capacity_busy_classified_as_retryable():
    """CapacityBusyError must be classified as retryable regardless of is_retry_safe."""
    policy = _make_policy()
    err = CapacityBusyError("at capacity", retry_after_seconds=5.0)
    do_retry, _, reason = policy.decide(
        err, attempt=0, session_deadline=time.monotonic() + 60, cmd="any_cmd"
    )
    assert do_retry is True
    assert reason == "capacity_busy"


def test_capacity_busy_includes_retry_after():
    """Delay returned by decide() must equal error.retry_after_seconds."""
    policy = _make_policy()
    err = CapacityBusyError("at capacity", retry_after_seconds=7.0)
    _, delay, _ = policy.decide(
        err, attempt=0, session_deadline=time.monotonic() + 60, cmd="any_cmd"
    )
    assert delay == pytest.approx(7.0)


def test_capacity_busy_not_retried_after_max_retries():
    """CapacityBusyError respects max_retries limit."""
    policy = _make_policy()
    err = CapacityBusyError("at capacity", retry_after_seconds=5.0)
    do_retry, _, reason = policy.decide(
        err, attempt=3, session_deadline=time.monotonic() + 60, cmd="any_cmd"
    )
    assert do_retry is False
    assert reason == "max_retries"


def test_capacity_busy_error_attributes():
    """CapacityBusyError carries capacity/active metadata."""
    err = CapacityBusyError("at capacity", retry_after_seconds=5.0, capacity=8, active=8)
    assert err.retry_after_seconds == 5.0
    assert err.capacity == 8
    assert err.active == 8
