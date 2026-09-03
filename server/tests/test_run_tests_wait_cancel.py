"""Tests for cancel/filter/timeout edge cases in run_tests_wait (MCP-UTF-001, MCP-UTF-002)."""

import asyncio
import json
import time
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.testing as testing
from helpers import REQUEST_ID, RUN_ID, make_snapshot

ACK = (
    f"{testing._STARTED}|request_id={REQUEST_ID}|run_id={RUN_ID}"
    "|utf_guid=utf-1|state=dispatched"
)


def _snapshot(state: str, outcome: str = "", health: str = "") -> str:
    return make_snapshot(REQUEST_ID, RUN_ID, state, outcome, health=health)


async def _started(mode, filter=None, request_id=None):
    assert request_id == REQUEST_ID
    return ACK


@pytest.mark.asyncio
async def test_wait_cancelled_during_poll_preserves_run_identity():
    """CancelledError from get_test_run mid-poll propagates; never swallowed by the loop."""
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(side_effect=asyncio.CancelledError())), \
         patch("asyncio.sleep", AsyncMock()):
        with pytest.raises(asyncio.CancelledError):
            await testing.run_tests_wait(
                request_id=REQUEST_ID, timeout=5.0, poll_interval=1.0
            )


@pytest.mark.asyncio
async def test_empty_exact_filter_rejected_before_dispatch(monkeypatch):
    """Dispatch happens once; ACK with expected_count=0 returns BLOCKED without registry entry."""
    call_count = {"run_tests": 0}

    async def send(command, args, timeout=None):
        if command == "run_tests":
            call_count["run_tests"] += 1
            request_id = args.get("request_id", "")
            return (
                f"{testing._STARTED}|request_id={request_id}"
                "|run_id=run-empty|utf_guid=utf-1|state=dispatched|expected_count=0"
            )
        return "none"

    monkeypatch.setattr(testing, "_send", send)
    with pytest.raises(ToolError, match="Empty manifest"):
        await testing.run_tests(filter="NonExistent.Exact.Class")

    assert call_count["run_tests"] == 1  # dispatch happened, then post-ACK check blocked it


@pytest.mark.asyncio
async def test_empty_filter_validated_on_zero_expected_count_in_ack(monkeypatch):
    """expected_count=0 in ACK blocks the run and leaves no registry entry for run_id."""
    run_id = "run-zero"

    async def send(command, args, timeout=None):
        if command == "run_tests":
            request_id = args.get("request_id", "")
            return (
                f"{testing._STARTED}|request_id={request_id}"
                f"|run_id={run_id}|utf_guid=utf-1|state=dispatched|expected_count=0"
            )
        return "none"

    monkeypatch.setattr(testing, "_send", send)
    with pytest.raises(ToolError, match="Empty manifest"):
        await testing.run_tests(filter="NoSuchClass")

    # No handle registered because ToolError raised before registry.register()
    assert testing._registry.get(run_id) is None


@pytest.mark.asyncio
async def test_wait_reconnect_mid_poll_resumes_same_run_id():
    """ConnectionError during polling is absorbed; same run_id used for all poll calls."""
    terminal = _snapshot("terminal", "passed")
    polls = [
        ConnectionError("domain reload"),
        ConnectionError("domain reload again"),
        terminal,
    ]
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(side_effect=polls)) as get_run, \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=10.0, poll_interval=1.0
        )

    decoded = json.loads(result)
    assert decoded["run_id"] == RUN_ID
    assert decoded["state"] == "terminal"
    for call in get_run.await_args_list:
        assert call.args[0] == RUN_ID


@pytest.mark.asyncio
async def test_wait_300s_timeout_terminates_within_deadline():
    """Low timeout with mocked sleep: TIMEOUT returned quickly, not after 300s default."""
    running = _snapshot("running")
    started_at = time.monotonic()
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value=running)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=0.05, poll_interval=1.0
        )
    elapsed = time.monotonic() - started_at

    assert result.startswith(f"TIMEOUT|request_id={REQUEST_ID}|run_id={RUN_ID}|")
    assert '"state":"running"' in result
    assert elapsed < 0.5  # never waits 300s


@pytest.mark.asyncio
async def test_run_tests_wait_propagates_tool_error_for_zero_match(monkeypatch):
    """run_tests_wait must not swallow ToolError from zero-match filter."""
    async def send(command, args, timeout=None):
        if command == "run_tests":
            request_id = args.get("request_id", "")
            return (
                f"{testing._STARTED}|request_id={request_id}"
                "|run_id=run-zero-wait|utf_guid=utf-1|state=dispatched|expected_count=0"
            )
        return "none"

    monkeypatch.setattr(testing, "_send", send)
    with pytest.raises(ToolError, match="Empty manifest"):
        await testing.run_tests_wait(filter="NoSuchClass", request_id=REQUEST_ID)


@pytest.mark.asyncio
async def test_expected_count_one_does_not_raise(monkeypatch):
    """ACK with expected_count=1 proceeds normally — no ToolError raised."""
    run_id = "run-one"

    async def send(command, args, timeout=None):
        if command == "run_tests":
            request_id = args.get("request_id", "")
            return (
                f"{testing._STARTED}|request_id={request_id}"
                f"|run_id={run_id}|utf_guid=utf-1|state=dispatched|expected_count=1"
            )
        return "none"

    monkeypatch.setattr(testing, "_send", send)
    result = await testing.run_tests(filter="SomeClass")

    assert "tests-started" in result
    assert testing._registry.get(run_id) is not None
    # cleanup
    testing._registry._handles.pop(run_id, None)
