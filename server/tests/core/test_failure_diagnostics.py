"""Tests for typed auto-recovery diagnostics (MCP-DIAG-009).

Verifies that categorize_failure() returns a typed FailureCategory
instead of a generic 'unknown' for every known exception type.
"""
import asyncio

import pytest

from unity_mcp.errors import (
    CapacityBusyError,
    FailureCategory,
    SessionIdentityMismatch,
    categorize_failure,
)


def test_classify_transport_closed():
    exc = ConnectionError("TCP connection lost")
    category, detail = categorize_failure(exc)
    assert category == FailureCategory.TRANSPORT_CLOSED
    assert "FAIL:transport_closed" == f"FAIL:{category.value}"


def test_classify_capacity_busy():
    exc = CapacityBusyError("at capacity", retry_after_seconds=5.0, capacity=1, active=1)
    category, detail = categorize_failure(exc)
    assert category == FailureCategory.CAPACITY_BUSY
    assert "FAIL:capacity_busy" == f"FAIL:{category.value}"


def test_classify_session_mismatch():
    exc = SessionIdentityMismatch("project changed")
    category, detail = categorize_failure(exc)
    assert category == FailureCategory.SESSION_MISMATCH
    assert "FAIL:session_mismatch" == f"FAIL:{category.value}"


def test_classify_timeout():
    for exc in [TimeoutError("timed out"), asyncio.TimeoutError()]:
        category, detail = categorize_failure(exc)
        assert category == FailureCategory.TIMEOUT, f"Expected TIMEOUT for {type(exc).__name__}"
        assert "FAIL:timeout" == f"FAIL:{category.value}"


def test_classify_unknown_preserves_original():
    exc = ValueError("some unexpected error")
    category, detail = categorize_failure(exc)
    assert category == FailureCategory.UNKNOWN
    assert "some unexpected error" in detail


def test_all_error_types_have_classification():
    """Known exception types must NOT map to UNKNOWN — each gets its own category."""
    known_errors = [
        (ConnectionError("generic"), FailureCategory.TRANSPORT_CLOSED),
        (CapacityBusyError("busy"), FailureCategory.CAPACITY_BUSY),
        (SessionIdentityMismatch("mismatch"), FailureCategory.SESSION_MISMATCH),
        (TimeoutError(), FailureCategory.TIMEOUT),
        (asyncio.TimeoutError(), FailureCategory.TIMEOUT),
    ]
    for exc, expected in known_errors:
        category, _ = categorize_failure(exc)
        assert category == expected, (
            f"{type(exc).__name__} mapped to {category} instead of {expected}"
        )
        assert category != FailureCategory.UNKNOWN, (
            f"{type(exc).__name__} should not be UNKNOWN"
        )
