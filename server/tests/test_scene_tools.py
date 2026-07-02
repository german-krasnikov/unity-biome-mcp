"""Unit tests for scene.py tool functions — keyword filter and undo_last."""
import pytest
from unittest.mock import AsyncMock, MagicMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.scene as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def scene_mod():
    import unity_mcp.tools.scene as mod
    return mod


@pytest.fixture
def mock_diagnose(monkeypatch):
    """Patch diagnose.diagnose so run_tests pre-flight uses controlled verdict."""
    import unity_mcp.tools.diagnose as diag_mod
    mock = AsyncMock()
    monkeypatch.setattr(diag_mod, "diagnose", mock)
    return mock


# ── T4: get_console keyword / count_only ─────────────────────────────────────

@pytest.mark.asyncio
async def test_get_console_keyword_passed_to_send(scene_mod, _patch_send):
    await scene_mod.get_console(count=5, keyword="NullRef")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_console"
    assert call_args[0][1].get("keyword") == "NullRef"


@pytest.mark.asyncio
async def test_get_console_count_only_passed_to_send(scene_mod, _patch_send):
    await scene_mod.get_console(count=20, count_only=True)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("count_only") == "true"


@pytest.mark.asyncio
async def test_get_console_count_only_false_omitted(scene_mod, _patch_send):
    """count_only=False should not appear in args (filtered by _args)."""
    await scene_mod.get_console(count=5, count_only=False)

    call_args = _patch_send.call_args
    assert "count_only" not in call_args[0][1]


@pytest.mark.asyncio
async def test_get_console_keyword_none_omitted(scene_mod, _patch_send):
    await scene_mod.get_console(count=5)

    call_args = _patch_send.call_args
    assert "keyword" not in call_args[0][1]


# ── S5: get_console since ─────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_get_console_since_sends_param(scene_mod, _patch_send):
    await scene_mod.get_console(count=10, since=30.0)

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_console"
    assert call_args[0][1].get("since") == 30.0


@pytest.mark.asyncio
async def test_get_console_since_none_omitted(scene_mod, _patch_send):
    """since=None should not appear in args (filtered by _args)."""
    await scene_mod.get_console(count=5)

    call_args = _patch_send.call_args
    assert "since" not in call_args[0][1]


# ── T5: undo_last ─────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_undo_last_sends_correct_command(scene_mod, _patch_send):
    await scene_mod.undo_last()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "undo_last"
    assert call_args[0][1].get("turns") == 1


@pytest.mark.asyncio
async def test_undo_last_passes_turns_param(scene_mod, _patch_send):
    await scene_mod.undo_last(turns=3)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("turns") == 3


# ── Pre-flight gate — blocked verdicts ────────────────────────────────────────

@pytest.mark.asyncio
async def test_run_tests_blocked_by_compile_error(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "FAILED:CS0001"
    result = await scene_mod.run_tests()
    assert result.startswith("BLOCKED:")
    assert "FAILED:CS0001" in result
    _patch_send.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_blocked_by_wedge(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "WEDGE-ENGINE"
    result = await scene_mod.run_tests()
    assert result.startswith("BLOCKED:")
    assert "WEDGE-ENGINE" in result
    _patch_send.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_blocked_by_build_failed_wedge(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = (
        "BUILD-FAILED-WEDGE: reload failed on unknown — "
        "reimport the file: package (sync), do NOT restart"
    )
    result = await scene_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_blocked_by_rebuilding(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "REBUILDING"
    result = await scene_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_blocked_by_stale_domain(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "STALE-DOMAIN"
    result = await scene_mod.run_tests()
    assert result.startswith("BLOCKED:")
    _patch_send.assert_not_called()


@pytest.mark.asyncio
async def test_run_tests_proceeds_on_clean(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.return_value = "CLEAN-LIVE"
    result = await scene_mod.run_tests()
    assert "tests-started" in result or result == "ok"
    _patch_send.assert_called_once()


@pytest.mark.asyncio
async def test_run_tests_degrades_on_diagnose_failure(scene_mod, _patch_send, mock_diagnose):
    mock_diagnose.side_effect = RuntimeError("disk read failed")
    result = await scene_mod.run_tests()
    assert "tests-started" in result or result == "ok"
    _patch_send.assert_called_once()


@pytest.mark.asyncio
async def test_run_tests_propagates_tool_error(scene_mod, _patch_send, mock_diagnose):
    from mcp.server.fastmcp.exceptions import ToolError
    mock_diagnose.side_effect = ToolError("Unity connection dead")
    with pytest.raises(ToolError, match="Unity connection dead"):
        await scene_mod.run_tests()
    _patch_send.assert_not_called()


# ── get_test_count ─────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_get_test_count_sends_command(scene_mod, _patch_send):
    await scene_mod.get_test_count()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_test_count"
    assert call_args[0][1] == {}


@pytest.mark.asyncio
async def test_get_test_count_returns_result(scene_mod, _patch_send):
    _patch_send.return_value = "42|edit=30|play=12"

    result = await scene_mod.get_test_count()

    assert result == "42|edit=30|play=12"


# ── ping_object / get_selection ──────────────────────────────────────────────

@pytest.mark.asyncio
async def test_ping_object_sends_command(scene_mod, _patch_send):
    await scene_mod.ping_object(path="/Test")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "ping_object"
    assert call_args[0][1] == {"path": "/Test"}


@pytest.mark.asyncio
async def test_get_selection_sends_command(scene_mod, _patch_send):
    await scene_mod.get_selection()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_selection"
    assert call_args[0][1] == {}


# ── scene_environment ────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_scene_environment_get_sends_command(scene_mod, _patch_send):
    await scene_mod.scene_environment()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene_environment"
    assert call_args[0][1] == {"action": "get"}


@pytest.mark.asyncio
async def test_scene_environment_set_sends_params(scene_mod, _patch_send):
    await scene_mod.scene_environment(action="set", prop="fog", value="true")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "scene_environment"
    assert call_args[0][1] == {"action": "set", "prop": "fog", "value": "true"}
