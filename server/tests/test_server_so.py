import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import scriptable_object


async def test_so_create(mock_bridge):
    """create action forwards type and path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/Data/Config.asset"})
    result = await scriptable_object(action="create", type="GameConfig", path="Assets/Data/Config.asset")
    mock_bridge.send.assert_called_once_with(
        "scriptable_object",
        {"action": "create", "type": "GameConfig", "path": "Assets/Data/Config.asset"},
        timeout=30.0,
    )
    assert "Assets/Data/Config.asset" in result


async def test_so_get(mock_bridge):
    """get action forwards path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "configName: default\nmaxHealth: 100"})
    result = await scriptable_object(action="get", path="Assets/Data/Config.asset")
    mock_bridge.send.assert_called_once_with(
        "scriptable_object",
        {"action": "get", "path": "Assets/Data/Config.asset"},
        timeout=30.0,
    )
    assert "maxHealth" in result


async def test_so_set(mock_bridge):
    """set action forwards path, prop and value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    result = await scriptable_object(action="set", path="Assets/Data/Config.asset", prop="maxHealth", value="100")
    mock_bridge.send.assert_called_once_with(
        "scriptable_object",
        {"action": "set", "path": "Assets/Data/Config.asset", "prop": "maxHealth", "value": "100"},
        timeout=30.0,
    )
    assert result == "ok"


async def test_so_list_types(mock_bridge):
    """list_types with filter forwards filter param."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "GameConfig\nGameSettings"})
    result = await scriptable_object(action="list_types", filter="Game")
    mock_bridge.send.assert_called_once_with(
        "scriptable_object",
        {"action": "list_types", "filter": "Game"},
        timeout=30.0,
    )
    assert "GameConfig" in result


async def test_so_list_types_no_filter(mock_bridge):
    """list_types without filter omits filter key."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "SomeType\nAnotherType"})
    await scriptable_object(action="list_types")
    call_args = mock_bridge.send.call_args[0][1]
    assert "filter" not in call_args
    assert call_args["action"] == "list_types"


async def test_so_find(mock_bridge):
    """find action forwards type."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Data/Config.asset\nAssets/Data/Config2.asset"})
    result = await scriptable_object(action="find", type="GameConfig")
    mock_bridge.send.assert_called_once_with(
        "scriptable_object",
        {"action": "find", "type": "GameConfig"},
        timeout=30.0,
    )
    assert "Assets/Data/Config.asset" in result


async def test_so_missing_path(mock_bridge):
    """Error response from bridge raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "path is required"})
    with pytest.raises(ToolError, match="path is required"):
        await scriptable_object(action="get")


async def test_so_error(mock_bridge):
    """Generic bridge error raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Asset not found: Assets/Missing.asset"})
    with pytest.raises(ToolError, match="Asset not found"):
        await scriptable_object(action="get", path="Assets/Missing.asset")


async def test_so_set_fields_forwarded(mock_bridge):
    """fields param is forwarded; no prop/value keys in payload."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await scriptable_object(action="set", path="Assets/X.asset", fields="speed=10\nhealth=100")
    payload = mock_bridge.send.call_args[0][1]
    assert payload == {"action": "set", "path": "Assets/X.asset", "fields": "speed=10\nhealth=100"}


async def test_so_set_fields_none_not_sent(mock_bridge):
    """When fields is None (default), it is not included in the payload."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await scriptable_object(action="set", path="Assets/X.asset", prop="speed", value="10")
    payload = mock_bridge.send.call_args[0][1]
    assert "fields" not in payload
