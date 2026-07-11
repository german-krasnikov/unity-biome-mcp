"""Tests for run_tests_wait — blocking polling wrapper around run_tests/get_test_results."""
import pytest
from unittest.mock import AsyncMock, patch
import unity_mcp.tools.testing as _t


@pytest.mark.asyncio
async def test_run_tests_wait_returns_final_result():
    """First poll returns 'pending', second returns final result."""
    polls = iter(["pending", "PASSED: 10/10"])

    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", AsyncMock(side_effect=polls)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait()

    assert result == "PASSED: 10/10"


@pytest.mark.asyncio
async def test_run_tests_wait_timeout():
    """Always pending → TIMEOUT: <last_progress>."""
    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", AsyncMock(return_value="pending")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait(timeout=0.001, poll_interval=1.0)

    assert result.startswith("TIMEOUT:")


@pytest.mark.asyncio
async def test_run_tests_wait_blocked_compile_errors():
    """run_tests BLOCKED → propagated immediately, no polling."""
    async def fake_run_tests(mode, filter=None):
        return "BLOCKED: FAILED:CS0117 — fix domain state before running tests"

    mock_poll = AsyncMock()
    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", mock_poll):
        result = await _t.run_tests_wait()

    assert result.startswith("BLOCKED:")
    mock_poll.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_wait_domain_reload_survival():
    """Exception on first poll treated as pending; succeeds on second."""
    call_count = 0

    async def fake_get_test_results():
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            raise ConnectionError("domain reload")
        return "PASSED: 5/5"

    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", fake_get_test_results), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait()

    assert result == "PASSED: 5/5"


@pytest.mark.asyncio
async def test_run_tests_wait_never_returns_tests_started():
    """Final return value never contains the tests-started sentinel."""
    polls = iter(["pending", "pending", "FAILED: 2/10"])

    async def fake_run_tests(mode, filter=None):
        return "tests-started|EditMode|poll..."

    with patch.object(_t, "run_tests", fake_run_tests), \
         patch.object(_t, "get_test_results", AsyncMock(side_effect=polls)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await _t.run_tests_wait()

    assert "tests-started" not in result


@pytest.mark.asyncio
async def test_run_tests_wait_with_filter():
    """mode and filter are forwarded to run_tests."""
    captured = {}

    async def fake_run_tests(mode, filter=None):
        captured["mode"] = mode
        captured["filter"] = filter
        return "PASSED: 3/3"

    with patch.object(_t, "run_tests", fake_run_tests):
        await _t.run_tests_wait(mode="PlayMode", filter="ClassA|ClassB")

    assert captured == {"mode": "PlayMode", "filter": "ClassA|ClassB"}
