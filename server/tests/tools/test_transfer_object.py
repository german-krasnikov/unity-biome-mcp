"""TDD tests for transfer_object tool and create_object scene param."""
from unittest.mock import AsyncMock


async def test_transfer_move_sends_correct_args(mock_bridge):
    """move action sends path, action, target_scene to Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "Moved /Enemy to AdditiveScene"}
    from unity_mcp.tools.objects import transfer_object
    result = await transfer_object(path="/Enemy", action="move", target_scene="AdditiveScene")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["path"] == "/Enemy"
    assert sent["action"] == "move"
    assert sent["target_scene"] == "AdditiveScene"
    assert "Moved" in result


async def test_transfer_copy_sends_correct_args(mock_bridge):
    """copy action sends path, action, target_scene to Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "Copied /Player to Level2"}
    from unity_mcp.tools.objects import transfer_object
    result = await transfer_object(path="/Player", action="copy", target_scene="Level2")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["path"] == "/Player"
    assert sent["action"] == "copy"
    assert sent["target_scene"] == "Level2"


async def test_transfer_with_parent_sends_parent(mock_bridge):
    """parent param is forwarded when set."""
    mock_bridge.send.return_value = {"ok": True, "data": "Moved /Obj to Scene2"}
    from unity_mcp.tools.objects import transfer_object
    await transfer_object(path="/Obj", action="move", target_scene="Scene2", parent="/Container")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["parent"] == "/Container"


async def test_transfer_world_position_stays_false(mock_bridge):
    """world_position_stays=False is forwarded as 'false' string."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.objects import transfer_object
    await transfer_object(path="/Obj", action="move", target_scene="S", world_position_stays=False)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["world_position_stays"] == "false"


async def test_transfer_world_position_stays_default_omitted(mock_bridge):
    """world_position_stays=True (default) is omitted from args (no-op default)."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.objects import transfer_object
    await transfer_object(path="/Obj", action="move", target_scene="S")
    sent = mock_bridge.send.call_args[0][1]
    # True is default in C# — no need to send
    assert "world_position_stays" not in sent


async def test_transfer_no_target_scene_omitted(mock_bridge):
    """target_scene=None is omitted (same-scene duplicate)."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.objects import transfer_object
    await transfer_object(path="/Obj", action="copy")
    sent = mock_bridge.send.call_args[0][1]
    assert "target_scene" not in sent


async def test_create_object_scene_forwarded(mock_bridge):
    """scene param is forwarded to create_object command."""
    mock_bridge.send.return_value = {"ok": True, "data": "Created /Enemy"}
    from unity_mcp.tools.objects import create_object
    await create_object(name="Enemy", scene="AdditiveScene")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["scene"] == "AdditiveScene"
    assert sent["name"] == "Enemy"


async def test_create_object_no_scene_omitted(mock_bridge):
    """Regression: scene=None must NOT appear in args (single-scene unchanged behavior)."""
    mock_bridge.send.return_value = {"ok": True, "data": "Created /Obj"}
    from unity_mcp.tools.objects import create_object
    await create_object(name="Obj")
    sent = mock_bridge.send.call_args[0][1]
    assert "scene" not in sent
    assert sent["name"] == "Obj"
