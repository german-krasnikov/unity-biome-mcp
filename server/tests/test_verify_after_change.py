"""Tests for tools/verify.py — verify_after_change orchestrator.

All I/O mocked at the imported-module boundary.
"""
import json
import pytest
from unittest.mock import AsyncMock, patch

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
    assert result.startswith("PASS:")
    assert "compile" in result
    assert "tests(10/10)" in result
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
    assert result.startswith("PASS:")
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
    assert result == "PASS: compile + errors_clean"


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

_STOP_PLAY = "unity_mcp.tools.verify._stop_play_mode"


@pytest.mark.asyncio
async def test_verify_restart_between_stops_play_before_playtests():
    """restart_between=True calls _stop_play_mode before playtest suite."""
    stop_mock = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_STOP_PLAY, stop_mock),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 1/1 passed (1.0s)")),
    ):
        result = await _v.verify_after_change(playtests="t.playtest", restart_between=True)
    assert "PASS" in result
    stop_mock.assert_called_once()


@pytest.mark.asyncio
async def test_verify_restart_between_false_no_stop():
    """restart_between=False (default) must not call _stop_play_mode."""
    stop_mock = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_STOP_PLAY, stop_mock),
        patch(_RUN_SUITE, AsyncMock(return_value="SUITE: 1/1 passed (1.0s)")),
    ):
        result = await _v.verify_after_change(playtests="t.playtest", restart_between=False)
    assert "PASS" in result
    stop_mock.assert_not_called()
