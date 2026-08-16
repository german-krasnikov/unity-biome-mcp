"""Unity Test Framework orchestration with durable run identity."""

import asyncio
import contextlib
import json
import math
import re
import uuid
from typing import Any

from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._annotations import RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None

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
_RUN_STATES = {"prepared", "dispatched", "running", "finalizing", "terminal"}
_TERMINAL_OUTCOMES = {
    "passed", "failed", "cancelled", "incomplete", "invalid", "dispatch_failed",
}
_IDENTITY_RE = re.compile(r"^[A-Za-z0-9._-]{1,200}$")


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
        return json.dumps(json.loads(value), separators=(",", ":"), sort_keys=True)
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


def _terminal_snapshot_error(
    snapshot: dict[str, Any], *, mode: str, filter_name: str
) -> str | None:
    """Return a stable fail-closed protocol reason for terminal evidence."""
    if snapshot.get("state") != "terminal" or snapshot.get("lifecycle") != "terminal":
        return "lifecycle-mismatch"
    if not _snapshot_matches_intent(snapshot, mode=mode, filter_name=filter_name):
        return "request-intent-mismatch"

    required_flags = (
        ("is_terminal", "terminal-flag-missing"),
        ("execution_finished", "execution-boundary-missing"),
        ("cleanup_complete", "cleanup-boundary-missing"),
        ("run_started_observed", "run-start-boundary-missing"),
        ("manifest_complete", "manifest-boundary-missing"),
        ("run_finished_observed", "run-finish-boundary-missing"),
        ("build_coherent", "build-incoherent"),
    )
    for field, reason in required_flags:
        if snapshot.get(field) is not True:
            return reason

    if snapshot.get("outcome") not in _TERMINAL_OUTCOMES:
        return "invalid-terminal-outcome"
    if not snapshot.get("utf_guid"):
        return "utf-guid-missing"
    if snapshot.get("utf_xml_scope") not in {"complete", "partial"}:
        return "utf-xml-scope-missing"

    expected = snapshot.get("expected_count")
    if isinstance(expected, bool) or not isinstance(expected, int) or expected <= 0:
        return "expected-count-invalid"
    for field in (
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
    ):
        value = snapshot.get(field)
        if isinstance(value, bool) or not isinstance(value, int) or value != expected:
            return "terminal-count-invariant"
    for field in (
        "unmaterialized_expected_count",
        "missing_count",
        "unexpected_count",
        "conflict_count",
    ):
        value = snapshot.get(field)
        if isinstance(value, bool) or not isinstance(value, int) or value != 0:
            return "terminal-count-invariant"

    statuses = (
        "passed", "failed", "skipped", "inconclusive", "cancelled", "invalid",
    )
    status_total = 0
    for field in statuses:
        value = snapshot.get(field)
        if isinstance(value, bool) or not isinstance(value, int) or value < 0:
            return "terminal-status-count-invalid"
        status_total += value
    if status_total != expected:
        return "terminal-status-total-mismatch"

    issues = snapshot.get("issues")
    if not isinstance(issues, list) or any(
        not isinstance(issue, dict) for issue in issues
    ):
        return "issues-evidence-invalid"
    if any(issue.get("severity") == "error" for issue in issues):
        return "infrastructure-errors"
    return None


async def _preflight() -> str | None:
    """Return a BLOCKED verdict, or None when dispatch may proceed."""
    from mcp.server.fastmcp.exceptions import ToolError as _ToolError

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
    except _ToolError:
        raise
    except Exception:
        # Diagnose is advisory. Dispatch still has its own fail-closed checks in Unity.
        return None
    return None


async def run_tests(
    mode: str = "EditMode",
    filter: str | None = None,
    request_id: str | None = None,
) -> str:
    """Dispatch Unity tests and return their durable identity immediately.

    A successful response is
    ``tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched``.
    If transport fails after dispatch may have happened, the result is
    ``START-UNKNOWN`` with the same request_id; resolve it instead of retrying
    with a new identity.
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
                    current = await get_test_run(existing_status["run_id"])
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

    try:
        result = await _send("run_tests", args, timeout=8.0)
    except Exception as exc:
        return _unknown_start(stable_request_id, type(exc).__name__)

    if _parse_correlated_start(result, stable_request_id) is not None:
        return result
    return _unknown_start(stable_request_id, "invalid-ack")


async def run_tests_wait(
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
    """
    stable_request_id = request_id or _new_request_id()
    started = await run_tests(mode, filter or None, request_id=stable_request_id)
    if started.startswith("BLOCKED:"):
        return started

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

    for attempt in range(attempts):
        if not run_id:
            remaining = deadline - loop.time()
            if remaining <= 0:
                break
            try:
                resolved = await asyncio.wait_for(
                    resolve_test_request(stable_request_id), timeout=remaining
                )
            except asyncio.TimeoutError:
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
                    except asyncio.TimeoutError:
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
                    get_test_run(run_id), timeout=remaining
                )
            except asyncio.TimeoutError:
                break
            except Exception:
                current = ""
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
                    return _compact_snapshot(current)

        if attempt + 1 < attempts:
            remaining = deadline - loop.time()
            if remaining > 0:
                await asyncio.sleep(min(interval, remaining))

    return (
        f"TIMEOUT|request_id={stable_request_id}"
        f"|run_id={run_id or known_run_id or 'unknown'}"
        f"|snapshot={_compact_snapshot(last_snapshot)}"
    )


async def resolve_test_request(request_id: str) -> str:
    """Resolve a possibly lost start ACK without dispatching another test run."""
    return await _send("resolve_test_request", {"request_id": request_id})


async def get_test_run(run_id: str) -> str:
    """Return the durable JSON snapshot for one exact test run."""
    return await _send("get_test_run", {"run_id": run_id})


async def cancel_test_run(run_id: str) -> str:
    """Request cancellation of one exact test run; cancellation is asynchronous."""
    return await _send("cancel_test_run", {"run_id": run_id})


async def list_test_runs(limit: int = 20) -> str:
    """List recent durable test runs as JSON, newest first."""
    return await _send("list_test_runs", {"limit": max(1, min(int(limit), 100))})


async def get_test_results(run_id: str | None = None) -> str:
    """Legacy result facade. Pass run_id to prevent reading a stale latest run."""
    args = {"run_id": run_id} if run_id else {}
    try:
        return await _send("get_test_results", args)
    except Exception:
        return "pending"


async def get_test_progress(run_id: str | None = None) -> str:
    """Legacy progress facade. Pass run_id to correlate the response."""
    args = {"run_id": run_id} if run_id else {}
    try:
        return await _send("get_test_progress", args)
    except Exception:
        return "pending"


async def get_test_count() -> str:
    """Number of edit-mode and play-mode tests in the project."""
    return await _send("get_test_count", {})


def register(mcp, send, args):
    bind(globals(), send, args)
    # The bridge may retry run_tests only because every retry carries the same
    # durable request_id. run_tests_wait is a composite operation, not idempotent.
    mcp.tool(annotations=_RW_IDEM)(run_tests)
    mcp.tool(annotations=_RW)(run_tests_wait)
    mcp.tool(annotations=_RO)(resolve_test_request)
    mcp.tool(annotations=_RO)(get_test_run)
    mcp.tool(annotations=_RW_IDEM)(cancel_test_run)
    mcp.tool(annotations=_RO)(list_test_runs)
    mcp.tool(annotations=_RO)(get_test_results)
    mcp.tool(annotations=_RO)(get_test_progress)
    mcp.tool(annotations=_RO)(get_test_count)
