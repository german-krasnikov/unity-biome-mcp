import pytest
from unittest.mock import AsyncMock, Mock
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


async def test_validate_move_path_only_true_sends_path_only(mock_bridge):
    """path_only=True forwards path_only='true' to bridge (P-199 dry-run mode)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: path syntax valid"})
    await asset(action="validate_move", source="Assets/A.prefab",
                dest="Assets/NonExistentFolder/A.prefab", path_only=True)
    args = mock_bridge.send.call_args[0][1]
    assert args.get("path_only") == "true"
    assert args["action"] == "validate_move"


async def test_validate_move_path_only_false_omits_key(mock_bridge):
    """path_only=False (default) does not include path_only key in bridge args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await asset(action="validate_move", source="Assets/A.prefab", dest="Assets/B.prefab")
    args = mock_bridge.send.call_args[0][1]
    assert "path_only" not in args


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


# ---------------------------------------------------------------------------
# P0-30: freeze asset.write_text changeset-capture baseline (OFF, pre-SourcePatch).
# Direct asset(action="write_text") goes through _write_text_with_capture(),
# which is the ONLY Python entry point that records a changeset op. The
# `batch` tool (server/tests/test_source_patch_boundary.py) sends raw command
# text straight to Unity and never calls this capture path — that asymmetry
# is the real current boundary between the two .cs effect entry points and is
# intentionally left unmasked here (see Plans/HotReload P0-30).
# ---------------------------------------------------------------------------

async def test_asset_write_text_without_changeset_wiring_still_forwards(mock_bridge, monkeypatch):
    """Default unit-test state: coordinator/store singletons are unwired (None).
    write_text must still forward to Unity unchanged and must not raise."""
    monkeypatch.setattr("unity_mcp.changeset_coordinator.get_coordinator", lambda: None)
    monkeypatch.setattr("unity_mcp.changeset_store.get_store", lambda: None)
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write\npath:Assets/f.cs\nsize:4"})
    await asset(action="write_text", path="Assets/f.cs", content="data")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "write_text", "path": "Assets/f.cs", "content": "data"}


async def test_asset_write_text_appends_changeset_op_when_content_changes(mock_bridge, monkeypatch, tmp_path):
    """Direct write_text records exactly one append_file_op when the on-disk
    bytes actually differ before vs. after send() — the real Unity write case."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.changeset_store import ContentStore

    target = tmp_path / "Script.cs"
    target.write_text("old content", encoding="utf-8")
    store = ContentStore(str(tmp_path / "blobs"))
    coordinator = Mock()

    monkeypatch.setattr("unity_mcp.changeset_coordinator.get_coordinator", lambda: coordinator)
    monkeypatch.setattr("unity_mcp.changeset_store.get_store", lambda: store)

    async def _fake_send(cmd, args, **kwargs):
        # Mirrors what the real Unity write_text handler does to disk.
        target.write_text(args["content"], encoding="utf-8")
        return {"ok": True, "data": "ok:write"}

    mock_bridge.send = AsyncMock(side_effect=_fake_send)

    await asset(action="write_text", path=str(target), content="new content")

    coordinator.append_file_op.assert_called_once_with(
        "asset.write_text", str(target), ContentRef.of("old content"), ContentRef.of("new content"))


async def test_asset_write_text_no_changeset_op_when_content_unchanged(mock_bridge, monkeypatch, tmp_path):
    """Direct write_text records NOTHING when before/after on-disk bytes are
    identical — e.g. a mocked bridge that never actually touches disk, or a
    real write that happens to round-trip the same content."""
    from unity_mcp.changeset_store import ContentStore

    target = tmp_path / "Script.cs"
    target.write_text("same content", encoding="utf-8")
    store = ContentStore(str(tmp_path / "blobs"))
    coordinator = Mock()

    monkeypatch.setattr("unity_mcp.changeset_coordinator.get_coordinator", lambda: coordinator)
    monkeypatch.setattr("unity_mcp.changeset_store.get_store", lambda: store)

    # _send does NOT touch disk here — mirrors every other unit test in this file.
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write"})

    await asset(action="write_text", path=str(target), content="same content")

    coordinator.append_file_op.assert_not_called()


# ---------------------------------------------------------------------------
# P0-50: route a `.cs` write through source_patch_write when Source Patch
# mutation is armed, otherwise stay on the legacy `asset` route. Mutation is
# unconditionally OFF today — _source_patch_mutation_is_on() always returns
# False, since no editor(mutation_mode) command or coordinator wiring exists
# yet (P0-70). These tests force the seam True to prove the routing/capture
# logic itself ahead of that wiring. See §3.2/§6 P0-50 in
# Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md.
# ---------------------------------------------------------------------------

async def test_asset_write_text_cs_routes_to_source_patch_when_mutation_armed(mock_bridge, monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.asset._source_patch_mutation_is_on", lambda: True)
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write"})

    await asset(action="write_text", path="Assets/f.cs", content="data")

    cmd, args = mock_bridge.send.call_args[0][:2]
    assert cmd == "source_patch_write"
    assert args == {"path": "Assets/f.cs", "content": "data"}


async def test_asset_write_text_cs_stays_legacy_when_mutation_off(mock_bridge, monkeypatch):
    monkeypatch.setattr("unity_mcp.tools.asset._source_patch_mutation_is_on", lambda: False)
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write"})

    await asset(action="write_text", path="Assets/f.cs", content="data")

    cmd, args = mock_bridge.send.call_args[0][:2]
    assert cmd == "asset"
    assert args == {"action": "write_text", "path": "Assets/f.cs", "content": "data"}


async def test_asset_write_text_non_cs_stays_legacy_even_when_mutation_armed(mock_bridge, monkeypatch):
    """§6 P0-50 requirement 4: non-.cs is never rerouted, regardless of state."""
    monkeypatch.setattr("unity_mcp.tools.asset._source_patch_mutation_is_on", lambda: True)
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write"})

    await asset(action="write_text", path="Assets/f.txt", content="data")

    cmd, _args = mock_bridge.send.call_args[0][:2]
    assert cmd == "asset"


async def test_asset_write_text_cs_sends_exactly_once_regardless_of_route(mock_bridge, monkeypatch):
    """§3.2: never probes by writing first — exactly one send either way."""
    monkeypatch.setattr("unity_mcp.tools.asset._source_patch_mutation_is_on", lambda: True)
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:write"})

    await asset(action="write_text", path="Assets/f.cs", content="data")

    assert mock_bridge.send.call_count == 1
