"""Tests for tools/verify.py — verify_after_change orchestrator.

All I/O mocked at the imported-module boundary.
"""
import json
import pytest
from unittest.mock import AsyncMock, patch

from unity_mcp import editor_log
import unity_mcp.tools.verify as _v

# Patch targets (verify.py uses module references, not function references)
_AWAIT_COMPILE = "unity_mcp.tools.code_intel.await_compile"
_GET_ERRORS = "unity_mcp.tools.console.get_compile_errors"
_GET_CONSOLE_SINCE = "unity_mcp.tools.console.get_console_since"
_RUN_TESTS_WAIT = "unity_mcp.tools.testing.run_tests_wait"
_RUN_SUITE = "unity_mcp.tools.runtime.run_playtest_suite"

MARK = "mark:1000.0"


# ── _is_compile_clean unit tests ──────────────────────────────────────────────

def test_is_compile_clean_variants():
    from unity_mcp.tools.verify import _is_compile_clean
    assert _is_compile_clean("compile clean (5.2s)")
    assert _is_compile_clean("compile clean")
    assert _is_compile_clean("")
    assert _is_compile_clean("no errors")
    assert _is_compile_clean("No compilation errors")   # C# sentinel
    assert _is_compile_clean("No compilation errors.")  # period suffix
    assert not _is_compile_clean("error CS0234: bad type")
    assert not _is_compile_clean("1 compilation error(s):")


def test_is_compile_clean_rejects_unreachable_sentinel():
    # ARC-6 T5: a dead-Unity UNITY_UNREACHABLE sentinel must never be
    # mistaken for "compile clean" by the mandatory verify gate.
    assert not _v._is_compile_clean(editor_log.UNITY_UNREACHABLE)


def _run_snapshot(
    *,
    state="terminal",
    outcome="passed",
    expected=10,
    completed=10,
    missing=0,
    unexpected=0,
    conflicts=0,
    errors=None,
):
    return json.dumps({
        "request_id": "req-1",
        "run_id": "run-1",
        "utf_guid": "utf-1",
        "state": state,
        "outcome": outcome,
        "is_terminal": state == "terminal",
        "execution_finished": state == "terminal",
        "cleanup_complete": state == "terminal",
        "build_coherent": True,
        "utf_version": "1.6.0",
        "manifest_complete": True,
        "run_started_observed": True,
        "run_finished_observed": state == "terminal",
        "counts": {
            "expected": expected,
            "completed": completed,
            "missing": missing,
            "unexpected": unexpected,
            "conflicts": conflicts,
        },
        "errors": [] if errors is None else errors,
        "issues": [],
    })


@pytest.mark.parametrize("result", [
    _run_snapshot(outcome="failed"),
    _run_snapshot(outcome="incomplete"),
    _run_snapshot(outcome="invalid"),
    _run_snapshot(state="running", completed=4),
    _run_snapshot(completed=9, missing=1),
    _run_snapshot(unexpected=1),
    _run_snapshot(conflicts=1),
    _run_snapshot(errors=["observer failure"]),
    "TIMEOUT|request_id=req-1|run_id=run-1|snapshot={}",
    "START-UNKNOWN|request_id=req-1|reason=TimeoutError",
    "tests-started|request_id=req-1|run_id=run-1|utf_guid=utf-1|state=dispatched",
    "unstructured success text",
])
def test_test_gate_fails_closed_for_non_proven_results(result):
    assert not _v._is_tests_pass(result)


def test_test_gate_accepts_only_complete_terminal_pass_snapshot():
    result = _run_snapshot()
    assert _v._is_tests_pass(result)
    assert _v._extract_ratio(result) == "10/10"


def test_test_gate_accepts_reconciler_summary_field_names():
    result = json.dumps({
        "run_id": "run-1",
        "request_id": "req-1",
        "utf_guid": "utf-1",
        "lifecycle": "terminal",
        "outcome": "passed",
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "build_coherent": True,
        "utf_version": "1.6.0",
        "manifest_complete": True,
        "run_started_observed": True,
        "run_finished_observed": True,
        "expected_count": 10,
        "completed_expected_count": 10,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "issues": [],
    })
    assert _v._is_tests_pass(result)
    assert _v._extract_ratio(result) == "10/10"


def test_test_gate_rejects_legacy_summaries_without_durable_evidence():
    assert not _v._is_tests_pass("EditMode: 10/10 passed, 0 failed")
    assert not _v._is_tests_pass("10 tests: 8 passed, 0 failed, 2 skipped")


