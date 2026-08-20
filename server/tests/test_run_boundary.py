"""Run boundary correlation: expected_count in ACK, zero-test guard,
expected_count enrichment in get_test_run, uncorrelated result detection."""

import json
from unittest.mock import AsyncMock, patch

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
    """ACK with expected_count=0 returns error; run is never registered."""
    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=_ack(0))),
        patch.object(testing, "_registry", registry),
        patch.object(testing, "_preflight", AsyncMock(return_value=None)),
        patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")),
    ):
        result = await testing.run_tests(mode="EditMode", request_id=REQUEST_ID)

    assert "Empty manifest" in result or result.startswith("BLOCKED:")
    assert registry.get(RUN_ID) is None



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
