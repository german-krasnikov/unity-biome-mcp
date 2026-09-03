"""Tests for exact-run polling and lost-ACK recovery."""

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
async def test_wait_returns_only_the_terminal_snapshot_for_exact_run():
    polls = [_snapshot("running"), _snapshot("terminal", "passed")]
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(side_effect=polls)) as get_run, \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=2.0, poll_interval=1.0
        )

    decoded = json.loads(result)
    assert decoded["run_id"] == RUN_ID
    assert decoded["state"] == "terminal"
    assert decoded["outcome"] == "passed"
    assert get_run.await_args_list[0].args == (RUN_ID,)


@pytest.mark.asyncio
async def test_wait_polls_exact_run_before_first_sleep():
    order = []
    terminal = _snapshot("terminal", "passed")

    async def poll(run_id):
        order.append(("poll", run_id))
        return terminal

    async def sleep(_seconds):
        order.append(("sleep", None))

    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", poll), \
         patch("asyncio.sleep", sleep):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=10.0, poll_interval=5.0
        )

    assert json.loads(result)["outcome"] == "passed"
    assert order == [("poll", RUN_ID)]


@pytest.mark.asyncio
async def test_finalizing_dispatch_failure_is_polled_by_exact_run_id():
    async def finalizing(mode, filter=None, request_id=None):
        return (
            f"test-request|request_id={REQUEST_ID}|run_id={RUN_ID}"
            "|state=finalizing|outcome=dispatch_failed"
        )

    terminal = _snapshot("terminal", "dispatch_failed")
    get_run = AsyncMock(return_value=terminal)
    with patch.object(testing, "run_tests", finalizing), \
         patch.object(testing, "_fetch_test_run_json", get_run), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert json.loads(result)["outcome"] == "dispatch_failed"
    get_run.assert_awaited_once_with(RUN_ID)


@pytest.mark.asyncio
async def test_lost_ack_resolves_correlated_terminal_status_then_polls():
    async def unknown(mode, filter=None, request_id=None):
        return f"START-UNKNOWN|request_id={request_id}|reason=ConnectionError"

    status = (
        f"test-request|request_id={REQUEST_ID}|run_id={RUN_ID}"
        "|state=terminal|outcome=dispatch_failed"
    )
    terminal = _snapshot("terminal", "dispatch_failed")
    with patch.object(testing, "run_tests", unknown), \
         patch.object(
             testing, "resolve_test_request", AsyncMock(return_value=status)
         ), \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(return_value=terminal)
         ) as get_run, \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert json.loads(result)["outcome"] == "dispatch_failed"
    get_run.assert_awaited_once_with(RUN_ID)


@pytest.mark.asyncio
async def test_timeout_is_nonterminal_and_keeps_identity_and_last_snapshot():
    running = _snapshot("running")
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value=running)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=0.001, poll_interval=1.0
        )

    assert result.startswith(
        f"TIMEOUT|request_id={REQUEST_ID}|run_id={RUN_ID}|snapshot="
    )
    assert '"state":"running"' in result
    assert '"state":"terminal"' not in result


@pytest.mark.asyncio
async def test_blocked_preflight_is_propagated_without_polling():
    async def blocked(mode, filter=None, request_id=None):
        raise ToolError("BLOCKED: FAIL:CS0117 -- fix domain state before running tests")

    poll = AsyncMock()
    with patch.object(testing, "run_tests", blocked), \
         patch.object(testing, "_fetch_test_run_json", poll):
        with pytest.raises(ToolError, match="BLOCKED"):
            await testing.run_tests_wait(request_id=REQUEST_ID)

    poll.assert_not_awaited()


@pytest.mark.asyncio
async def test_poll_transport_failure_keeps_last_snapshot_and_recovers():
    polls = [
        _snapshot("running"),
        ConnectionError("domain reload"),
        _snapshot("terminal", "failed"),
    ]
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(side_effect=polls)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=3.0, poll_interval=1.0
        )

    decoded = json.loads(result)
    assert decoded["run_id"] == RUN_ID
    assert decoded["outcome"] == "failed"


@pytest.mark.asyncio
async def test_lost_start_ack_is_resolved_before_exact_polling():
    async def unknown(mode, filter=None, request_id=None):
        return f"START-UNKNOWN|request_id={request_id}|reason=ConnectionError"

    terminal = _snapshot("terminal", "passed")
    with patch.object(testing, "run_tests", unknown), \
         patch.object(
             testing,
             "resolve_test_request",
             AsyncMock(side_effect=["none", ACK]),
         ) as resolve, \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value=terminal)) as get_run, \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=3.0, poll_interval=1.0
        )

    assert json.loads(result)["outcome"] == "passed"
    assert resolve.await_count == 2
    get_run.assert_awaited_once_with(RUN_ID)