@pytest.mark.parametrize("result", [
    "SUITE: 0/0 passed (0.0s)",
    "SUITE: no files matched",
    "SUITE: 1/1 passed (1.0s)\nFAIL hidden failure",
    "SUITE: 1/1 passed (1.0s)\nPLAYTEST: 1/1 ERROR: reset failed",
    "SUITE: 1/1",
    "err: no files",
])
def test_playtest_suite_gate_rejects_empty_or_unproven_results(result):
    assert not _v._is_suite_pass(result)


def test_playtest_suite_gate_accepts_nonempty_complete_clean_result():
    assert _v._is_suite_pass(
        "SUITE: 2/2 passed (1.0s)\nOK 0.4s a.playtest\nOK 0.6s b.playtest"
    )


def test_is_suite_pass_matches_real_producer_output():
    # DEV-52: runtime.py's _format_suite_report always appends
    # " terminal:true play_stopped:<bool>" (and optionally " timed_out:true")
    # to the first line. The gate must accept this real shape.
    assert _v._is_suite_pass(
        "SUITE: 3/3 passed (12.3s) terminal:true play_stopped:true"
    )


def test_is_suite_pass_rejects_corrupted_first_line_with_flags():
    # Double-red guard: a fix that just accepts "anything with flags" is
    # still wrong — the ratio/word structure of the first line still matters.
    assert not _v._is_suite_pass(
        "SUITE: three/3 passed (12.3s) terminal:true play_stopped:true"
    )


def test_is_suite_pass_rejects_timed_out_suite_even_if_ratio_full():
    # DEV-52b: runtime.py's _format_suite_report appends " timed_out:true"
    # on timeout, even when every already-run file passed (passed == total
    # of the files that got to run before the deadline). The old keyword
    # check `\bTIMEOUT\b` never matches "timed_out" (different word), so
    # the gate falsely PASSed a suite that actually timed out.
    assert not _v._is_suite_pass(
        "SUITE: 3/3 passed (12.3s) terminal:true play_stopped:true timed_out:true"
    )


def test_is_suite_pass_accepts_same_result_without_timed_out():
    # Double-red guard: the fix must not start rejecting clean, non-timed-out
    # results that otherwise look identical.
    assert _v._is_suite_pass(
        "SUITE: 3/3 passed (12.3s) terminal:true play_stopped:true"
    )


@pytest.mark.parametrize("field", [
    "is_terminal",
    "execution_finished",
    "cleanup_complete",
    "build_coherent",
    "manifest_complete",
    "run_started_observed",
    "run_finished_observed",
])
def test_test_gate_rejects_missing_or_false_protocol_guarantee(field):
    snapshot = json.loads(_run_snapshot())
    snapshot[field] = False
    assert not _v._is_tests_pass(json.dumps(snapshot))


def _patch_all(compile_result="compile clean (5s)", errors="",
               console="", tests=None,
               suite="SUITE: 3/3 passed (12s)"):
    tests = _run_snapshot() if tests is None else tests
    return [
        patch(_AWAIT_COMPILE, AsyncMock(return_value=compile_result)),
        patch(_GET_ERRORS, AsyncMock(return_value=errors)),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value=console)),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value=tests)),
        patch(_RUN_SUITE, AsyncMock(return_value=suite)),
    ]


@pytest.mark.asyncio
async def test_verify_all_pass():
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (5s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value=_run_snapshot())),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 3/3 passed (12s)")),
    ):
        result = await _v.verify_after_change(
            mark_id=MARK, run_tests_mode="EditMode", playtests="Tests/*.playtest"
        )
    assert result.startswith("PASS("), f"Expected PASS(N/5): prefix, got: {result!r}"
    assert "compile" in result
    assert "tests(10/10)" in result
    assert "playtests(3/3)" in result


@pytest.mark.asyncio
async def test_verify_all_pass_with_real_producer_suite_format():
    # DEV-52 symptom: run_playtest_suite's real output always carries the
    # " terminal:true play_stopped:true" suffix on its first line. Before the
    # fix, _is_suite_pass's re.fullmatch never matched this shape, so the
    # mandatory playtest-suite gate always FAILed even on a clean suite.
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (5s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value=_run_snapshot())),
        patch(_RUN_SUITE, AsyncMock(
            return_value="SUITE: 3/3 passed (12.3s) terminal:true play_stopped:true"
        )),
    ):
        result = await _v.verify_after_change(
            mark_id=MARK, run_tests_mode="EditMode", playtests="Tests/*.playtest"
        )
    assert result.startswith("PASS("), f"Expected PASS(N/5): prefix, got: {result!r}"
    assert "playtests(3/3)" in result


