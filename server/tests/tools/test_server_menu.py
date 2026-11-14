import pytest
from unittest.mock import AsyncMock
from mcp.server.mcpserver.exceptions import ToolError

from unity_mcp.server import menu


async def test_menu_execute_calls_bridge(mock_bridge):
    """menu(action='execute') sends correct args to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Executed: GameObject/3D Object/Cube"})
    result = await menu(action="execute", path="GameObject/3D Object/Cube")
    mock_bridge.send.assert_called_once_with(
        "menu", {"action": "execute", "path": "GameObject/3D Object/Cube"}, timeout=30.0
    )
    assert "Executed" in result


async def test_menu_list_calls_bridge(mock_bridge):
    """menu(action='list') sends correct args to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "GameObject/Create Empty\nGameObject/3D Object"})
    result = await menu(action="list", path="GameObject")
    mock_bridge.send.assert_called_once_with(
        "menu", {"action": "list", "path": "GameObject"}, timeout=30.0
    )
    assert "Create Empty" in result


async def test_menu_list_no_path(mock_bridge):
    """menu(action='list') without path lists all roots."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "File\nEdit\nAssets"})
    result = await menu(action="list")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "list"}
    assert "path" not in args


async def test_menu_execute_error(mock_bridge):
    """menu(action='execute') raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Menu item not found: Invalid/Path"})
    with pytest.raises(ToolError, match="Menu item not found"):
        await menu(action="execute", path="Invalid/Path")


async def test_menu_execute_disabled_item(mock_bridge):
    """menu(action='execute') raises ToolError for disabled item."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Menu item disabled: Assets/Delete"})
    with pytest.raises(ToolError, match="disabled"):
        await menu(action="execute", path="Assets/Delete")


async def test_menu_invalid_action(mock_bridge):
    """menu with invalid action raises ToolError from Unity."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Invalid action 'foo'. Valid: execute, list"})
    with pytest.raises(ToolError, match="Invalid action"):
        await menu(action="foo", path="test")


async def test_menu_none_path_filtered(mock_bridge):
    """None path is omitted from bridge call args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await menu(action="execute", path=None)
    args = mock_bridge.send.call_args[0][1]
    assert "path" not in args
    assert args == {"action": "execute"}


async def test_menu_not_connected(mock_bridge):
    """menu raises ToolError when manager is None."""
    import unity_mcp.server as srv
    old_slot = srv.slot
    srv.slot = None
    try:
        with pytest.raises(ToolError, match="Server not initialized"):
            await menu(action="list")
    finally:
        srv.slot = old_slot
