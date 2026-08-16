"""TDD tests for debug_tool: classify_symptom, build_commands, format_diagnostic, debug()."""
import pytest
from unittest.mock import AsyncMock


# ---------------------------------------------------------------------------
# 1. classify_symptom
# ---------------------------------------------------------------------------

def test_classify_symptom_movement_returns_inspect_and_console():
    from unity_mcp.tools.debug_tool import classify_symptom
    tools, _ = classify_symptom("enemy doesn't move")
    assert "inspect" in tools
    assert "get_console" in tools


def test_classify_symptom_no_match_defaults_to_console():
    from unity_mcp.tools.debug_tool import classify_symptom
    tools, _ = classify_symptom("something completely random")
    assert "get_console" in tools


def test_classify_symptom_attack_keyword():
    from unity_mcp.tools.debug_tool import classify_symptom
    tools, _ = classify_symptom("player can't attack")
    assert "inspect" in tools



def test_classify_symptom_multiple_keywords():
    from unity_mcp.tools.debug_tool import classify_symptom
    tools, _ = classify_symptom("animation and collision issue")
    assert "inspect" in tools


def test_classify_symptom_movement_returns_relevant_components():
    from unity_mcp.tools.debug_tool import classify_symptom
    _, components = classify_symptom("enemy doesn't move")
    assert any(c in components for c in ["NavMeshAgent", "Rigidbody", "CharacterController", "Transform"])


def test_classify_symptom_no_match_returns_empty_components():
    from unity_mcp.tools.debug_tool import classify_symptom
    _, components = classify_symptom("something completely random")
    assert components == []


# ---------------------------------------------------------------------------
# 2. build_commands
# ---------------------------------------------------------------------------

def test_build_commands_contains_inspect_when_path_given():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect"], "/Enemy_01")
    assert "inspect" in cmds


def test_build_commands_path_appears_in_output():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect", "get_console"], "/Player")
    assert "/Player" in cmds


def test_build_commands_includes_console_when_in_tools():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect", "get_console"], "/Enemy")
    assert "get_console" in cmds


def test_build_commands_excludes_console_when_not_in_tools():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect"], "/Enemy")
    assert "get_console" not in cmds


def test_build_commands_screenshot_when_in_tools():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect", "screenshot"], "/Cam")
    assert "screenshot" in cmds



def test_build_commands_includes_components_when_given():
    from unity_mcp.tools.debug_tool import build_commands
    cmds = build_commands(["inspect"], "/Enemy", ["NavMeshAgent", "Rigidbody"])
    assert "NavMeshAgent" in cmds or "Rigidbody" in cmds


# ---------------------------------------------------------------------------
# 3. format_diagnostic
# ---------------------------------------------------------------------------

def test_format_diagnostic_contains_symptom():
    from unity_mcp.tools.debug_tool import format_diagnostic
    result = format_diagnostic("some data", "enemy frozen", "/Enemy")
    assert "enemy frozen" in result


def test_format_diagnostic_contains_path():
    from unity_mcp.tools.debug_tool import format_diagnostic
    result = format_diagnostic("data", "symptom", "/Player")
    assert "/Player" in result



def test_format_diagnostic_empty_inputs():
    from unity_mcp.tools.debug_tool import format_diagnostic
    result = format_diagnostic("raw data", "", "")
    assert "raw data" in result


# ---------------------------------------------------------------------------
# 4. debug() with mocked _send
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_debug_calls_batch():
    import unity_mcp.tools.debug_tool as mod
    mock_send = AsyncMock(return_value="path=/Enemy\nNavMeshAgent\n  speed: 3.5")
    mod._send = mock_send
    await mod.debug(symptom="enemy doesn't move", path="/Enemy")
    mock_send.assert_called_once()
    assert mock_send.call_args[0][0] == "batch"


@pytest.mark.asyncio
async def test_debug_batch_uses_on_error_continue():
    import unity_mcp.tools.debug_tool as mod
    mock_send = AsyncMock(return_value="data")
    mod._send = mock_send
    await mod.debug(symptom="enemy doesn't move", path="/Enemy")
    batch_kwargs = mock_send.call_args[0][1]
    assert batch_kwargs.get("on_error") == "continue"


@pytest.mark.asyncio
async def test_debug_gather_override_uses_custom_tools():
    import unity_mcp.tools.debug_tool as mod
    mock_send = AsyncMock(return_value="console: no errors")
    mod._send = mock_send
    await mod.debug(gather="get_console", path="/Player")
    mock_send.assert_called_once()
    assert mock_send.call_args[0][0] == "batch"


@pytest.mark.asyncio
async def test_debug_gather_strips_whitespace():
    import unity_mcp.tools.debug_tool as mod
    mock_send = AsyncMock(return_value="data")
    mod._send = mock_send
    await mod.debug(gather="inspect, get_console", path="/Player")
    commands = mock_send.call_args[0][1]["commands"]
    assert "inspect" in commands
    assert "get_console" in commands


@pytest.mark.asyncio
async def test_debug_rejects_screenshot_gather_before_transport():
    from mcp.server.fastmcp.exceptions import ToolError
    import unity_mcp.tools.debug_tool as mod

    mock_send = AsyncMock(return_value="must not be reached")
    mod._send = mock_send

    with pytest.raises(ToolError, match="direct typed call"):
        await mod.debug(gather="inspect, screenshot", path="/Player")

    mock_send.assert_not_awaited()


@pytest.mark.asyncio
async def test_debug_returns_string():
    import unity_mcp.tools.debug_tool as mod
    mod._send = AsyncMock(return_value="data")
    result = await mod.debug(symptom="button not clickable")
    assert isinstance(result, str)


@pytest.mark.asyncio
async def test_debug_result_contains_symptom():
    import unity_mcp.tools.debug_tool as mod
    mod._send = AsyncMock(return_value="Transform\n  position: 0,0,0")
    result = await mod.debug(symptom="player is stuck", path="/Player")
    assert "player is stuck" in result
