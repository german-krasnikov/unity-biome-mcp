"""Tests for finalization deadlock safeguards in run_tests_wait."""

import json
from unittest.mock import AsyncMock, patch

import pytest

import unity_mcp.tools.testing as testing

REQ = "req-deadlock"
RUN = "run-deadlock"
ACK = (
    f"tests-started|request_id={REQ}|run_id={RUN}"
    "|utf_guid=utf-1|state=dispatched"
)


async def _started(mode, filter=None, request_id=None):
    return ACK


def _terminal_snapshot():
    return json.dumps({
        "request_id": REQ,
        "run_id": RUN,
        "utf_guid": "utf-1",
        "state": "terminal",
        "lifecycle": "terminal",
        "outcome": "passed",
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": True,
        "build_coherent": True,
        "utf_xml_scope": "complete",
        "expected_count": 1,
        "declared_expected_count": 1,
        "readable_manifest_count": 1,
        "completed_expected_count": 1,
        "unique_terminal_count": 1,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": 1,
        "failed": 0,
        "skipped": 0,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
    })


def _finalizing_snapshot():
    return json.dumps({
        "request_id": REQ,
        "run_id": RUN,
        "utf_guid": "utf-1",
        "state": "finalizing",
        "lifecycle": "finalizing",
        "outcome": "",
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "is_terminal": False,
        "execution_finished": True,
        "cleanup_complete": False,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": True,
        "build_coherent": True,
        "utf_xml_scope": "complete",
        "expected_count": 1,
        "declared_expected_count": 1,
        "readable_manifest_count": 1,
        "completed_expected_count": 1,
        "unique_terminal_count": 1,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": 1,
        "failed": 0,
        "skipped": 0,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
    })


@pytest.mark.asyncio
async def test_consecutive_tcp_failures_exit_early():
    """N consecutive TCP errors trigger early exit, not a 1800s loop.

    Polling setup: timeout=1.0, interval=0.1 → 11 attempts max.
    After fix: exits after 5 consecutive failures (poll.await_count == 5 < 11).
    Before fix: runs all 11 attempts (assertion fails → RED).
    """
    poll = AsyncMock(side_effect=ConnectionError("unity reloading"))

    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "get_test_run", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ,
            timeout=1.0,
            poll_interval=0.1,
        )

    assert "reason=tcp-consecutive-failures" in result, (
        f"Expected reason=tcp-consecutive-failures in result: {result!r}"
    )
    # Fail-fast: must not exhaust all 11 attempts
    assert poll.await_count < 11, (
        f"Expected early exit after consecutive failures, "
        f"but poll was called {poll.await_count} times"
    )


@pytest.mark.asyncio
async def test_single_failure_recovers():
    """Single TCP failure recovers: existing behavior must not regress."""
    polls = [ConnectionError("domain reload"), _terminal_snapshot()]

    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "get_test_run", AsyncMock(side_effect=polls)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=3.0, poll_interval=1.0
        )

    decoded = json.loads(result)
    assert decoded["outcome"] == "passed"
    assert decoded["run_id"] == RUN


@pytest.mark.asyncio
async def test_stuck_finalizing_returns_after_timeout(monkeypatch):
    """Stuck finalizing (C# reflection failure) returns TIMEOUT with reason.

    Monkeypatching _FINALIZATION_STUCK_TIMEOUT=0.0 means the second poll
    always sees elapsed >= 0.0 and returns immediately.
    Before fix: loops until wall-clock timeout.
    """
    monkeypatch.setattr(testing, "_FINALIZATION_STUCK_TIMEOUT", 0.0)

    poll = AsyncMock(return_value=_finalizing_snapshot())

    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "get_test_run", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=60.0, poll_interval=0.001,
        )

    assert result.startswith("TIMEOUT|"), f"Expected TIMEOUT, got: {result!r}"
    assert "reason=finalization-timeout" in result, (
        f"Expected reason=finalization-timeout in: {result!r}"
    )
    assert RUN in result
