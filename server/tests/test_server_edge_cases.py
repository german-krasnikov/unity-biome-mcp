"""Edge cases tests for all 18 MCP tools."""
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import (
    get_hierarchy, get_component, set_property, create_object, find_objects,
    manage_component, get_console, screenshot, scene, animation, timeline,
    _send, resolve_tool_schema,
)


# --- get_hierarchy edge cases ---
async def test_get_hierarchy_default_params(mock_bridge):
    """get_hierarchy uses depth=2, root=None by default."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Scene"})
    await get_hierarchy()
    mock_bridge.send.assert_called_once_with("get_hierarchy", {"depth": 2}, timeout=15.0)


async def test_get_hierarchy_filter_passed(mock_bridge):
    """filter parameter IS passed to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Scene"})
    await get_hierarchy(depth=5, root="/Root", filter="active")
    mock_bridge.send.assert_called_once_with("get_hierarchy", {"depth": 5, "root": "/Root", "filter": "active"}, timeout=15.0)


# --- get_component edge cases ---
async def test_get_component_empty_path(mock_bridge):
    """get_component accepts empty path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "root"})
    await get_component(path="", type="Transform")
    mock_bridge.send.assert_called_once_with("get_component", {"path": "", "type": "Transform"}, timeout=30.0)


async def test_get_component_special_chars_in_path(mock_bridge):
    """get_component handles unicode and special chars."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "pos: 0,0,0"})
    await get_component(path="/Объект (1)/Дочерний", type="Transform")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Объект (1)/Дочерний"


# --- set_property edge cases ---
async def test_set_property_value_types(mock_bridge):
    """set_property passes value as string (all types)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await set_property(path="/Obj", component="Transform", prop="position", value="(1,2,3)")
    args = mock_bridge.send.call_args[0][1]
    assert args["value"] == "(1,2,3)"


async def test_set_property_raises_on_error(mock_bridge):
    """set_property raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Invalid property"})
    with pytest.raises(ToolError, match="Invalid property"):
        await set_property(path="/Obj", component="Transform", prop="bad", value="x")


# --- create_object edge cases ---
async def test_create_object_minimal(mock_bridge):
    """create_object with only name (no parent/components)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await create_object(name="Empty")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"name": "Empty"}


async def test_create_object_with_parent(mock_bridge):
    """create_object includes parent when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await create_object(name="Child", parent="/Scene")
    args = mock_bridge.send.call_args[0][1]
    assert args["parent"] == "/Scene"


async def test_create_object_with_components_string(mock_bridge):
    """create_object passes components as comma-separated string."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await create_object(name="Rigidbody", components="BoxCollider,Rigidbody")
    args = mock_bridge.send.call_args[0][1]
    assert args["components"] == "BoxCollider,Rigidbody"


async def test_create_object_raises_on_error(mock_bridge):
    """create_object raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Parent not found"})
    with pytest.raises(ToolError, match="Parent not found"):
        await create_object(name="Child", parent="/Invalid")


# --- find_objects edge cases ---
async def test_find_objects_no_filters(mock_bridge):
    """find_objects with all None params sends empty args dict."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "All objects"})
    await find_objects()
    args = mock_bridge.send.call_args[0][1]
    assert args == {}


async def test_find_objects_all_filters(mock_bridge):
    """find_objects with all filters provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Results"})
    await find_objects(name="Player", tag="Enemy", layer="Default", component="Rigidbody")
    args = mock_bridge.send.call_args[0][1]
    assert len(args) == 4
    assert args["name"] == "Player"
    assert args["tag"] == "Enemy"
    assert args["layer"] == "Default"
    assert args["component"] == "Rigidbody"


async def test_find_objects_single_filter_name(mock_bridge):
    """find_objects with only name filter."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Found"})
    await find_objects(name="Cube")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"name": "Cube"}


async def test_find_objects_single_filter_tag(mock_bridge):
    """find_objects with only tag filter."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Found"})
    await find_objects(tag="Player")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"tag": "Player"}


async def test_find_objects_single_filter_layer(mock_bridge):
    """find_objects with only layer filter."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Found"})
    await find_objects(layer="UI")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"layer": "UI"}


async def test_find_objects_single_filter_component(mock_bridge):
    """find_objects with only component filter."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Found"})
    await find_objects(component="Camera")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"component": "Camera"}


# --- manage_component edge cases ---
async def test_manage_component_add(mock_bridge):
    """manage_component with action=add."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Added"})
    await manage_component(path="/Obj", type="BoxCollider", action="add")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add"


async def test_manage_component_remove(mock_bridge):
    """manage_component with action=remove."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Removed"})
    await manage_component(path="/Obj", type="BoxCollider", action="remove")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "remove"


async def test_manage_component_raises_on_error(mock_bridge):
    """manage_component raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Component not found"})
    with pytest.raises(ToolError, match="Component not found"):
        await manage_component(path="/Obj", type="Bad", action="remove")


# --- get_console edge cases ---
async def test_get_console_defaults(mock_bridge):
    """get_console defaults: count=10, level=None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Logs"})
    await get_console()
    args = mock_bridge.send.call_args[0][1]
    assert args == {"count": 10}