@pytest.mark.asyncio
async def test_verify_compile_errors_fail():
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="error CS0246: type not found")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value="should not be called")),
    ):
        result = await _v.verify_after_change(run_tests_mode="EditMode")
    assert result.startswith("FAIL: await_compile")
    assert "CS0246" in result


@pytest.mark.asyncio
async def test_verify_compile_unreachable_sentinel_fails_gate():
    # ARC-6 T5 integration proof: a dead Unity connection must FAIL the
    # mandatory gate, never PASS as if Unity were clean.
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value=editor_log.UNITY_UNREACHABLE)),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value="should not be called")),
    ):
        result = await _v.verify_after_change(run_tests_mode="EditMode")
    assert result.startswith("FAIL: await_compile")
    assert editor_log.UNITY_UNREACHABLE in result


@pytest.mark.asyncio
async def test_verify_console_errors_fail():
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="[Error] NullReferenceException in Foo.cs")),
        patch(_RUN_TESTS_WAIT, AsyncMock()) as mock_tests,
    ):
        result = await _v.verify_after_change(mark_id=MARK, run_tests_mode="EditMode")
    assert result.startswith("FAIL: console_since")
    assert "NullReferenceException" in result
    mock_tests.assert_not_called()


@pytest.mark.asyncio
async def test_verify_tests_fail():
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value="EditMode: 8/10 passed, 2 failed\nFAIL MyTest1\nFAIL MyTest2")),
        patch(_RUN_SUITE, AsyncMock()) as mock_suite,
    ):
        result = await _v.verify_after_change(run_tests_mode="EditMode", playtests="p.playtest")
    assert result.startswith("FAIL: tests")
    assert "recommended" in result
    assert "MyTest1" in result or "MyTest2" in result
    mock_suite.assert_not_called()


@pytest.mark.asyncio
async def test_verify_playtests_fail():
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 2/3 passed (8s)\nFAIL tests/a.playtest")),
    ):
        result = await _v.verify_after_change(playtests="Tests/*.playtest")
    assert result.startswith("FAIL: playtests")
    assert "2/3" in result


@pytest.mark.asyncio
async def test_verify_skips_disabled_gates():
    """No mark_id → console skipped. No run_tests_mode → tests skipped."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock()) as mock_con,
        patch(_RUN_TESTS_WAIT, AsyncMock()) as mock_tests,
        patch(_RUN_SUITE, AsyncMock()) as mock_suite,
    ):
        result = await _v.verify_after_change()
    assert result.startswith("PASS("), f"Expected PASS(N/5): prefix, got: {result!r}"
    assert "SKIPPED: console, tests, playtests" in result
    mock_con.assert_not_called()
    mock_tests.assert_not_called()
    mock_suite.assert_not_called()


@pytest.mark.asyncio
async def test_verify_compile_only():
    """Only compile gates enabled → PASS with just compile + errors_clean."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
    ):
        result = await _v.verify_after_change()
    assert result == "PASS(2/5): compile + errors_clean | SKIPPED: console, tests, playtests"


@pytest.mark.asyncio
async def test_verify_partial_optional_shows_remaining_skipped():
    """A4: Only mark_id → SKIPPED must list tests and playtests."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="")),
    ):
        result = await _v.verify_after_change(mark_id=MARK)
    assert "SKIPPED: tests, playtests" in result
    assert "SKIPPED: console" not in result


@pytest.mark.asyncio
async def test_verify_all_gates_no_skipped_suffix():
    """A4: All 3 optional gates enabled → no SKIPPED section."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value=_run_snapshot())),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 3/3 passed (12s)")),
    ):
        result = await _v.verify_after_change(
            mark_id=MARK, run_tests_mode="EditMode", playtests="*.playtest"
        )
    assert "SKIPPED" not in result
    assert result.startswith("PASS("), f"Expected PASS(N/5): prefix, got: {result!r}"


@pytest.mark.asyncio
async def test_verify_stops_on_first_failure():
    """Compile error → tests and playtests never called."""
    mock_tests = AsyncMock()
    mock_suite = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="error CS1234: bad code")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, mock_tests),
        patch(_RUN_SUITE, mock_suite),
    ):
        result = await _v.verify_after_change(run_tests_mode="EditMode", playtests="a.playtest")
    assert "FAIL" in result
    mock_tests.assert_not_called()
    mock_suite.assert_not_called()


