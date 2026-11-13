"""Unity Test Framework orchestration with durable run identity."""

import asyncio
import contextlib
import json
import math
import re
import uuid
from pathlib import Path  # noqa: TC003 — get_type_hints() requires runtime (test_python314_compat)
from typing import Any

from mcp.server.mcpserver.exceptions import ToolError

from ..compile_state import CompileStateProbe
from ..decisions.generated.terminalerror_logic import (
    TerminalErrorInput,
    TerminalErrorValues,
    terminal_error,
)
from ..generated.testrun_enums import Lifecycle, RunOutcome
from . import run_disk_fallback
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import (  # noqa: F401
    cancel_test_run,
    get_test_count,
    get_test_progress,
    get_test_results,
    get_test_run,
    list_test_runs,
    resolve_test_request,
    run_tests,
    run_tests_wait,
    tools,
)
from .run_handle import TestRunRegistry

_send = None
_args = None
_get_slot = None
_registry = TestRunRegistry()

# STALE-DOMAIN: defensive -- unreachable with expected_compile=False, guards
# future callers that change diagnose semantics.
_BLOCK_STARTS = (
    "FAIL:", "BUILD-FAILED-WEDGE", "STALE-CACHE",
    "STALE-DOMAIN", "STALE-TRANSIENT", "WEDGE-ENGINE", "WEDGE-STATE",
    "REBUILDING", "TESTS-INVISIBLE",
)
_STARTED = "tests-started"
_REQUEST_STATUS = "test-request"
_START_UNKNOWN = "START-UNKNOWN"
# PT-9: generated from dsl/data/testrun.data-contract -- the single source for these two word sets.
_RUN_STATES = frozenset(Lifecycle)
_TERMINAL_OUTCOMES = frozenset(RunOutcome)
_IDENTITY_RE = re.compile(r"^[A-Za-z0-9._-]{1,200}$")
# Mirrors bridge_heartbeat.py's _ping_failures threshold (3): gates a
# diagnostic TIMEOUT suffix only, never control flow.
_HEALTH_STREAK_TIMEOUT_THRESHOLD = 3


def _try_update_handle_from_result(run_id: str, result: str) -> None:
    """Parse result JSON and update registry handle state if terminal."""
    handle = _registry.get(run_id)
    if handle is None:
        return
    try:
        snapshot = json.loads(result)
    except (TypeError, ValueError):
        return
    if not isinstance(snapshot, dict) or snapshot.get("state") != "terminal":
        return
    expected = snapshot.get("expected_count")
    ec = expected if isinstance(expected, int) and not isinstance(expected, bool) else None
    outcome = snapshot.get("outcome", "")
    state = "passed" if outcome == "passed" else "failed" if outcome in ("failed", "dispatch_failed") else "cancelled" if outcome == "cancelled" else "completed"
    handle.update(state, result=result, expected_count=ec)


def _new_request_id() -> str:
    return uuid.uuid4().hex


def _valid_identity(value: str) -> bool:
    return isinstance(value, str) and _IDENTITY_RE.fullmatch(value) is not None


def _parse_fields(value: str, prefix: str) -> dict[str, str] | None:
    """Parse a pipe-delimited protocol record and reject malformed fields."""
    if not isinstance(value, str):
        return None
    parts = value.split("|")
    if not parts or parts[0] != prefix:
        return None
    fields: dict[str, str] = {}
    for part in parts[1:]:
        if "=" not in part:
            return None
        key, field_value = part.split("=", 1)
        if not key or key in fields:
            return None
        fields[key] = field_value
    return fields


def _parse_ack(value: str, expected_request_id: str) -> dict[str, str] | None:
    fields = _parse_fields(value, _STARTED)
    if fields is None:
        return None
    if fields.get("request_id") != expected_request_id:
        return None
    if not fields.get("run_id") or not fields.get("utf_guid"):
        return None
    if fields.get("state") != "dispatched":
        return None
    return fields


