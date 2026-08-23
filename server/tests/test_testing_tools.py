"""Unit tests for the durable Unity test-run tools."""
import asyncio
import pytest
from unittest.mock import AsyncMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.testing as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def testing_mod():
    import unity_mcp.tools.testing as mod
    return mod


@pytest.fixture
def mock_diagnose(monkeypatch):
    """Patch diagnose.diagnose so run_tests pre-flight uses controlled verdict."""
    import unity_mcp.tools.diagnose as diag_mod
    mock = AsyncMock()
    monkeypatch.setattr(diag_mod, "diagnose", mock)
    return mock


# ── Pre-flight gate — blocked verdicts ────────────────────────────────────────

async def test_run_tests_blocked_by_compile_error(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "FAIL:CS0001"
    result = await testing_mod.run_tests()
    assert result.startswith("BLOCKED:")
    assert "FAIL:CS0001" in result
    _patch_send.assert_not_called()


async def test_run_tests_blocked_by_wedge(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "WEDGE-ENGINE"
    result = await testing_mod.run_tests()
    assert result.startswith("BLOCKED:")
    assert "WEDGE-ENGINE" in result
    _patch_send.assert_not_called()


async def test_run_tests_blocked_by_build_failed_wedge(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = (
        "BUILD-FAILED-WEDGE: reload failed on unknown — "
        "reimport the file: package (sync), do NOT restart"
    )
    result = await testing_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


async def test_run_tests_blocked_by_rebuilding(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "REBUILDING"
    result = await testing_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


async def test_run_tests_blocked_by_stale_domain(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "STALE-DOMAIN"
    result = await testing_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


async def test_run_tests_proceeds_on_clean(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "CLEAN-LIVE"
    ack = (
        "tests-started|request_id=req-1|run_id=run-1|utf_guid=utf-1|state=dispatched"
    )
    _patch_send.side_effect = ["none", ack]
    result = await testing_mod.run_tests(request_id="req-1")
    assert result == ack
    assert _patch_send.await_count == 2


async def test_run_tests_degrades_on_diagnose_failure(testing_mod, _patch_send, mock_diagnose):
    mock_diagnose.side_effect = RuntimeError("disk read failed")
    ack = (
        "tests-started|request_id=req-2|run_id=run-2|utf_guid=utf-2|state=dispatched"
    )
    _patch_send.side_effect = ["none", ack]
    result = await testing_mod.run_tests(request_id="req-2")
    assert result == ack
    assert _patch_send.await_count == 2


async def test_run_tests_propagates_tool_error(testing_mod, _patch_send, mock_diagnose):
    from mcp.server.fastmcp.exceptions import ToolError
    mock_diagnose.side_effect = ToolError("Unity connection dead")
    with pytest.raises(ToolError, match="Unity connection dead"):
        await testing_mod.run_tests()
    _patch_send.assert_not_called()


# ── get_test_count ─────────────────────────────────────────────────────────────

async def test_get_test_count_sends_command(testing_mod, _patch_send):
    await testing_mod.get_test_count()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_test_count"
    assert call_args[0][1] == {}


async def test_get_test_count_returns_result(testing_mod, _patch_send):
    _patch_send.return_value = "42|edit=30|play=12"

    result = await testing_mod.get_test_count()

    assert result == "42|edit=30|play=12"


# ── get_test_results ──────────────────────────────────────────────────────────

async def test_get_test_results_sends_command(testing_mod, _patch_send):
    await testing_mod.get_test_results()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_test_results"
    assert call_args[0][1] == {}


async def test_get_test_results_returns_pending_on_connection_error(testing_mod, _patch_send):
    """get_test_results returns 'pending' when bridge is disconnected (domain reload)."""
    _patch_send.side_effect = Exception("Connection lost")
    result = await testing_mod.get_test_results()
    assert result == "pending"


async def test_get_test_results_forwards_run_identity(testing_mod, _patch_send):
    await testing_mod.get_test_results("run-7")
    _patch_send.assert_awaited_once_with("get_test_results", {"run_id": "run-7"})


# ── get_test_progress ─────────────────────────────────────────────────────────

async def test_get_test_progress_returns_send_result(testing_mod, _patch_send):
    _patch_send.return_value = "running|142|140|2|0|5864|18.3|eta=45s"

    result = await testing_mod.get_test_progress()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_test_progress"
    assert call_args[0][1] == {}
    assert result == "running|142|140|2|0|5864|18.3|eta=45s"


async def test_get_test_progress_returns_pending_on_exception(testing_mod, _patch_send):
    _patch_send.side_effect = Exception("Connection lost")

    result = await testing_mod.get_test_progress()

    assert result == "pending"


async def test_get_test_progress_forwards_run_identity(testing_mod, _patch_send):
    await testing_mod.get_test_progress("run-8")
    _patch_send.assert_awaited_once_with("get_test_progress", {"run_id": "run-8"})


# -- Exact run protocol -------------------------------------------------------

async def test_resolve_test_request_sends_identity(testing_mod, _patch_send):
    await testing_mod.resolve_test_request("req-1")
    _patch_send.assert_awaited_once_with(
        "resolve_test_request", {"request_id": "req-1"}
    )


async def test_get_test_run_sends_identity(testing_mod, _patch_send):
    await testing_mod.get_test_run("run-1")
    _patch_send.assert_awaited_once_with("get_test_run", {"run_id": "run-1"})


async def test_cancel_test_run_sends_identity(testing_mod, _patch_send):
    await testing_mod.cancel_test_run("run-1")
    _patch_send.assert_awaited_once_with("cancel_test_run", {"run_id": "run-1"})


async def test_list_test_runs_clamps_limit(testing_mod, _patch_send):
    await testing_mod.list_test_runs(1000)
    _patch_send.assert_awaited_once_with("list_test_runs", {"limit": 100})


# ── Terminal outcomes ─────────────────────────────────────────────────────────

def test_dirty_scene_blocked_is_terminal(testing_mod):
    assert "dirty_scene_blocked" in testing_mod._TERMINAL_OUTCOMES