async def test_get_console_with_level(mock_bridge):
    """get_console includes level when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Errors"})
    await get_console(count=5, level="Error")
    args = mock_bridge.send.call_args[0][1]
    assert args["count"] == 5
    assert args["level"] == "Error"


async def test_get_console_raises_on_error(mock_bridge):
    """get_console raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Console capture failed"})
    with pytest.raises(ToolError, match="Console capture failed"):
        await get_console()


async def test_get_console_passes_first_param(mock_bridge):
    """get_console passes first param when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Logs"})
    await get_console(count=20, first=10)
    args = mock_bridge.send.call_args[0][1]
    assert args["first"] == 10
    assert args["count"] == 20


async def test_get_console_default_first_zero(mock_bridge):
    """get_console omits first from args when default (0)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Logs"})
    await get_console()
    args = mock_bridge.send.call_args[0][1]
    assert "first" not in args


# --- screenshot edge cases ---
async def test_screenshot_defaults(mock_bridge):
    """screenshot defaults: 640x480, no camera."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "base64..."})
    await screenshot()
    args = mock_bridge.send.call_args[0][1]
    assert args == {"width": 640, "height": 480}


async def test_screenshot_with_camera(mock_bridge):
    """screenshot includes camera when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "base64..."})
    await screenshot(width=1920, height=1080, camera="/CamObj")
    args = mock_bridge.send.call_args[0][1]
    assert args["camera"] == "/CamObj"


async def test_screenshot_custom_size(mock_bridge):
    """screenshot with custom dimensions."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "base64..."})
    await screenshot(width=1920, height=1080)
    args = mock_bridge.send.call_args[0][1]
    assert args["width"] == 1920
    assert args["height"] == 1080


# --- scene tools edge cases ---
async def test_scene_save_without_path(mock_bridge):
    """scene save with no path sends only action."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Saved"})
    await scene(action="save")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "save"}


async def test_scene_new_raises_on_error(mock_bridge):
    """scene new raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene creation failed"})
    with pytest.raises(ToolError, match="Scene creation failed"):
        await scene(action="new")


async def test_scene_discard_raises_on_error(mock_bridge):
    """scene discard raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No changes to discard"})
    with pytest.raises(ToolError, match="No changes to discard"):
        await scene(action="discard")


# --- animation edge cases ---
async def test_animation_create_no_property_no_keys(mock_bridge):
    """animation create with no property/keys sends only required fields."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await animation(action="create", path="/Obj", clip_name="Walk")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "create"
    assert args["clip_name"] == "Walk"
    assert "property" not in args
    assert "keys" not in args


async def test_animation_create_with_property_and_keys(mock_bridge):
    """animation create with property and keys passes them."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await animation(action="create", path="/Obj", clip_name="Idle", property="localRotation", keys="")
    args = mock_bridge.send.call_args[0][1]
    assert args["property"] == "localRotation"
    assert args["keys"] == ""


async def test_animation_edit_minimal(mock_bridge):
    """animation edit with only path, clip (no property/keys)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Edited"})
    await animation(action="edit", path="/Obj", clip="Walk")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "edit", "path": "/Obj", "clip": "Walk"}


async def test_animation_get_with_clip_and_time(mock_bridge):
    """animation get includes clip and time when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Clip data"})
    await animation(action="get", path="/Obj", clip="Walk", time=1.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["clip"] == "Walk"
    assert args["time"] == 1.5


# --- timeline edge cases ---
async def test_timeline_edit_all_optional_params(mock_bridge):
    """timeline edit with all optional params."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Edited"})
    await timeline(
        action="edit",
        path="/TL",
        track="Track1",
        track_type="Animation",
        clip="Clip1",
        binding="/Obj",
        start=1.0,
        duration=2.0,
        blend_in=0.5,
        blend_out=0.5
    )
    args = mock_bridge.send.call_args[0][1]
    assert args["blend_in"] == 0.5
    assert args["blend_out"] == 0.5


async def test_timeline_edit_minimal(mock_bridge):
    """timeline edit with only path + action."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Edited"})
    await timeline(action="edit", path="/TL")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "edit", "path": "/TL"}


# --- animation sub-action passthrough ---
async def test_animation_add_key_passes_action(mock_bridge):
    """animation action=add_key passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Clip | add_key"})
    result = await animation(action="add_key", path="/Obj", clip="Clip", property="localScale", keys="t:0 v:(1,1,1)")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_key"
    assert args["clip"] == "Clip"
    assert args["property"] == "localScale"
    assert "edited" in result


async def test_animation_remove_curve_passes_action(mock_bridge):
    """animation action=remove_curve passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Clip | remove_curve"})
    await animation(action="remove_curve", path="/Obj", clip="Clip", property="localPosition")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "remove_curve"
    assert args["property"] == "localPosition"


async def test_animation_set_loop_passes_action(mock_bridge):
    """animation action=set_loop passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Clip | set_loop"})
    await animation(action="set_loop", path="/Obj", clip="Clip", keys="true")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_loop"
    assert args["keys"] == "true"


# --- timeline sub-action passthrough ---
async def test_timeline_add_track_passes_action(mock_bridge):
    """timeline action=add_track passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: add_track [Audio] SFX"})
    result = await timeline(action="add_track", path="/Dir", track="SFX", track_type="Audio")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_track"
    assert args["track"] == "SFX"
    assert args["track_type"] == "Audio"
    assert "add_track" in result