@pytest.mark.asyncio
async def test_lost_ack_prepared_intent_is_resumed_with_same_immutable_payload():
    calls = []

    async def start(mode, filter=None, request_id=None):
        calls.append((mode, filter, request_id))
        if len(calls) == 1:
            return f"START-UNKNOWN|request_id={request_id}|reason=ConnectionError"
        return ACK

    prepared = (
        f"test-request|request_id={REQUEST_ID}|run_id={RUN_ID}"
        "|state=prepared|outcome="
    )
    terminal = _snapshot("terminal", "passed")
    with patch.object(testing, "run_tests", start), \
         patch.object(
             testing, "resolve_test_request", AsyncMock(return_value=prepared)
         ) as resolve, \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(return_value=terminal)
         ) as get_run:
        result = await testing.run_tests_wait(
            mode="EditMode",
            filter="",
            request_id=REQUEST_ID,
            timeout=1.0,
            poll_interval=0.01,
        )

    assert json.loads(result)["outcome"] == "passed"
    assert calls == [
        ("EditMode", None, REQUEST_ID),
        ("EditMode", None, REQUEST_ID),
    ]
    resolve.assert_awaited_once_with(REQUEST_ID)
    get_run.assert_awaited_once_with(RUN_ID)


@pytest.mark.asyncio
async def test_real_wait_flow_reuses_payload_when_prepared_intent_survives_lost_ack(
    monkeypatch,
):
    sent = []
    resolve_count = 0
    dispatch_count = 0
    prepared = (
        f"test-request|request_id={REQUEST_ID}|run_id={RUN_ID}"
        "|state=prepared|outcome="
    )
    terminal_snapshot = json.loads(_snapshot("terminal", "passed"))
    terminal_snapshot["filter"] = "Fixture.Test"
    terminal = json.dumps(terminal_snapshot)

    async def send(command, args, **kwargs):
        nonlocal resolve_count, dispatch_count
        sent.append((command, dict(args)))
        if command == "resolve_test_request":
            resolve_count += 1
            return "none" if resolve_count == 1 else prepared
        if command == "run_tests":
            dispatch_count += 1
            if dispatch_count == 1:
                raise ConnectionError("ACK lost during reload")
            return ACK
        if command == "get_test_run":
            return terminal
        raise AssertionError(command)

    monkeypatch.setattr(testing, "_send", send)
    with patch(
        "unity_mcp.tools.diagnose.diagnose",
        AsyncMock(return_value="CLEAN"),
    ):
        result = await testing.run_tests_wait(
            mode="EditMode",
            filter="Fixture.Test",
            request_id=REQUEST_ID,
            timeout=1.0,
            poll_interval=0.01,
        )

    assert json.loads(result)["outcome"] == "passed"
    dispatches = [args for command, args in sent if command == "run_tests"]
    assert dispatches == [
        {
            "mode": "EditMode",
            "filter": "Fixture.Test",
            "request_id": REQUEST_ID,
        },
        {
            "mode": "EditMode",
            "filter": "Fixture.Test",
            "request_id": REQUEST_ID,
        },
    ]


@pytest.mark.asyncio
async def test_lost_ack_timeout_does_not_invent_run_id():
    async def unknown(mode, filter=None, request_id=None):
        return f"START-UNKNOWN|request_id={request_id}|reason=TimeoutError"

    with patch.object(testing, "run_tests", unknown), \
         patch.object(testing, "resolve_test_request", AsyncMock(return_value="none")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=0.001, poll_interval=1.0
        )

    assert result == (
        f"TIMEOUT|request_id={REQUEST_ID}|run_id=unknown|snapshot=none"
    )


