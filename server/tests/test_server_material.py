import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import material


async def test_material_create(mock_bridge):
    """create forwards path and shader to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material: Assets/M.mat\nshader: Standard"})
    result = await material(action="create", path="Assets/M.mat", shader="Standard")
    mock_bridge.send.assert_called_once_with(
        "material",
        {"action": "create", "path": "Assets/M.mat", "shader": "Standard"},
        timeout=30.0,
    )
    assert "Assets/M.mat" in result


async def test_material_create_default_shader(mock_bridge):
    """create without shader omits shader from args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material: Assets/M.mat\nshader: Standard"})
    await material(action="create", path="Assets/M.mat")
    call_args = mock_bridge.send.call_args[0]
    assert "shader" not in call_args[1]
    assert call_args[1]["action"] == "create"
    assert call_args[1]["path"] == "Assets/M.mat"


async def test_material_get_by_asset(mock_bridge):
    """get by asset path forwards path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material: Assets/M.mat\n_Color: (1,1,1,1)\n_Smoothness: 0.5"})
    result = await material(action="get", path="Assets/M.mat")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "material"
    assert call_args[1] == {"action": "get", "path": "Assets/M.mat"}
    assert "Assets/M.mat" in result


async def test_material_get_by_object(mock_bridge):
    """get by scene object path forwards object_path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material on '/Cube'\n_Color: (1,1,1,1)"})
    result = await material(action="get", object_path="/Cube")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1] == {"action": "get", "object_path": "/Cube"}
    assert "path" not in call_args[1]
    assert "/Cube" in result


async def test_material_set_float(mock_bridge):
    """set float property forwards path, prop, value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "_Smoothness=0.5 on Assets/M.mat"})
    result = await material(action="set", path="Assets/M.mat", prop="_Smoothness", value="0.5")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "set"
    assert call_args[1]["path"] == "Assets/M.mat"
    assert call_args[1]["prop"] == "_Smoothness"
    assert call_args[1]["value"] == "0.5"
    assert "_Smoothness" in result


async def test_material_set_color(mock_bridge):
    """set color by object_path forwards object_path and hex value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "_BaseColor=#FF0000FF on /Cube"})
    result = await material(action="set", object_path="/Cube", prop="_BaseColor", value="#FF0000FF")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["object_path"] == "/Cube"
    assert call_args[1]["prop"] == "_BaseColor"
    assert call_args[1]["value"] == "#FF0000FF"
    assert "path" not in call_args[1]
    assert "_BaseColor" in result


async def test_material_set_texture(mock_bridge):
    """set texture property forwards texture asset path as value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "_BaseMap=Assets/Tex.png on Assets/M.mat"})
    result = await material(action="set", path="Assets/M.mat", prop="_BaseMap", value="Assets/Tex.png")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["prop"] == "_BaseMap"
    assert call_args[1]["value"] == "Assets/Tex.png"
    assert "_BaseMap" in result


async def test_material_copy(mock_bridge):
    """copy forwards source and targets to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Copied material from /Cube to 2 objects"})
    result = await material(action="copy", source="/Cube", targets="/Sphere,/Capsule")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "copy"
    assert call_args[1]["source"] == "/Cube"
    assert call_args[1]["targets"] == "/Sphere,/Capsule"
    assert "Copied" in result


async def test_material_list_properties(mock_bridge):
    """list_properties forwards path and returns property list."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material: Assets/M.mat\nproperties:\n  _Color: Color\n  _Smoothness: Float\n  _BaseMap: Texture"})
    result = await material(action="list_properties", path="Assets/M.mat")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "list_properties"
    assert call_args[1]["path"] == "Assets/M.mat"
    assert "_Color" in result
    assert "_Smoothness" in result


async def test_material_slot_sends_param(mock_bridge):
    """slot param is forwarded to bridge when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: Standard"})
    await material(action="get", object_path="/Cube", slot=1)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["slot"] == 1
    assert call_args[1]["object_path"] == "/Cube"


async def test_material_slot_omitted_when_none(mock_bridge):
    """slot param is excluded from args when None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Shader: Standard"})
    await material(action="get", object_path="/Cube")
    call_args = mock_bridge.send.call_args[0]
    assert "slot" not in call_args[1]


async def test_material_list_slots(mock_bridge):
    """list_slots action forwards object_path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[0] Mat0 (Standard)\n[1] Mat1 (Standard)"})
    result = await material(action="list_slots", object_path="/Cube")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "list_slots"
    assert call_args[1]["object_path"] == "/Cube"
    assert "[0]" in result


async def test_material_error(mock_bridge):
    """Bridge error raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Material not found"})
    with pytest.raises(ToolError, match="Material not found"):
        await material(action="get", path="Assets/Missing.mat")


# M19: shader errors

async def test_material_get_errors_forwards_path(mock_bridge):
    """get_errors sends action and path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: no errors"})
    result = await material(action="get_errors", path="Assets/Shaders/Custom.shader")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "get_errors"
    assert args["path"] == "Assets/Shaders/Custom.shader"


# M20: list shaders

async def test_material_list_shaders_sends_filter(mock_bridge):
    """list_shaders forwards filter param."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Shaders/A.shader [Custom/A]"})
    result = await material(action="list_shaders", filter="Custom")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "list_shaders"
    assert args["filter"] == "Custom"


async def test_material_list_shaders_no_filter(mock_bridge):
    """list_shaders without filter excludes filter param."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "(none)"})
    await material(action="list_shaders")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "list_shaders"
    assert "filter" not in args


# M22: batch set fields

async def test_material_set_fields_forwards_value(mock_bridge):
    """set_fields forwards value with newline-separated prop=val."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 2 properties set"})
    result = await material(action="set_fields", path="Assets/M.mat",
                            value="_Color=#FF0000\n_Metallic=0.5")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_fields"
    assert args["value"] == "_Color=#FF0000\n_Metallic=0.5"


# Q2b: renderQueue

async def test_material_set_render_queue_forwards(mock_bridge):
    """set with prop=renderQueue forwards prop and value to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await material(action="set", path="Assets/M.mat", prop="renderQueue", value="3000")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["action"] == "set"
    assert call_args[1]["prop"] == "renderQueue"
    assert call_args[1]["value"] == "3000"
