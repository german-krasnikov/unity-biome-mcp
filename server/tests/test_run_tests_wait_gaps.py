"""Gap tests for run_tests_wait: 'none' handling, multi-phase ConnectionErrors, TCP exception."""
import pytest
from unittest.mock import AsyncMock, patch
import unity_mcp.tools.testing as _t


@pytest.fixture(autouse=True)
def _reset_send():
    original = _t._send
    yield
    _t._send = original


# T1: "none" is treated as pending — must NOT cause early exit
async def test_run_tests_wait_none_kept_as_pending():
    polls = iter(["none", "none", "PASSED: 7/7"])

    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", AsyncMock(side_effect=polls)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait()

    assert result == "PASSED: 7/7"
    assert result != "none"


# T2: all polls return "none" → TIMEOUT captures "none" as last value
async def test_run_tests_wait_all_none_polls_timeout_says_none():
    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", AsyncMock(return_value="none")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait(timeout=0.001, poll_interval=1.0)

    assert result.startswith("TIMEOUT:")
    assert "none" in result


# T7: 2 ConnectionErrors → "none" → final result
async def test_run_tests_wait_three_phase_reconnect():
    call_count = [0]

    async def fake_get_test_results():
        call_count[0] += 1
        if call_count[0] == 1:
            raise ConnectionError("domain reload")
        if call_count[0] == 2:
            raise ConnectionError("still reloading")
        if call_count[0] == 3:
            return "none"
        return "PASSED: 8/8"

    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", fake_get_test_results), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait(timeout=60.0)

    assert result == "PASSED: 8/8"
    assert call_count[0] == 4


# T8: TCP exception in run_tests → swallowed → sentinel → polling resumes → result
async def test_run_tests_wait_tcp_exception_in_run_tests_swallowed():
    polls = iter(["pending", "PASSED: 5/5"])

    async def _tcp_dead(cmd, args=None, **kwargs):
        if cmd == "run_tests":
            raise ConnectionError("domain reload mid-run_tests")
        if cmd == "get_test_results":
            return next(polls)
        raise Exception("diagnose unavailable")

    _t._send = _tcp_dead

    # Patch diagnose to avoid ToolError propagation from preflight
    with patch("unity_mcp.tools.diagnose.diagnose", AsyncMock(return_value="CLEAN")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait(timeout=60.0)

    assert result == "PASSED: 5/5"
