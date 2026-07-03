import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import create_ui, set_rect, set_material


async def test_create_ui_minimal(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Canvas"})
    result = await create_ui(type="Canvas")
    mock_bridge.send.assert_called_once_with("create_ui", {"type": "Canvas"}, timeout=30.0)
    assert result == "Created /Canvas"


async def test_create_ui_all_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Canvas/Btn"})
    await create_ui(type="Button", name="Btn", parent="/Canvas", anchor="center",
                    pos="(0,0)", size="(200,50)", color="#FF0000", text="GO", fontSize="24")
    args = mock_bridge.send.call_args[0][1]
    assert args["type"] == "Button"
    assert args["name"] == "Btn"
    assert args["parent"] == "/Canvas"
    assert args["anchor"] == "center"
    assert args["text"] == "GO"
    assert args["fontSize"] == "24"


async def test_create_ui_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Unknown UI type 'Slider'"})
    with pytest.raises(ToolError, match="Unknown UI type"):
        await create_ui(type="Slider")


async def test_create_ui_none_args_omitted(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Canvas/Panel"})
    await create_ui(type="Panel", name="BG", color="#000000")
    args = mock_bridge.send.call_args[0][1]
    assert "parent" not in args
    assert "anchor" not in args
    assert "pos" not in args


async def test_set_rect_minimal(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "rect:/Canvas/Btn updated"})
    result = await set_rect(path="/Canvas/Btn", anchor="center")
    mock_bridge.send.assert_called_once_with("set_rect", {"path": "/Canvas/Btn", "anchor": "center"}, timeout=30.0)
    assert result == "rect:/Canvas/Btn updated"


async def test_set_rect_all_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "rect:/X updated"})
    await set_rect(path="/X", anchor="top-left", pos="(10,20)", size="(100,50)",
                   pivot="(0,1)", offsetMin="(5,5)", offsetMax="(-5,-5)")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/X"
    assert args["anchor"] == "top-left"
    assert args["offsetMin"] == "(5,5)"
    assert args["offsetMax"] == "(-5,-5)"


async def test_set_rect_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No RectTransform"})
    with pytest.raises(ToolError, match="No RectTransform"):
        await set_rect(path="/Cube")


async def test_set_rect_none_args_omitted(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "rect:/Y updated"})
    await set_rect(path="/Y", pos="(0,0)")
    args = mock_bridge.send.call_args[0][1]
    assert "anchor" not in args
    assert "size" not in args
    assert "offsetMin" not in args


async def test_set_material_success(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material set: /Cube #FF0000"})
    result = await set_material(path="/Cube", color="#FF0000")
    mock_bridge.send.assert_called_once_with(
        "set_material", {"path": "/Cube", "color": "#FF0000"}, timeout=30.0
    )
    assert "#FF0000" in result


async def test_set_material_with_shader(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Material set: /Cube"})
    await set_material(path="/Cube", color="#00FF00", shader="Standard")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Cube"
    assert args["color"] == "#00FF00"
    assert args["shader"] == "Standard"


async def test_set_material_error_raises_tool_error(mock_bridge):
    """set_material raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    with pytest.raises(ToolError, match="Object not found"):
        await set_material(path="/Missing", color="#FF0000")


# ── Fix 23: editor_control.py editor annotation (B2: moved out of scene.py) ──

def test_editor_tool_annotation_is_rw():
    """Fix 23: editor() must use _RW annotation, not _RW_IDEM (it mutates editor state)."""
    import inspect
    import unity_mcp.tools.editor_control as ec_mod
    register_src = inspect.getsource(ec_mod.register)
    found = False
    for line in register_src.splitlines():
        if "editor" in line and "mcp.tool" in line:
            found = True
            assert "_RW_IDEM" not in line, "editor must use _RW not _RW_IDEM"
            break
    assert found, "Could not find editor tool registration in editor_control.register()"
