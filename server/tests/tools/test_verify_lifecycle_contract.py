"""Tests for verify_after_change lifecycle contract (Subtask 5, VERIFY-019).

Guards the conditional gate ordering: compile → errors → console → tests → playtests.
Each test verifies that a specific gate is skipped or triggered at the right point.
"""
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.verify as _v

_AWAIT_COMPILE = "unity_mcp.tools.code_intel.await_compile"
_GET_ERRORS = "unity_mcp.tools.console.get_compile_errors"
_GET_CONSOLE_SINCE = "unity_mcp.tools.console.get_console_since"
_RUN_TESTS_WAIT = "unity_mcp.tools.testing.run_tests_wait"
_RUN_SUITE = "unity_mcp.tools.runtime.run_playtest_suite"

MARK = "mark:2000.0"


async def test_verify_skips_playtest_when_no_playtests_param():
    """No playtests param → run_playtest_suite NOT called; compile gate IS called."""
    compile_mock = AsyncMock(return_value="compile clean (1s)")
    suite_mock = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, compile_mock),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, suite_mock),
    ):
        result = await _v.verify_after_change()

    suite_mock.assert_not_called()
    compile_mock.assert_awaited_once()
    assert "PASS" in result
    assert "playtests" not in result.split("|")[0]  # not in pass list


async def test_verify_runs_playtest_suite_when_param_given():
    """playtests='Tests/*.playtest' → run_playtest_suite called exactly once with that pattern."""
    suite_mock = AsyncMock(return_value="SUITE: 2/2 passed (3.0s)\nOK 1.0s a.playtest\nOK 2.0s b.playtest")
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_SUITE, suite_mock),
    ):
        await _v.verify_after_change(playtests="Tests/*.playtest")

    suite_mock.assert_awaited_once()
    assert suite_mock.call_args.args[0] == "Tests/*.playtest"


async def test_verify_fails_fast_on_compile_error():
    """get_compile_errors returning an error → FAIL before console or playtest step."""
    console_mock = AsyncMock()
    suite_mock = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="error CS0246: The type 'Foo' could not be found")),
        patch(_GET_CONSOLE_SINCE, console_mock),
        patch(_RUN_SUITE, suite_mock),
    ):
        result = await _v.verify_after_change(mark_id=MARK, playtests="t.playtest")

    assert "FAIL" in result
    assert "get_compile_errors" in result or "CS0246" in result
    console_mock.assert_not_called()
    suite_mock.assert_not_called()


async def test_verify_console_overflow_sentinel_causes_fail():
    """get_console_since returning 'err: overflow:…' → FAIL: console_overflow; tests skipped."""
    tests_mock = AsyncMock()
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_GET_CONSOLE_SINCE, AsyncMock(
            return_value="err: overflow:7 buffer wrapped, 7 problem entries may be lost"
        )),
        patch(_RUN_TESTS_WAIT, tests_mock),
    ):
        result = await _v.verify_after_change(mark_id=MARK, run_tests_mode="EditMode")

    assert "FAIL" in result
    assert "console_overflow" in result
    tests_mock.assert_not_called()
