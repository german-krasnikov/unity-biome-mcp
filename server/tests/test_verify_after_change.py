"""Tests for tools/verify.py — verify_after_change orchestrator.

All I/O mocked at the imported-module boundary.
"""
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


def _patch_all(compile_result="compile clean (5s)", errors="",
               console="", tests="EditMode: 10/10 passed, 0 failed",
               suite="SUITE: 3/3 passed (12s)"):
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
        patch(_RUN_TESTS_WAIT, AsyncMock(return_value="EditMode: 10/10 passed, 0 failed")),
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
async def test_verify_timeout_passed_to_run_tests_wait():
    mock_tests = AsyncMock(return_value="EditMode: 5/5 passed, 0 failed")
    with (
        patch(_AWAIT_COMPILE, AsyncMock(return_value="compile clean (1s)")),
        patch(_GET_ERRORS, AsyncMock(return_value="")),
        patch(_RUN_TESTS_WAIT, mock_tests),
    ):
        await _v.verify_after_change(run_tests_mode="EditMode", timeout=250.0)
    mock_tests.assert_awaited_once()
    _, kwargs = mock_tests.call_args
    assert kwargs.get("timeout") == 250.0 or mock_tests.call_args.args[2] == 250.0