def _parse_request_status(
    value: str,
    expected_request_id: str,
) -> dict[str, str] | None:
    """Parse a correlated non-ACK status returned before/after dispatch.

    Unity can reject dispatch after it has durably allocated ``run_id`` or can
    resolve a lost ACK while that run is finalizing. Both are authoritative and
    must be polled by exact run identity instead of being downgraded to an
    ambiguous transport failure.
    """
    fields = _parse_fields(value, _REQUEST_STATUS)
    if fields is None or fields.get("request_id") != expected_request_id:
        return None
    if not fields.get("run_id") or fields.get("state") not in _RUN_STATES:
        return None
    outcome = fields.get("outcome", "")
    if fields["state"] == "terminal" and outcome not in _TERMINAL_OUTCOMES:
        return None
    if outcome and outcome not in _TERMINAL_OUTCOMES:
        return None
    return fields


def _parse_correlated_start(
    value: str,
    expected_request_id: str,
) -> dict[str, str] | None:
    return (
        _parse_ack(value, expected_request_id)
        or _parse_request_status(value, expected_request_id)
    )


def _is_recoverable_prepared(value: str, fields: dict[str, str] | None) -> bool:
    return (
        fields is not None
        and value.startswith(f"{_REQUEST_STATUS}|")
        and fields.get("state") == "prepared"
        and not fields.get("outcome")
    )


def _unknown_start(request_id: str, reason: str) -> str:
    return f"{_START_UNKNOWN}|request_id={request_id}|reason={reason}"


def _compact_snapshot(value: str) -> str:
    """Keep timeout diagnostics precise without pretty-printed JSON noise."""
    try:
        return json.dumps(
            json.loads(value), separators=(",", ":"), sort_keys=True, ensure_ascii=False
        )
    except (TypeError, ValueError, json.JSONDecodeError):
        return value or "none"