async def test_timeline_set_binding_passes_action(mock_bridge):
    """timeline action=set_binding passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: set_binding"})
    await timeline(action="set_binding", path="/Dir", track="Track1", binding="/Actor")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_binding"
    assert args["binding"] == "/Actor"


async def test_timeline_mute_passes_action(mock_bridge):
    """timeline action=mute passes through to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: mute"})
    await timeline(action="mute", path="/Dir", track="Music")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "mute"
    assert args["track"] == "Music"


async def test_timeline_create_with_all_params(mock_bridge):
    """timeline create with asset_path + director_path + tracks."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created"})
    await timeline(
        action="create",
        path="/GO",
        asset_path="Assets/Timeline.playable",
        director_path="/Director",
        tracks="Animation:Char;Audio:Music"
    )
    args = mock_bridge.send.call_args[0][1]
    assert args["asset_path"] == "Assets/Timeline.playable"
    assert args["director_path"] == "/Director"
    assert args["tracks"] == "Animation:Char;Audio:Music"


# --- all tools connection error ---
async def test_get_component_connection_error(mock_bridge):
    """get_component raises ConnectionError on bridge failure."""
    mock_bridge.send = AsyncMock(side_effect=ConnectionError("Unity disconnected"))
    with pytest.raises((ConnectionError, ToolError)):
        await get_component(path="/Obj", type="Transform")


async def test_create_object_connection_error(mock_bridge):
    """create_object raises ConnectionError on bridge failure."""
    mock_bridge.send = AsyncMock(side_effect=ConnectionError("Unity disconnected"))
    with pytest.raises((ConnectionError, ToolError)):
        await create_object(name="Obj")


async def test_screenshot_connection_error(mock_bridge):
    """screenshot raises ConnectionError on bridge failure."""
    mock_bridge.send = AsyncMock(side_effect=ConnectionError("Unity disconnected"))
    with pytest.raises((ConnectionError, ToolError)):
        await screenshot()


# --- _send_raw ConnectionError wrapping ---
async def test_send_raw_wraps_connection_error_as_tool_error(mock_bridge):
    """_send_raw catches ConnectionError and raises ToolError."""
    mock_bridge.send = AsyncMock(side_effect=ConnectionError("dead"))
    with pytest.raises(ToolError):
        await _send("ping", {})


# --- C4: manager=None guard ---
async def test_send_bridge_none_raises_tool_error():
    """_send raises ToolError when slot is None (lifespan not started)."""
    import unity_mcp.server as srv
    original = srv.slot
    try:
        srv.slot = None
        with pytest.raises(ToolError, match="Server not initialized"):
            await get_hierarchy()
    finally:
        srv.slot = original


# --- P2 gaps ---

async def test_send_raw_wraps_generic_exception_as_tool_error(mock_bridge):
    """_send_raw catches generic Exception (not ConnectionError) and raises ToolError."""
    mock_bridge.send = AsyncMock(side_effect=RuntimeError("unexpected crash"))
    with pytest.raises(ToolError, match="Unexpected error"):
        await _send("ping", {})


async def test_resolve_tool_schema_empty_input():
    """resolve_tool_schema with empty string returns 'No schema found' message."""
    result = await resolve_tool_schema("")
    assert "No schema found" in result


async def test_resolve_tool_schema_unknown_tool():
    """resolve_tool_schema with unknown tool name returns 'No schema found' message."""
    result = await resolve_tool_schema("nonexistent_tool_xyz")
    assert "No schema found" in result


async def test_send_raw_bridge_none_raises_tool_error(mock_bridge):
    """_send_raw raises ToolError with 'No Unity connection' when slot.bridge is None."""
    import unity_mcp.server as srv
    from unittest.mock import Mock
    mock_slot = Mock()
    mock_slot.bridge = None
    original = srv.slot
    try:
        srv.slot = mock_slot
        with pytest.raises(ToolError, match="No Unity connection"):
            await _send("ping", {})
    finally:
        srv.slot = original


# --- get_console additional edge cases ---

async def test_get_console_empty_response_is_valid(mock_bridge):
    """get_console with empty data string is valid (not ToolError)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": ""})
    result = await get_console()
    assert result == ""


async def test_get_console_warning_level(mock_bridge):
    """get_console passes level=Warning to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "warn"})
    await get_console(level="Warning")
    args = mock_bridge.send.call_args[0][1]
    assert args["level"] == "Warning"


async def test_get_console_missing_data_key(mock_bridge):
    """get_console returns empty string when data key absent."""
    mock_bridge.send = AsyncMock(return_value={"ok": True})
    result = await get_console()
    assert result == ""


async def test_get_console_count_minus_one(mock_bridge):
    """get_console passes count=-1 to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "all logs"})
    await get_console(count=-1)
    args = mock_bridge.send.call_args[0][1]
    assert args["count"] == -1