@pytest.mark.asyncio
async def test_snapshot_identity_mismatch_is_protocol_error():
    mismatched = json.dumps({
        "request_id": REQUEST_ID,
        "run_id": "another-run",
        "state": "terminal",
        "outcome": "passed",
    })
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value=mismatched)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert result.startswith(
        f"PROTOCOL-ERROR|request_id={REQUEST_ID}|run_id={RUN_ID}"
    )
    assert "reason=run-id-mismatch" in result


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("missing_field", "reason"),
    (("run_id", "run-id-missing"), ("request_id", "request-id-missing")),
)
async def test_snapshot_requires_both_correlation_identities(
    missing_field, reason
):
    snapshot = {
        "request_id": REQUEST_ID,
        "run_id": RUN_ID,
        "state": "terminal",
        "outcome": "passed",
    }
    snapshot.pop(missing_field)
    with patch.object(testing, "run_tests", _started), \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(return_value=json.dumps(snapshot))
         ):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert result.startswith(
        f"PROTOCOL-ERROR|request_id={REQUEST_ID}|run_id={RUN_ID}"
    )
    assert f"reason={reason}" in result


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("field", "reason"),
    (("is_terminal", "terminal-flag-missing"),
     ("execution_finished", "execution-boundary-missing"),
     ("cleanup_complete", "cleanup-boundary-missing")),
)
async def test_terminal_snapshot_requires_explicit_completion_evidence(field, reason):
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot.pop(field)
    with patch.object(testing, "run_tests", _started), \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(return_value=json.dumps(snapshot))
         ):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert f"reason={reason}" in result


@pytest.mark.asyncio
async def test_terminal_snapshot_must_match_requested_mode_and_filter():
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["filter"] = "Old.Filter"
    with patch.object(testing, "run_tests", _started), \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(return_value=json.dumps(snapshot))
         ):
        result = await testing.run_tests_wait(
            filter="New.Filter", request_id=REQUEST_ID, timeout=1.0, poll_interval=1.0
        )

    assert "reason=request-intent-mismatch" in result


def test_terminal_snapshot_error_reports_no_test_progress():
    snapshot = json.loads(
        _snapshot("terminal", "incomplete", health="no_test_progress")
    )
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    ) == "run-health-no-test-progress"


def test_terminal_snapshot_error_reports_editor_unresponsive():
    snapshot = json.loads(
        _snapshot("terminal", "incomplete", health="editor_unresponsive")
    )
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    ) == "run-health-editor-unresponsive"


def test_terminal_snapshot_error_ignores_missing_health_for_wire_compat():
    """Old plugin never emits `health` -- absence must not change behavior."""
    snapshot = json.loads(_snapshot("terminal", "passed"))
    assert "health" not in snapshot
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    ) is None


def test_terminal_validator_rejects_partial_or_untrusted_evidence():
    mutations = (
        (lambda snapshot: snapshot.update(expected_count=0), "run-zero-match-filter"),
        (lambda snapshot: snapshot.update(build_coherent=False), "build-incoherent"),
        (
            lambda snapshot: snapshot.update(completed_expected_count=6963),
            "terminal-count-invariant",
        ),
        (
            lambda snapshot: snapshot.update(missing_count=1),
            "terminal-count-invariant",
        ),
        (
            lambda snapshot: snapshot.update(conflict_count=1),
            "terminal-count-invariant",
        ),
        (
            lambda snapshot: snapshot.update(
                issues=[{"severity": "error", "code": "INFRASTRUCTURE_ERROR"}]
            ),
            "infrastructure-errors",
        ),
    )
    for mutate, expected_reason in mutations:
        snapshot = json.loads(_snapshot("terminal", "passed"))
        mutate(snapshot)
        assert testing._terminal_snapshot_error(
            snapshot, mode="EditMode", filter_name=""
        ) == expected_reason


def test_zero_match_filter_with_regex_metachar_reason():
    """A nested-class filter's '+' is a .NET separator UTF regex-matches as a
    quantifier, usually zero-matching. Flag it distinctly from an honest miss."""
    filter_name = "...StatSheetTests+OrderIndependence"
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["filter"] = filter_name
    snapshot["expected_count"] = 0
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=filter_name
    ) == "run-zero-match-metachar"


def test_zero_match_filter_without_metachar_reason():
    """Arm B: a plain non-empty filter with zero matches must not be
    misreported as a metachar typo -- discriminates an "always metachar" fix."""
    filter_name = "NoSuchClass"
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["filter"] = filter_name
    snapshot["expected_count"] = 0
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=filter_name
    ) == "run-zero-match-filter"


def test_zero_match_filter_with_group_separator_reason():
    """'|' is UTF's legal multi-group filter separator ("TestA|TestB"), never
    misinterpreted by the engine -- it must not trigger the metachar hint."""
    filter_name = "TestA|TestB"
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["filter"] = filter_name
    snapshot["expected_count"] = 0
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=filter_name
    ) == "run-zero-match-filter"


