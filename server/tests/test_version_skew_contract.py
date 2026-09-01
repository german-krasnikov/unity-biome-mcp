"""ARC-17 X1: locks the mixed-version contract for `health` + `expected_count`.

Old plugin + new server must degrade to pre-ARC-1/pre-ARC-3 behavior: a
missing `health` key is never a `KeyError` and falls back to the boundary-flag
detection that existed before ARC-1; a missing `expected_count` key is never a
`KeyError` either. No new wire combination is invented here -- see
`Plans/consumer-reports/ARC-17-version-skew-matrix.md` Task X1 and its own
`Plans/consumer-reports/ARC-0a-test-conventions.md` §4 standing rule
(new field -> read with `.get(key, default)`, never bare indexing).

This is a compat *lock*, not a bugfix: production code (`testing.py`) is
already correct (ARC-1 P1/P2, ARC-3 P1/P2 merged) -- all three tests are
expected green immediately. Regression proof is the fault-injection arm
described in each test's docstring (swap `.get()` for bare indexing, confirm
RED, revert) -- performed manually and recorded in the PR, not committed.
"""

import json
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.testing as testing
from unity_mcp.tools.run_handle import TestRunRegistry

REQUEST_ID = "req-skew-1"
RUN_ID = "run-skew-1"
UTF_GUID = "utf-skew-1"


def _new_plugin_terminal_snapshot(
    *, outcome: str = "passed", expected_count: int = 1
) -> dict:
    """Today's real (post-ARC-1/ARC-3) plugin output for a terminal run:
    `health="healthy"`, non-zero `expected_count`, no issues. Every count
    invariant is honestly self-consistent so the validator has nothing else
    to reject."""
    run_finished_observed = outcome != "incomplete"
    return {
        "request_id": REQUEST_ID,
        "run_id": RUN_ID,
        "utf_guid": UTF_GUID,
        "state": "terminal",
        "lifecycle": "terminal",
        "outcome": outcome,
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "health": "healthy",
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": run_finished_observed,
        "build_coherent": True,
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
    }


def test_pre_batch_shaped_terminal_snapshot_is_unaffected():
    """Today's real plugin output must pass through clean -- exact `None`,
    not merely falsy. Pins the happy path this whole batch must not disturb."""
    snapshot = _new_plugin_terminal_snapshot()

    reason = testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    )

    assert reason is None


def test_missing_health_key_degrades_to_pre_arc1_reason():
    """Old plugin never emits `health` at all. An abandoned/incomplete run
    must still be caught by the pre-ARC-1 boundary-flag path -- not a
    `KeyError` -- with the exact same reason string that path always gave.

    Fault-injection arm (manual, not committed): change
    `health = snapshot.get("health")` at `testing.py` (`_terminal_snapshot_error`)
    to bare `snapshot["health"]`. This test must go RED with `KeyError`.
    Revert after confirming.
    """
    snapshot = _new_plugin_terminal_snapshot(outcome="incomplete")
    del snapshot["health"]

    reason = testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    )

    assert reason == "run-finish-boundary-missing"


async def test_missing_expected_count_key_never_raises():
    """A snapshot that is otherwise fully valid but missing `expected_count`
    (an even-older plugin shape) must fail closed through `.get()`, never
    raise, and `get_test_run` must still hand back a coherent result built
    from the same snapshot.

    Fault-injection arm (manual, not committed): change
    `expected = snapshot.get("expected_count")` to bare
    `snapshot["expected_count"]`. This test must go RED with `KeyError`.
    Revert after confirming.
    """
    snapshot = _new_plugin_terminal_snapshot()
    del snapshot["expected_count"]

    reason = testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    )
    assert reason == "expected-count-invalid"

    registry = TestRunRegistry()
    with (
        patch.object(testing, "_send", AsyncMock(return_value=json.dumps(snapshot))),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert result.startswith(f"PROTOCOL-ERROR|run_id={RUN_ID}")
    assert "reason=expected-count-invalid" in result
