import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import asset, material


async def test_asset_find_by_type(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Mat.mat"})
    result = await asset(action="find", type="Material")
    mock_bridge.send.assert_called_once_with("asset", {"action": "find", "type": "Material"}, timeout=30.0)
    assert "Assets/Mat.mat" in result


async def test_asset_find_with_folder(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Prefabs/Enemy.prefab"})
    await asset(action="find", type="Prefab", folder="Assets/Prefabs")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "find", "type": "Prefab", "folder": "Assets/Prefabs"}


async def test_asset_find_with_name(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Textures/wood.png"})
    await asset(action="find", type="Texture2D", name="wood")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "find", "type": "Texture2D", "name": "wood"}


async def test_asset_get_info(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "type: Texture2D\nsize: 512x512"})
    result = await asset(action="get_info", path="Assets/Tex.png")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_info", "path": "Assets/Tex.png"}
    assert "type: Texture2D" in result


async def test_asset_create_folder(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/NewDir"})
    await asset(action="create", type="Folder", path="Assets/NewDir")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "create", "type": "Folder", "path": "Assets/NewDir"}


async def test_asset_create_material(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/Mat.mat"})
    await asset(action="create", type="Material", path="Assets/Mat.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "create", "type": "Material", "path": "Assets/Mat.mat"}


async def test_asset_move(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Moved to Assets/B.mat"})
    await asset(action="move", source="Assets/A.mat", dest="Assets/B.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "move", "source": "Assets/A.mat", "dest": "Assets/B.mat"}


async def test_asset_duplicate(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Duplicated to Assets/B.mat"})
    await asset(action="duplicate", source="Assets/A.mat", dest="Assets/B.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "duplicate", "source": "Assets/A.mat", "dest": "Assets/B.mat"}


async def test_asset_delete(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Deleted: Assets/Old.mat"})
    await asset(action="delete", path="Assets/Old.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "delete", "path": "Assets/Old.mat"}


async def test_asset_get_dependencies(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Shader.shader"})
    await asset(action="get_dependencies", path="Assets/X.mat", recursive=True)
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_dependencies", "path": "Assets/X.mat", "recursive": "true"}


async def test_asset_import_settings(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "maxTextureSize=512"})
    await asset(action="import_settings", path="Assets/X.png", prop="maxTextureSize", value="512")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "import_settings", "path": "Assets/X.png", "prop": "maxTextureSize", "value": "512"}


async def test_asset_error_from_unity(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Asset not found: Assets/Missing.mat"})
    with pytest.raises(ToolError, match="Asset not found"):
        await asset(action="get_info", path="Assets/Missing.mat")


async def test_asset_create_error_raises_tool_error(mock_bridge):
    """asset create raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Folder already exists"})
    with pytest.raises(ToolError, match="Folder already exists"):
        await asset(action="create", type="Folder", path="Assets/Existing")


async def test_asset_move_error_raises_tool_error(mock_bridge):
    """asset move raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Source asset not found"})
    with pytest.raises(ToolError, match="Source asset not found"):
        await asset(action="move", source="Assets/Missing.mat", dest="Assets/B.mat")


async def test_asset_delete_error_raises_tool_error(mock_bridge):
    """asset delete raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Cannot delete read-only asset"})
    with pytest.raises(ToolError, match="Cannot delete read-only asset"):
        await asset(action="delete", path="Assets/ReadOnly.mat")


async def test_asset_validate_move(mock_bridge):
    """validate_move passes source and dest to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await asset(action="validate_move", source="Assets/A.prefab", dest="Assets/B.prefab")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "validate_move", "source": "Assets/A.prefab", "dest": "Assets/B.prefab"}


async def test_asset_validate_move_error_from_unity(mock_bridge):
    """validate_move raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "destination already exists"})
    with pytest.raises(ToolError, match="destination already exists"):
        await asset(action="validate_move", source="Assets/A.prefab", dest="Assets/Existing.prefab")


async def test_asset_export_package(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Exported to /tmp/rocks.unitypackage"})
    result = await asset(action="export_package", path="Assets/Rocks", output="/tmp/rocks.unitypackage")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "export_package", "path": "Assets/Rocks", "output": "/tmp/rocks.unitypackage"}
    assert "Exported to" in result


async def test_asset_export_package_no_deps(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Exported to /tmp/x.unitypackage"})
    await asset(action="export_package", path="Assets/Foo", output="/tmp/x.unitypackage", include_deps=False)
    args = mock_bridge.send.call_args[0][1]
    assert args["include_deps"] == "false"


async def test_asset_export_package_default_includes_deps(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Exported to /tmp/x.unitypackage"})
    await asset(action="export_package", path="Assets/Foo", output="/tmp/x.unitypackage")
    args = mock_bridge.send.call_args[0][1]
    assert "include_deps" not in args  # omitted = C# defaults to true


async def test_asset_import_package(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 2 assets\nAssets/Rock.prefab\nAssets/Rock.mat"})
    result = await asset(action="import_package", path="/tmp/rocks.unitypackage")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "import_package", "path": "/tmp/rocks.unitypackage"}
    assert "ok: 2 assets" in result


async def test_asset_import_package_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Package not found: /tmp/missing.unitypackage"})
    with pytest.raises(ToolError, match="Package not found"):
        await asset(action="import_package", path="/tmp/missing.unitypackage")


async def test_asset_export_package_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "export_package requires 'path' and 'output'"})
    with pytest.raises(ToolError, match="requires 'path' and 'output'"):
        await asset(action="export_package", path="Assets/Foo", output=None)


# ---------------------------------------------------------------------------
# Q2 — material set shader
# ---------------------------------------------------------------------------

async def test_material_set_shader_sends_correct_args(mock_bridge):
    """material(action=set, prop=shader, value=...) passes args to bridge unchanged."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await material(action="set", path="Assets/M.mat", prop="shader", value="Standard")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "set", "path": "Assets/M.mat", "prop": "shader", "value": "Standard"}


# ---------------------------------------------------------------------------
# Q5 — asset import_settings read path
# ---------------------------------------------------------------------------

async def test_asset_import_settings_read_no_value(mock_bridge):
    """Omitting value reads the setting — bridge args must not include 'value' key."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "maxTextureSize: 2048"})
    await asset(action="import_settings", path="Assets/T.png", prop="maxTextureSize")
    args = mock_bridge.send.call_args[0][1]
    assert "value" not in args
    assert args == {"action": "import_settings", "path": "Assets/T.png", "prop": "maxTextureSize"}


async def test_asset_import_settings_dump_all(mock_bridge):
    """Omitting both prop and value dumps all settings — bridge args have only action+path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "type: TextureImporter\nmaxTextureSize: 2048"})
    await asset(action="import_settings", path="Assets/T.png")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "import_settings", "path": "Assets/T.png"}


# ---------------------------------------------------------------------------
# Pipeline gap extensions: read_text / write_text / reimport / class_name
# ---------------------------------------------------------------------------

async def test_asset_read_text_sends_action(mock_bridge):
    """read_text action forwards path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:read\npath:Assets/f.txt\nsize:5\ncontent:hello"})
    await asset(action="read_text", path="Assets/f.txt")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "read_text", "path": "Assets/f.txt"}


async def test_asset_write_text_passes_content(mock_bridge):
    """write_text action forwards content to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write\npath:Assets/f.txt\nsize:4"})
    await asset(action="write_text", path="Assets/f.txt", content="data")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "write_text"
    assert args["content"] == "data"


async def test_asset_reimport_sends_action(mock_bridge):
    """reimport action forwards path to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:reimport\npath:Assets/t.png"})
    await asset(action="reimport", path="Assets/t.png")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "reimport", "path": "Assets/t.png"}


async def test_asset_class_name_maps_to_class_key(mock_bridge):
    """class_name Python param maps to 'class' key in bridge args (reserved word workaround)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: Assets/Cfg.asset"})
    await asset(action="create", type="ScriptableObject", path="Assets/Cfg.asset", class_name="GameConfig")
    args = mock_bridge.send.call_args[0][1]
    assert args["class"] == "GameConfig"
    assert "class_name" not in args