def _decode_snapshot(
    value: str,
    *,
    expected_request_id: str,
    expected_run_id: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """Return (snapshot, protocol_error), validating correlation fields."""
    try:
        snapshot = json.loads(value)
    except (TypeError, ValueError, json.JSONDecodeError):
        return None, None
    if not isinstance(snapshot, dict):
        return None, None

    run_id = snapshot.get("run_id")
    if run_id is None:
        return None, "run-id-missing"
    if run_id != expected_run_id:
        return None, "run-id-mismatch"
    request_id = snapshot.get("request_id")
    if request_id is None:
        return None, "request-id-missing"
    if request_id != expected_request_id:
        return None, "request-id-mismatch"
    return snapshot, None


def _snapshot_matches_intent(
    snapshot: dict[str, Any], *, mode: str, filter_name: str
) -> bool:
    return (
        snapshot.get("source") == "mcp"
        and snapshot.get("mode") == mode
        and str(snapshot.get("filter") or "") == filter_name
    )


# UTF's TestRunService feeds the raw filter into Filter.groupNames,
# regex-matched. A nested-class filter like "Foo+Bar" ('+' is .NET's nested
# -class separator) is reinterpreted as a quantifier -- usually zero matches.
# '.' is excluded: it's the universal namespace/method separator, flagging it
# would make every filter look suspicious. '|' is excluded too: it's UTF's
# legal separator for multi-group filters ("TestA|TestB"), never
# misinterpreted by the matching engine -- an "escape it" hint would mislead.
_REGEX_METACHARS = frozenset("+*?()[]{}^$\\")


def _filter_has_regex_metachar(filter_name: str) -> bool:
    return any(ch in _REGEX_METACHARS for ch in filter_name)


# The reason string each terminalError DECISION outcome prints on the wire (unchanged by
# the extraction -- callers and tests match these exact kebab-case strings). `ok` has no
# entry: `.get()` returns None for it, which is the "no error" contract of this function.
_TERMINAL_ERROR_REASONS = {
    TerminalErrorValues.LIFECYCLE_MISMATCH: "lifecycle-mismatch",
    TerminalErrorValues.REQUEST_INTENT_MISMATCH: "request-intent-mismatch",
    TerminalErrorValues.HEALTH_NO_PROGRESS: "run-health-no-test-progress",
    TerminalErrorValues.HEALTH_UNRESPONSIVE: "run-health-editor-unresponsive",
    TerminalErrorValues.TERMINAL_FLAG_MISSING: "terminal-flag-missing",
    TerminalErrorValues.EXECUTION_BOUNDARY_MISSING: "execution-boundary-missing",
    TerminalErrorValues.CLEANUP_BOUNDARY_MISSING: "cleanup-boundary-missing",
    TerminalErrorValues.RUN_START_BOUNDARY_MISSING: "run-start-boundary-missing",
    TerminalErrorValues.MANIFEST_BOUNDARY_MISSING: "manifest-boundary-missing",
    TerminalErrorValues.RUN_FINISH_BOUNDARY_MISSING: "run-finish-boundary-missing",
    TerminalErrorValues.BUILD_INCOHERENT: "build-incoherent",
    TerminalErrorValues.INVALID_TERMINAL_OUTCOME: "invalid-terminal-outcome",
    TerminalErrorValues.UTF_GUID_MISSING: "utf-guid-missing",
    TerminalErrorValues.UTF_XML_SCOPE_MISSING: "utf-xml-scope-missing",
    TerminalErrorValues.EXPECTED_COUNT_INVALID: "expected-count-invalid",
    TerminalErrorValues.RUN_ZERO_MATCH_METACHAR: "run-zero-match-metachar",
    TerminalErrorValues.RUN_ZERO_MATCH_FILTER: "run-zero-match-filter",
    TerminalErrorValues.TERMINAL_COUNT_INVARIANT: "terminal-count-invariant",
    TerminalErrorValues.TERMINAL_STATUS_COUNT_INVALID: "terminal-status-count-invalid",
    TerminalErrorValues.TERMINAL_STATUS_TOTAL_MISMATCH: "terminal-status-total-mismatch",
    TerminalErrorValues.ISSUES_EVIDENCE_INVALID: "issues-evidence-invalid",
    TerminalErrorValues.INFRASTRUCTURE_ERRORS: "infrastructure-errors",
}


def _expected_count_facts(
    snapshot: dict[str, Any], expected: int | None
) -> tuple[bool, str, int]:
    """(hasExpected, expectedCountCase, expectedCountValue) for the terminalError DECISION.

    ARC-17 X1 (DEV-16 review): an old plugin's terminal snapshot may omit
    `expected_count` entirely -- absence must be inert, not fail-closed (ARC-17 §4).
    Only a *present but corrupted* value (bool, non-int, negative, or an honest zero)
    still produces a non-"ok" case.
    """
    if expected is None:
        return False, "ok", 0
    if isinstance(expected, bool) or not isinstance(expected, int):
        return True, "invalidType", 0
    if expected == 0:
        return True, "zero", 0
    if expected < 0:
        return True, "negative", 0

    invariant_fields = (
        "declared_expected_count", "readable_manifest_count",
        "completed_expected_count", "unique_terminal_count",
    )
    zero_fields = (
        "unmaterialized_expected_count", "missing_count",
        "unexpected_count", "conflict_count",
    )
    broken = any(
        isinstance(snapshot.get(f), bool) or not isinstance(snapshot.get(f), int)
        or snapshot.get(f) != expected
        for f in invariant_fields
    ) or any(
        isinstance(snapshot.get(f), bool) or not isinstance(snapshot.get(f), int)
        or snapshot.get(f) != 0
        for f in zero_fields
    )
    return True, "invariantBroken" if broken else "ok", expected


def _status_total_facts(snapshot: dict[str, Any]) -> tuple[bool, int]:
    """(statusCountsValid, statusTotal) for the terminalError DECISION."""
    total = 0
    for field in ("passed", "failed", "skipped", "inconclusive", "cancelled", "invalid"):
        value = snapshot.get(field)
        if isinstance(value, bool) or not isinstance(value, int) or value < 0:
            return False, 0
        total += value
    return True, total


def _gather_terminal_facts(
    snapshot: dict[str, Any], *, mode: str, filter_name: str
) -> TerminalErrorInput:
    """Facts for the terminalError DECISION, read from the same fields the original
    if-ladder read (no new I/O; these are all cheap dict reads)."""
    health = snapshot.get("health")
    has_expected, expected_count_case, expected_count_value = _expected_count_facts(
        snapshot, snapshot.get("expected_count")
    )
    status_counts_valid, status_total = _status_total_facts(snapshot)
    issues = snapshot.get("issues")
    issues_valid = isinstance(issues, list) and all(
        isinstance(issue, dict) for issue in issues
    )

    return TerminalErrorInput(
        terminal_lifecycle_ok=snapshot.get("state") == "terminal"
        and snapshot.get("lifecycle") == "terminal",
        terminal_intent_matches=_snapshot_matches_intent(
            snapshot, mode=mode, filter_name=filter_name
        ),
        terminal_health_no_progress=health == "no_test_progress",
        terminal_health_unresponsive=health == "editor_unresponsive",
        terminal_is_terminal_flag=snapshot.get("is_terminal") is True,
        terminal_execution_finished=snapshot.get("execution_finished") is True,
        terminal_cleanup_complete=snapshot.get("cleanup_complete") is True,
        terminal_run_started_observed=snapshot.get("run_started_observed") is True,
        terminal_manifest_complete=snapshot.get("manifest_complete") is True,
        terminal_run_finished_observed=snapshot.get("run_finished_observed") is True,
        terminal_build_coherent=snapshot.get("build_coherent") is True,
        terminal_outcome_valid=snapshot.get("outcome") in _TERMINAL_OUTCOMES,
        terminal_utf_guid_present=bool(snapshot.get("utf_guid")),
        terminal_utf_xml_scope_valid=snapshot.get("utf_xml_scope") in {"complete", "partial"},
        terminal_has_expected=has_expected,
        terminal_expected_count_case=expected_count_case,
        terminal_filter_has_metachar=_filter_has_regex_metachar(filter_name),
        terminal_status_counts_valid=status_counts_valid,
        terminal_status_total=status_total,
        terminal_expected_count_value=expected_count_value,
        terminal_issues_valid=issues_valid,
        terminal_has_infrastructure_error=issues_valid
        and any(issue.get("severity") == "error" for issue in issues),
    )


def _terminal_snapshot_error(
    snapshot: dict[str, Any], *, mode: str, filter_name: str
) -> str | None:
    """Return a stable fail-closed protocol reason for terminal evidence.

    The branch order is the DECISION terminalError of dsl/decisions/terminalerror.logic
    (generated into decisions/generated/terminalerror_logic.py), extracted because this
    first-match-wins ladder is exactly the shape that breaks silently when a branch is
    inserted, reordered, or merged by hand (see busySignal/retryGate).
    """
    facts = _gather_terminal_facts(snapshot, mode=mode, filter_name=filter_name)
    return _TERMINAL_ERROR_REASONS.get(terminal_error(facts))


async def _preflight() -> str | None:
    """Return a BLOCKED verdict, or None when dispatch may proceed."""
    max_retries = 2
    recoverable = ("FAIL:stale-dll", "FAIL:unknown", "STALE-CACHE", "STALE-TRANSIENT")
    try:
        from . import diagnose as _diag
        for attempt in range(max_retries + 1):
            verdict = await _diag.diagnose(prev_mvid="", expected_compile=False)
            if not verdict.startswith(_BLOCK_STARTS):
                return None
            if not verdict.startswith(recoverable):
                return f"BLOCKED: {verdict} -- fix domain state before running tests"
            if attempt >= max_retries:
                return (
                    f"BLOCKED: {verdict} -- auto-recovery exhausted after "
                    f"{max_retries} attempts"
                )
            with contextlib.suppress(Exception):
                await _send("force_refresh", {})
            await asyncio.sleep(10)
    except ToolError:
        raise
    except Exception:
        # Diagnose is advisory. Dispatch still has its own fail-closed checks in Unity.
        return None
    return None


async def _run_tests_body(
    mode: str = "EditMode",
    filter: str | None = None,
    request_id: str | None = None,
    assemblies: str | None = None,
    categories: str | None = None,
    group: str | None = None,
    tests: str | None = None,
) -> str:
    """Dispatch Unity tests and return their durable identity immediately.

    A successful response is
    ``tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched``.
    If transport fails after dispatch may have happened, the result is
    ``START-UNKNOWN`` with the same request_id; resolve it instead of retrying
    with a new identity.

    assemblies/categories/tests: comma-separated selection lists narrowing
    the run (assembly names, NUnit categories, fully-qualified test names).
    group: a named test group to restrict the run to.
    """
    caller_supplied_request = request_id is not None
    if caller_supplied_request and not _valid_identity(request_id):
        return (
            "BLOCKED: request_id must contain 1-200 ASCII letters, digits, "
            "'.', '_' or '-'"
        )
    stable_request_id = request_id or _new_request_id()
    if caller_supplied_request:
        try:
            existing = await resolve_test_request(stable_request_id)
        except Exception as exc:
            return _unknown_start(
                stable_request_id, f"resolve-{type(exc).__name__}"
            )
        existing_status = _parse_correlated_start(existing, stable_request_id)
        if existing_status is not None:
            # request.json is committed before run.json. A crash at that boundary
            # resolves as a correlated prepared intent; calling Start with the same
            # identity heals/continues it. Every later state is authoritative and
            # must never be dispatched again.
            recoverable_prepared = _is_recoverable_prepared(
                existing, existing_status
            )
            if not recoverable_prepared:
                try:
                    # Raw fetch, not the validating get_test_run wrapper: this
                    # branch does its own correlation + intent check right
                    # below, same reason run_tests_wait bypasses it (see
                    # _fetch_test_run_json docstring) -- a PROTOCOL-ERROR
                    # string here would otherwise fail _decode_snapshot's
                    # JSON parse and get masked as "intent-check-invalid".
                    current = await _fetch_test_run_json(existing_status["run_id"])
                except Exception as exc:
                    return _unknown_start(
                        stable_request_id, f"intent-check-{type(exc).__name__}"
                    )
                snapshot, protocol_error = _decode_snapshot(
                    current,
                    expected_request_id=stable_request_id,
                    expected_run_id=existing_status["run_id"],
                )
                if protocol_error is not None or snapshot is None:
                    return _unknown_start(stable_request_id, "intent-check-invalid")
                if not _snapshot_matches_intent(
                    snapshot, mode=mode, filter_name=filter or ""
                ):
                    return (
                        "BLOCKED: request_id is already bound to a different "
                        "immutable test mode or filter"
                    )
                return existing
        elif existing != "none":
            return _unknown_start(stable_request_id, "invalid-resolve")

    blocked = await _preflight()
    if blocked is not None:
        return blocked

    args = {"mode": mode, "request_id": stable_request_id}
    if filter:
        args["filter"] = filter
    if assemblies:
        args["assemblies"] = assemblies
    if categories:
        args["categories"] = categories
    if group:
        args["group"] = group
    if tests:
        args["tests"] = tests

    try:
        result = await _send("run_tests", args)
    except Exception as exc:
        return _unknown_start(stable_request_id, type(exc).__name__)

    correlated = _parse_correlated_start(result, stable_request_id)
    if correlated is not None:
        if not _is_recoverable_prepared(result, correlated):
            ec_raw = correlated.get("expected_count", "")
            try:
                expected_count: int | None = int(ec_raw) if ec_raw else None
            except (TypeError, ValueError):
                expected_count = None
            if expected_count == 0:
                hint = ""
                if filter and _filter_has_regex_metachar(filter):
                    hint = (
                        " (filter contains a regex metacharacter -- escape it "
                        "or use exact test names)"
                    )
                raise ToolError(
                    f"BLOCKED: Empty manifest: no tests match filter{hint}"
                )
            handle = _registry.register(correlated["run_id"], stable_request_id)
            if expected_count is not None:
                handle.expected_count = expected_count
        return result
    return _unknown_start(stable_request_id, "invalid-ack")


async def _run_tests_wait_body(
    mode: str = "EditMode",
    filter: str = "",
    timeout: float = 900.0,
    poll_interval: float = 5.0,
    request_id: str | None = None,
) -> str:
    """Dispatch tests and wait for the exact run to become terminal. Dispatches test run. No confirmation required.

    Transport failures and domain reloads do not erase the last snapshot. A
    caller timeout is observational only: it returns ``TIMEOUT`` with request,
    run and snapshot data and never marks the Unity run complete.
    on_timeout: result starts with TIMEOUT|request_id=...|run_id=... — use run_id to resume polling via get_test_run without re-dispatching.
    """
    stable_request_id = request_id or _new_request_id()
    started = await run_tests(mode, filter or None, request_id=stable_request_id)

    correlated = _parse_correlated_start(started, stable_request_id)
    known_run_id = correlated["run_id"] if correlated is not None else ""
    run_id = "" if _is_recoverable_prepared(started, correlated) else known_run_id
    if correlated is None:
        unknown = _parse_fields(started, _START_UNKNOWN)
        if unknown is None or unknown.get("request_id") != stable_request_id:
            return _unknown_start(stable_request_id, "invalid-start-response")

    interval = max(0.01, float(poll_interval))
    attempts = max(1, math.floor(max(0.0, float(timeout)) / interval) + 1)
    loop = asyncio.get_running_loop()
    deadline = loop.time() + max(0.0, float(timeout))
    last_snapshot = "none"
    health_streak = 0

    for attempt in range(attempts):
        if not run_id:
            remaining = deadline - loop.time()
            if remaining <= 0:
                break
            try:
                resolved = await asyncio.wait_for(
                    resolve_test_request(stable_request_id), timeout=remaining
                )
            except TimeoutError:
                break
            except Exception:
                resolved = ""
            resolved_status = _parse_correlated_start(resolved, stable_request_id)
            if resolved_status is None:
                if resolved not in ("", "none", "pending"):
                    last_snapshot = resolved
            else:
                resolved_run_id = resolved_status["run_id"]
                if known_run_id and resolved_run_id != known_run_id:
                    return (
                        f"PROTOCOL-ERROR|request_id={stable_request_id}"
                        f"|run_id={known_run_id}|reason=request-run-id-changed"
                        f"|snapshot={_compact_snapshot(resolved)}"
                    )
                known_run_id = resolved_run_id
                if _is_recoverable_prepared(resolved, resolved_status):
                    remaining = deadline - loop.time()
                    if remaining <= 0:
                        break
                    try:
                        resumed = await asyncio.wait_for(
                            run_tests(
                                mode,
                                filter or None,
                                request_id=stable_request_id,
                            ),
                            timeout=remaining,
                        )
                    except TimeoutError:
                        break
                    except Exception:
                        resumed = ""
                    if resumed.startswith("BLOCKED:"):
                        return resumed
                    resumed_status = _parse_correlated_start(
                        resumed, stable_request_id
                    )
                    if resumed_status is not None:
                        resumed_run_id = resumed_status["run_id"]
                        if resumed_run_id != known_run_id:
                            return (
                                f"PROTOCOL-ERROR|request_id={stable_request_id}"
                                f"|run_id={known_run_id}"
                                f"|reason=request-run-id-changed"
                                f"|snapshot={_compact_snapshot(resumed)}"
                            )
                        if not _is_recoverable_prepared(resumed, resumed_status):
                            run_id = resumed_run_id
                    elif (
                        resumed not in ("", "none", "pending")
                        and not resumed.startswith(f"{_START_UNKNOWN}|")
                    ):
                        last_snapshot = resumed
                else:
                    run_id = resolved_run_id

        if run_id:
            remaining = deadline - loop.time()
            if remaining <= 0:
                break
            try:
                current = await asyncio.wait_for(
                    _fetch_test_run_json(run_id, compact=True), timeout=remaining
                )
            except TimeoutError:
                break
            except Exception:
                # Transport hiccup, not confirmed dead -- counts toward the
                # same diagnostic streak as an explicit suspected_stall.
                current = ""
                health_streak += 1
            if current not in ("", "none", "pending"):
                last_snapshot = current
                snapshot, protocol_error = _decode_snapshot(
                    current,
                    expected_request_id=stable_request_id,
                    expected_run_id=run_id,
                )
                if protocol_error is not None:
                    return (
                        f"PROTOCOL-ERROR|request_id={stable_request_id}"
                        f"|run_id={run_id}|reason={protocol_error}"
                        f"|snapshot={_compact_snapshot(current)}"
                    )
                snapshot_state = None
                if snapshot is not None:
                    state = snapshot.get("state")
                    lifecycle = snapshot.get("lifecycle")
                    if (
                        state is not None
                        and lifecycle is not None
                        and state != lifecycle
                    ):
                        return (
                            f"PROTOCOL-ERROR|request_id={stable_request_id}"
                            f"|run_id={run_id}"
                            f"|reason=lifecycle-mismatch"
                            f"|snapshot={_compact_snapshot(current)}"
                        )
                    snapshot_state = state or lifecycle
                    if snapshot_state != "terminal":
                        health = snapshot.get("health")
                        if health == "suspected_stall":
                            health_streak += 1
                        elif health in ("healthy", "reloading"):
                            health_streak = 0
                if snapshot is not None and snapshot_state == "terminal":
                    terminal_error = _terminal_snapshot_error(
                        snapshot, mode=mode, filter_name=filter or ""
                    )
                    if terminal_error is not None:
                        return (
                            f"PROTOCOL-ERROR|request_id={stable_request_id}"
                            f"|run_id={run_id}"
                            f"|reason={terminal_error}"
                            f"|snapshot={_compact_snapshot(current)}"
                        )
                    _try_update_handle_from_result(run_id, current)
                    return _compact_snapshot(current)

        if attempt + 1 < attempts:
            remaining = deadline - loop.time()
            if remaining > 0:
                await asyncio.sleep(min(interval, remaining))

    resolved_run_id = run_id or known_run_id
    if resolved_run_id:
        try:
            disk_result = _read_disk_fallback(
                resolved_run_id,
                mode=mode,
                filter_name=filter or "",
                expected_request_id=stable_request_id,
            )
        except Exception:
            # A filesystem hiccup degrades to the pre-existing TIMEOUT below,
            # never crashes a caller that's already out of patience.
            disk_result = None
        if disk_result is not None:
            _try_update_handle_from_result(resolved_run_id, disk_result)
            return disk_result

    streak_suffix = (
        f"|health_streak={health_streak}"
        if health_streak >= _HEALTH_STREAK_TIMEOUT_THRESHOLD
        else ""
    )
    return (
        f"TIMEOUT|request_id={stable_request_id}"
        f"|run_id={run_id or known_run_id or 'unknown'}"
        f"|snapshot={_compact_snapshot(last_snapshot)}"
        f"{streak_suffix}"
    )


async def _resolve_test_request_body(request_id: str) -> str:
    """Resolve a possibly lost start ACK without dispatching another test run."""
    return await _send("resolve_test_request", {"request_id": request_id})


async def _fetch_test_run_json(run_id: str, *, compact: bool = False) -> str:
    """Return the raw durable JSON snapshot for one exact test run.

    Unvalidated by design: ``run_tests_wait`` polls through this directly and
    does its own richer, intent-aware validation right after. Routing it
    through the fail-closed ``get_test_run`` wrapper instead would let a
    ``PROTOCOL-ERROR`` string get silently swallowed by ``_decode_snapshot``'s
    non-JSON path and surface as a masked ``TIMEOUT``.

    compact (A27): only ``run_tests_wait``'s tight-cadence poll loop opts in
    (Unity's A26 trims leaf-detail arrays on a non-terminal snapshot). Omitted
    from the wire args entirely when False, so a caller reading a snapshot in
    full (e.g. the direct ``get_test_run`` tool) sends the exact same payload
    shape as before this existed.
    """
    handle = _registry.get(run_id)
    if handle is not None and handle.result is not None:
        return handle.result  # cached terminal result

    args = {"run_id": run_id}
    if compact:
        args["compact"] = True
    result = await _send("get_test_run", args)

    if handle is None and result in ("none", "null", ""):
        return f"NOT_FOUND|run_id={run_id}"

    try:
        data = json.loads(result)
        if isinstance(data, dict):
            result_run_id = data.get("run_id")
            if result_run_id is not None and result_run_id != run_id:
                return f"UNCORRELATED|run_id={run_id}|result_run_id={result_run_id}"
            if (
                handle is not None
                and handle.expected_count is not None
                and "expected_count" not in data
            ):
                data["expected_count"] = handle.expected_count
                result = json.dumps(data, separators=(",", ":"), ensure_ascii=False)
    except (TypeError, ValueError):
        pass

    _try_update_handle_from_result(run_id, result)
    return result


async def _get_test_run_body(run_id: str, compact: bool = False) -> str:
    """Return the durable JSON snapshot for one exact test run.

    Fails closed on terminal-invalid evidence: a snapshot claiming
    ``state == "terminal"`` that doesn't pass ARC-1/ARC-3 validation (an
    honest zero-match filter, a stalled/unresponsive terminal claim, a
    corrupted count) comes back as
    ``PROTOCOL-ERROR|run_id=...|reason=...|snapshot=...`` instead of the raw
    snapshot -- closes the direct-poll path around ``run_tests_wait``'s own
    validation.

    compact=True trims leaf-detail arrays on a non-terminal snapshot (A27) --
    use for a tight-cadence poll loop; a terminal snapshot is unaffected.
    """
    result = await _fetch_test_run_json(run_id, compact=compact)
    try:
        data = json.loads(result)
    except (TypeError, ValueError, json.JSONDecodeError):
        return result
    if not isinstance(data, dict) or data.get("state") != "terminal":
        return result
    reason = _terminal_snapshot_error(
        data, mode=str(data.get("mode") or ""), filter_name=str(data.get("filter") or "")
    )
    if reason is None:
        return result
    return (
        f"PROTOCOL-ERROR|run_id={run_id}|reason={reason}"
        f"|snapshot={_compact_snapshot(result)}"
    )


async def _list_test_runs_body(limit: int = 20) -> str:
    """List recent durable test runs as JSON, newest first."""
    return await _send("list_test_runs", {"limit": max(1, min(int(limit), 100))})


async def _get_test_results_body(run_id: str | None = None) -> str:
    """Legacy result facade. Pass run_id to prevent reading a stale latest run."""
    args = {"run_id": run_id} if run_id else {}
    try:
        return await _send("get_test_results", args)
    except Exception:
        return "pending"


async def _get_test_progress_body(run_id: str | None = None) -> str:
    """Legacy progress facade. Pass run_id to correlate the response."""
    args = {"run_id": run_id} if run_id else {}
    try:
        return await _send("get_test_progress", args)
    except Exception:
        return "pending"


def _resolve_project_path() -> Path | None:
    """Resolve the connected Unity project's path: get_slot -> port -> autodetect.

    Fail-inert by design (returns None on any missing link) — a project-path
    miss must degrade the disk fallback (D3/D4) to a no-op, never raise.
    """
    if _get_slot is None:
        return None
    slot = _get_slot()
    if slot is None:
        return None
    return CompileStateProbe.autodetect_project_path(port=slot.port)


def _read_disk_fallback(
    run_id: str,
    *,
    mode: str,
    filter_name: str,
    expected_request_id: str,
) -> str | None:
    """Last-resort disk read of a durable terminal test-run summary (ARC-2).

    Single-shot: called only from run_tests_wait's TIMEOUT return. Reuses
    _decode_snapshot/_terminal_snapshot_error verbatim against disk JSON
    instead of wire JSON -- one validation path, no new rules. Fail-inert by
    design: an unresolved project path, an unsafe run_id, a missing/empty/
    corrupt file, a non-terminal snapshot, or failed terminal invariants all
    return None -- never an exception, so a filesystem hiccup degrades to the
    pre-existing TIMEOUT instead of crashing it.
    """
    if not _valid_identity(run_id):
        return None
    project_path = _resolve_project_path()
    if project_path is None:
        return None
    raw = run_disk_fallback.read_terminal_summary(project_path, run_id)
    if raw is None:
        return None
    snapshot, protocol_error = _decode_snapshot(
        raw, expected_request_id=expected_request_id, expected_run_id=run_id
    )
    if protocol_error is not None or snapshot is None:
        return None
    if _terminal_snapshot_error(snapshot, mode=mode, filter_name=filter_name) is not None:
        return None
    snapshot["read_via"] = run_disk_fallback.READ_VIA_DISK
    return json.dumps(snapshot, separators=(",", ":"), sort_keys=True, ensure_ascii=False)


def register(mcp, send, args, *, get_slot=None):
    bind(globals(), send, args)
    global _get_slot
    _get_slot = get_slot
