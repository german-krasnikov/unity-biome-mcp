"""Locks the mixed-version contract for `health` + `expected_count`.

Old plugin + new server must degrade to inert, pre-batch behavior, per the
standing rule ("old plugin: field absent -> inert as today"):

- A missing `health` key is never a `KeyError` and never fabricates a reason
  on an otherwise honestly-finished run -- the branch is a silent no-op.
- A missing `expected_count` key is never a `KeyError` either, and -- since
  a later fix -- is no longer treated as a corrupted value: the
  count-invariant checks that depend on it are skipped entirely, and the
  rest of the validation (boundary flags, outcome, issues) still applies.
  A *present but corrupted* `expected_count` (bool, non-int) still fails
  closed exactly as before -- the None-skip widens only the absent-key case.

See `Plans/consumer-reports/ARC-17-version-skew-matrix.md` Task X1 (and its
§4 table row for this fix) and
`Plans/consumer-reports/ARC-0a-test-conventions.md` §4 standing rule (new
field -> read with `.get(key, default)`, never bare indexing).

This is a compat lock for `health` and a compat *fix* for `expected_count`
(a later review found the original .get()-default for `expected_count` still
fail-closed on a merely-absent key, which is not inert). Regression proof is
the fault-injection arm described in each test's docstring -- performed
manually and recorded in the PR, not committed.
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


def test_missing_health_key_is_inert_when_run_finished_cleanly():
    """Old plugin never emits `health` at all. On an otherwise fully-finished
    run (every boundary flag True), its absence must be a silent no-op --
    not a `KeyError`, and it must not fabricate a reason.

    Discriminates (Arm B): an `outcome="incomplete"` snapshot would surface
    `run-finish-boundary-missing` via the boundary-flag check regardless of
    whether `health` exists or is even read at all -- that shape can't tell
    "the health branch tolerates absence" from "the health branch never ran".
    Only a run with `run_finished_observed=True` (every other gate open)
    isolates the health branch itself.

    Fault-injection arm (manual, not committed): change
    `health = snapshot.get("health")` at `testing.py` (`_terminal_snapshot_error`)
    to bare `snapshot["health"]`. This test must go RED with `KeyError`.
    Revert after confirming.
    """
    snapshot = _new_plugin_terminal_snapshot(outcome="passed")
    assert snapshot["run_finished_observed"] is True
    del snapshot["health"]

    reason = testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    )

    assert reason is None


async def test_missing_expected_count_key_degrades_to_inert_pass_through():
    """Old plugin never sends `expected_count` at all. Per ARC-17 §4, an
    absent field must be inert: the count-invariant checks that depend on it
    (declared/readable/completed/unique counts, status-total mismatch) are
    skipped entirely, but the rest of the validation (boundary flags,
    outcome, issues) still applies. An otherwise-valid run passes through
    `get_test_run` unchanged -- no `PROTOCOL-ERROR`, no `KeyError`.

    Fault-injection arm (manual, not committed): in `_terminal_snapshot_error`,
    remove the `expected is not None` guard so a `None` `expected_count`
    falls through to the type check again (i.e. restore the pre-fix
    unconditional `expected-count-invalid` on absence). This test must go RED
    (`reason is None` fails; `result == raw` fails). Revert after confirming.
    """
    snapshot = _new_plugin_terminal_snapshot()
    del snapshot["expected_count"]

    reason = testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    )
    assert reason is None

    registry = TestRunRegistry()
    raw = json.dumps(snapshot)
    with (
        patch.object(testing, "_send", AsyncMock(return_value=raw)),
        patch.object(testing, "_registry", registry),
    ):
        result = await testing.get_test_run(RUN_ID)

    assert result == raw


def test_corrupted_expected_count_value_still_invalid():
    """The None-skip widens only the *absent-key* case. A present but
    corrupted `expected_count` -- bool or non-int -- must still fail closed
    with the same reason as always; this is the discriminating counterpart
    to the pass-through test above (proves the widening isn't a blanket
    "any expected_count problem is now inert")."""
    for bad_value in (True, False, "6", 3.5):
        snapshot = _new_plugin_terminal_snapshot()
        snapshot["expected_count"] = bad_value

        reason = testing._terminal_snapshot_error(
            snapshot, mode="EditMode", filter_name=""
        )

        assert reason == "expected-count-invalid", f"bad_value={bad_value!r}"
