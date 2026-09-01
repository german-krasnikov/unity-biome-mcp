"""Run boundary correlation: expected_count in ACK, zero-test guard,
expected_count enrichment in get_test_run, uncorrelated result detection."""

import json
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.testing as testing
from unity_mcp.tools.run_handle import TestRunRegistry

REQUEST_ID = "req-boundary-1"
RUN_ID = "run-boundary-1"
UTF_GUID = "utf-boundary-1"


def _ack(expected_count: int | None = 42) -> str:
    base = (
        f"tests-started|request_id={REQUEST_ID}|run_id={RUN_ID}"
        f"|utf_guid={UTF_GUID}|state=dispatched"
    )
    if expected_count is not None:
        base += f"|expected_count={expected_count}"
    return base



async def test_run_tests_ack_includes_expected_count():
    """After dispatch with expected_count in ACK, handle stores that count."""
    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=_ack(42))),
        patch.object(testing, "_registry", registry),
        patch.object(testing, "_preflight", AsyncMock(return_value=None)),
        patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")),
    ):
        result = await testing.run_tests(mode="EditMode", request_id=REQUEST_ID)

    assert result == _ack(42)
    handle = registry.get(RUN_ID)
    assert handle is not None
    assert handle.expected_count == 42



async def test_run_boundary_zero_expected_is_error():
    """ACK with expected_count=0 raises ToolError; run is never registered."""
    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=_ack(0))),
        patch.object(testing, "_registry", registry),
        patch.object(testing, "_preflight", AsyncMock(return_value=None)),
        patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")),
    ):
        with pytest.raises(ToolError, match="Empty manifest"):
            await testing.run_tests(mode="EditMode", request_id=REQUEST_ID)

    assert registry.get(RUN_ID) is None



async def test_run_tests_zero_match_message_has_no_metachar_hint():
    """A plain filter with an honest zero-match gets no metachar hint."""
    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=_ack(0))),
        patch.object(testing, "_registry", registry),
        patch.object(testing, "_preflight", AsyncMock(return_value=None)),
        patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")),
    ):
        with pytest.raises(ToolError) as exc_info:
            await testing.run_tests(
                mode="EditMode", filter="NoSuchClass", request_id=REQUEST_ID
            )

    message = str(exc_info.value)
    assert "Empty manifest" in message
    assert "regex metacharacter" not in message


async def test_run_tests_zero_match_message_includes_metachar_hint():
    """A nested-class filter with a '+' gets the regex-metachar hint."""
    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=_ack(0))),
        patch.object(testing, "_registry", registry),
        patch.object(testing, "_preflight", AsyncMock(return_value=None)),
        patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")),
    ):
        with pytest.raises(ToolError) as exc_info:
            await testing.run_tests(
                mode="EditMode",
                filter="StatSheetTests+OrderIndependence",
                request_id=REQUEST_ID,
            )

    message = str(exc_info.value)
    assert "Empty manifest" in message
    assert "regex metacharacter" in message



async def test_get_test_run_includes_expected_from_handle():
    """get_test_run enriches non-terminal response with expected_count from handle."""
    registry = TestRunRegistry()
    handle = registry.register(RUN_ID, REQUEST_ID)
    handle.expected_count = 99

    raw = json.dumps({
        "run_id": RUN_ID,
        "request_id": REQUEST_ID,
        "state": "running",
        "lifecycle": "running",
        "passed": 5,
        "failed": 0,
    })

    with (
        patch.object(testing, "_send", AsyncMock(return_value=raw)),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    data = json.loads(result)
    assert data.get("expected_count") == 99



async def test_results_without_run_id_classified_as_uncorrelated():
    """get_test_run flags results whose run_id mismatches the requested run."""
    registry = TestRunRegistry()
    registry.register(RUN_ID, REQUEST_ID)

    wrong_result = json.dumps({
        "run_id": "other-run-id",
        "request_id": REQUEST_ID,
        "state": "running",
    })

    with (
        patch.object(testing, "_send", AsyncMock(return_value=wrong_result)),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert "UNCORRELATED" in result


def _terminal_snapshot_json(expected_count: int, filter_name: str = "") -> str:
    """A structurally-complete terminal snapshot; ARC-3's exact bug shape at
    expected_count=0 (outcome=incomplete, issues=[], every flag honestly True)."""
    return json.dumps({
        "run_id": RUN_ID,
        "request_id": REQUEST_ID,
        "state": "terminal",
        "lifecycle": "terminal",
        "outcome": "passed" if expected_count else "incomplete",
        "source": "mcp",
        "mode": "EditMode",
        "filter": filter_name,
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": True,
        "build_coherent": True,
        "utf_guid": UTF_GUID,
        "utf_xml_scope": "complete",
        "expected_count": expected_count,
        "declared_expected_count": expected_count,
        "readable_manifest_count": expected_count,
        "completed_expected_count": expected_count,
        "unique_terminal_count": expected_count,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": expected_count,
        "failed": 0,
        "skipped": 0,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
    })


async def test_get_test_run_rejects_zero_match_terminal_snapshot():
    """DEV-09 gap: a direct get_test_run on a terminal zero-match snapshot
    must fail closed instead of handing back a snapshot that looks passed."""
    registry = TestRunRegistry()
    raw = _terminal_snapshot_json(expected_count=0)

    with (
        patch.object(testing, "_send", AsyncMock(return_value=raw)),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert result.startswith(f"PROTOCOL-ERROR|run_id={RUN_ID}")
    assert "reason=run-zero-match-filter" in result


async def test_get_test_run_passes_through_healthy_terminal_snapshot():
    """Arm B: fail-closing must be specific to invalid evidence, not every
    terminal snapshot -- an honest passing run comes back unchanged."""
    registry = TestRunRegistry()
    raw = _terminal_snapshot_json(expected_count=1)

    with (
        patch.object(testing, "_send", AsyncMock(return_value=raw)),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert result == raw


async def test_get_test_run_rejects_zero_match_from_cache():
    """The cache-hit path (a handle already holding a terminal .result) must
    be validated too, not returned raw straight from the registry."""
    registry = TestRunRegistry()
    handle = registry.register(RUN_ID, REQUEST_ID)
    handle.result = _terminal_snapshot_json(expected_count=0)

    with (
        patch.object(
            testing, "_send", AsyncMock(side_effect=AssertionError("must not fetch"))
        ),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert result.startswith(f"PROTOCOL-ERROR|run_id={RUN_ID}")
    assert "reason=run-zero-match-filter" in result
