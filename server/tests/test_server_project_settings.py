import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import project_settings


async def test_get_tags(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Untagged\nPlayer\nEnemy"})
    result = await project_settings(action="get", target="tags")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "tags"}, timeout=30.0)
    assert "Player" in result


async def test_set_tag(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Tag added"})
    result = await project_settings(action="set", target="tags", prop="Enemy")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "set", "target": "tags", "prop": "Enemy"}, timeout=30.0
    )
    assert "added" in result


async def test_get_layers(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0: Default\n8: Interactable"})
    result = await project_settings(action="get", target="layers")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "layers"}, timeout=30.0)
    assert "Default" in result


async def test_set_layer(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Layer set"})
    result = await project_settings(action="set", target="layers", index=8, value="Interactable")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "set", "target": "layers", "index": 8, "value": "Interactable"}, timeout=30.0
    )
    assert "set" in result


async def test_get_physics(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "gravity: (0,-9.81,0)\nbounceThreshold: 2"})
    result = await project_settings(action="get", target="physics")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "physics"}, timeout=30.0)
    assert "gravity" in result


async def test_set_physics_gravity(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "gravity set"})
    result = await project_settings(action="set", target="physics", prop="gravity", value="(0,-20,0)")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "set", "target": "physics", "prop": "gravity", "value": "(0,-20,0)"}, timeout=30.0
    )
    assert "gravity" in result and "set" in result, result


async def test_get_time(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "fixedDeltaTime: 0.02\ntimeScale: 1"})
    result = await project_settings(action="get", target="time")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "time"}, timeout=30.0)
    assert "fixedDeltaTime" in result


async def test_set_time(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "fixedDeltaTime set"})
    result = await project_settings(action="set", target="time", prop="fixedDeltaTime", value="0.01")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "set", "target": "time", "prop": "fixedDeltaTime", "value": "0.01"}, timeout=30.0
    )
    assert "fixedDeltaTime" in result and "set" in result, result


async def test_get_player(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "companyName: Acme\nproductName: MyGame"})
    result = await project_settings(action="get", target="player")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "player"}, timeout=30.0)
    assert "companyName" in result


async def test_set_player(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "companyName set"})
    result = await project_settings(action="set", target="player", prop="companyName", value="MyStudio")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "set", "target": "player", "prop": "companyName", "value": "MyStudio"}, timeout=30.0
    )
    assert "companyName" in result and "set" in result, result


async def test_get_quality(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "levels: Low, Medium, High\ncurrent: 2"})
    result = await project_settings(action="get", target="quality")
    mock_bridge.send.assert_called_once_with("project_settings", {"action": "get", "target": "quality"}, timeout=30.0)
    assert "levels" in result


async def test_error_from_unity(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Unknown target: invalid"})
    with pytest.raises(ToolError, match="Unknown target"):
        await project_settings(action="get", target="invalid")


# ---------------------------------------------------------------------------
# Pipeline gap extensions: graphics / audio / input / build_target
# ---------------------------------------------------------------------------

async def test_get_graphics(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "renderPipeline:none\ncolorSpace:Linear"})
    result = await project_settings(action="get", target="graphics")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "get", "target": "graphics"}, timeout=30.0
    )
    assert "renderPipeline" in result


async def test_get_audio(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "masterVolume:1\nrolloffScale:1"})
    result = await project_settings(action="get", target="audio")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "get", "target": "audio"}, timeout=30.0
    )
    assert "masterVolume" in result


async def test_get_input(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Horizontal\nVertical\nFire1"})
    result = await project_settings(action="get", target="input")
    mock_bridge.send.assert_called_once_with(
        "project_settings", {"action": "get", "target": "input"}, timeout=30.0
    )
    assert "Horizontal" in result


async def test_set_player_scripting_backend_passes_build_target(mock_bridge):
    """build_target is forwarded when setting ScriptingBackend."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await project_settings(action="set", target="player", prop="ScriptingBackend",
                           value="IL2CPP", build_target="iOS")
    args = mock_bridge.send.call_args[0][1]
    assert args["build_target"] == "iOS"
    assert args["prop"] == "ScriptingBackend"


async def test_build_target_omitted_when_none(mock_bridge):
    """build_target is absent from args when not provided (no None keys sent)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await project_settings(action="set", target="player", prop="companyName", value="Acme")
    args = mock_bridge.send.call_args[0][1]
    assert "build_target" not in args