def test_terminal_snapshot_error_negative_expected_count_stays_invalid():
    """A negative expected_count is corrupted evidence, not a zero-match --
    the original reason must survive the taxonomy split."""
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["expected_count"] = -1
    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    ) == "expected-count-invalid"


def test_unfiltered_editmode_one_test_can_be_green():
    snapshot = json.loads(_snapshot("terminal", "passed"))
    for field in (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "passed",
    ):
        snapshot[field] = 1
    snapshot["skipped"] = 0

    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name=""
    ) is None


@pytest.mark.asyncio
async def test_wait_accepts_unfiltered_editmode_one_test_terminal_snapshot():
    snapshot = json.loads(_snapshot("terminal", "passed"))
    for field in (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "passed",
    ):
        snapshot[field] = 1
    snapshot["skipped"] = 0

    with patch.object(testing, "run_tests", _started), patch.object(
        testing,
        "_fetch_test_run_json",
        AsyncMock(return_value=json.dumps(snapshot)),
    ):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID,
            timeout=1.0,
            poll_interval=0.01,
        )

    assert json.loads(result)["expected_count"] == 1


def test_focused_editmode_run_is_not_subject_to_full_suite_floor():
    snapshot = json.loads(_snapshot("terminal", "passed"))
    snapshot["filter"] = "Fixture.Test"
    for field in (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "passed",
    ):
        snapshot[field] = 1
    snapshot["skipped"] = 0

    assert testing._terminal_snapshot_error(
        snapshot, mode="EditMode", filter_name="Fixture.Test"
    ) is None


@pytest.mark.asyncio
async def test_wait_timeout_bounds_a_hung_poll_by_wall_clock():
    never = asyncio.Event()

    async def hung_poll(_run_id):
        await never.wait()

    started_at = time.monotonic()
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", hung_poll):
        result = await testing.run_tests_wait(
            request_id=REQUEST_ID, timeout=0.03, poll_interval=0.01
        )
    elapsed = time.monotonic() - started_at

    assert result.startswith(
        f"TIMEOUT|request_id={REQUEST_ID}|run_id={RUN_ID}|snapshot="
    )
    assert elapsed < 0.2


def test_compact_snapshot_preserves_cyrillic_without_escaping():
    raw = json.dumps({"run_id": RUN_ID, "request_id": REQUEST_ID, "filter": "Проверка"})
    result = testing._compact_snapshot(raw)
    assert "Проверка" in result
    assert "\\u" not in result


def test_compact_snapshot_ascii_only_is_unchanged_by_ensure_ascii():
    raw = json.dumps({"run_id": RUN_ID, "request_id": REQUEST_ID, "filter": "Fixture.Test"})
    result = testing._compact_snapshot(raw)
    assert result == json.dumps(json.loads(raw), separators=(",", ":"), sort_keys=True)


@pytest.mark.asyncio
async def test_mode_filter_and_request_identity_are_forwarded():
    captured = {}

    async def started(mode, filter=None, request_id=None):
        captured.update(mode=mode, filter=filter, request_id=request_id)
        return "BLOCKED: controlled stop"

    with patch.object(testing, "run_tests", started):
        await testing.run_tests_wait(
            mode="PlayMode",
            filter="ClassA|ClassB",
            request_id=REQUEST_ID,
        )

    assert captured == {
        "mode": "PlayMode",
        "filter": "ClassA|ClassB",
        "request_id": REQUEST_ID,
    }


@pytest.mark.asyncio
async def test_fetch_test_run_json_expected_count_injection_preserves_cyrillic():
    """_fetch_test_run_json's expected_count injection re-serializes the whole
    snapshot -- it must keep ensure_ascii=False so a Cyrillic filter/name already
    in the payload survives as UTF-8, not a \\uXXXX-escaped blob."""
    run_id = "run-cyrillic-expected-count"
    handle = testing._registry.register(run_id, REQUEST_ID)
    handle.expected_count = 3
    raw = json.dumps({"run_id": run_id, "state": "running", "filter": "Тест"})

    with patch.object(testing, "_send", AsyncMock(return_value=raw)):
        result = await testing._fetch_test_run_json(run_id)

    assert "\\u" not in result, f"Cyrillic must not be ASCII-escaped: {result!r}"
    assert json.loads(result) == {
        "run_id": run_id, "state": "running", "filter": "Тест", "expected_count": 3,
    }