@pytest.mark.asyncio
async def test_verify_console_gate_ignores_dropped_count_line():
    """Synthetic dropped-count line should not trigger console gate failure."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="[+3 older problem entries dropped]")),
    ):
        result = await _v.verify_after_change(mark_id=MARK)
    assert "PASS" in result
    assert "console_clean" in result


@pytest.mark.asyncio
async def test_verify_console_gate_fails_on_real_error_with_dropped_line():
    """Real error alongside dropped-count line must still fail."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(
            return_value="[Error] NullReferenceException\n[+2 older problem entries dropped]"
        )),
    ):
        result = await _v.verify_after_change(mark_id=MARK)
    assert "FAIL" in result
    assert "NullReferenceException" in result


@pytest.mark.asyncio
async def test_verify_timeout_passed_to_run_tests_wait():
    mock_tests = AsyncMock(return_value=_run_snapshot(expected=5, completed=5))
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, mock_tests),
    ):
        await _v.verify_after_change(run_tests_mode="EditMode", timeout=250.0)
    mock_tests.assert_awaited_once()
    _, kwargs = mock_tests.call_args
    assert kwargs.get("timeout") == 250.0 or mock_tests.call_args.args[2] == 250.0


# ── G47: restart_between ──────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_verify_restart_between_delegates_verified_reset_to_suite():
    """The runtime suite centrally owns the verified initial and between-file reset."""
    suite_mock = AsyncMock(return_value="SUITE: 1/1 passed (1.0s)")
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, suite_mock),
    ):
        result = await _v.verify_after_change(playtests="t.playtest", restart_between=True)
    assert "PASS" in result
    suite_mock.assert_awaited_once_with(
        "t.playtest", auto_play=False, restart_between=True, suite_timeout=300.0
    )


@pytest.mark.asyncio
async def test_verify_restart_between_false_forwards_no_lifecycle_ownership():
    """The default suite call neither auto-starts nor requests isolated restarts."""
    suite_mock = AsyncMock(return_value="SUITE: 1/1 passed (1.0s)")
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, suite_mock),
    ):
        result = await _v.verify_after_change(playtests="t.playtest", restart_between=False)
    assert "PASS" in result
    suite_mock.assert_awaited_once_with(
        "t.playtest", auto_play=False, restart_between=False, suite_timeout=300.0
    )


@pytest.mark.asyncio
async def test_verify_playtest_suite_exception_returns_fail():
    suite_mock = AsyncMock(side_effect=ConnectionError("Unity disconnected"))
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, suite_mock),
    ):
        result = await _v.verify_after_change(
            playtests="t.playtest", restart_between=True
        )

    assert result.startswith("FAIL: playtests gate failed")
    assert "ConnectionError" in result
    assert "Unity disconnected" in result


# ── P-NEW-3: Overflow gate ────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_verify_fails_on_overflow():
    """get_console_since returning 'err: overflow:5 …' → FAIL: console_overflow."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="err: overflow:5 buffer wrapped, 5 problem entries may be lost")),
        patch(_RUN_TESTS_WAIT, AsyncMock()) as mock_tests,
    ):
        result = await _v.verify_after_change(mark_id=MARK, run_tests_mode="EditMode")
    assert result.startswith("FAIL:"), f"Expected FAIL, got: {result!r}"
    assert "console_overflow" in result
    mock_tests.assert_not_called()


@pytest.mark.asyncio
async def test_verify_overflow_skips_remaining_gates():
    """Overflow failure reports subsequent gates as skipped."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (2s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="err: overflow:3 buffer wrapped, 3 problem entries may be lost")),
        patch(_RUN_TESTS_WAIT, AsyncMock()) as mock_tests,
    ):
        result = await _v.verify_after_change(mark_id=MARK, run_tests_mode="EditMode")
    assert "tests" in result  # reported as skipped gate
    mock_tests.assert_not_called()


# V7: PASS fraction — Phase 0

@pytest.mark.asyncio
async def test_pass_includes_gate_fraction_compile_only():
    """PASS(2/5) when only compile + errors_clean gates run (no optional gates)."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
    ):
        result = await _v.verify_after_change()
    assert result.startswith("PASS(2/5):"), f"Expected PASS(2/5): prefix, got: {result!r}"


@pytest.mark.asyncio
async def test_pass_includes_gate_fraction_all_gates():
    """PASS(5/5) when all 5 gates run."""
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value=_run_snapshot())),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 3/3 passed (12s)")),
    ):
        result = await _v.verify_after_change(
            mark_id=MARK, run_tests_mode="EditMode", playtests="Tests/*.playtest"
        )
    assert result.startswith("PASS(5/5):"), f"Expected PASS(5/5): prefix, got: {result!r}"
