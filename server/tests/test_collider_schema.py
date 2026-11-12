import pytest
from unittest.mock import AsyncMock
from mcp.server.mcpserver.exceptions import ToolError

from unity_mcp.server import check_colliders, get_schema


async def test_check_colliders_sends_command(mock_bridge, bridge_response):
    bridge_response(data="OK: no collider issues")
    result = await check_colliders()
    mock_bridge.send.assert_called_once_with("check_colliders", {}, timeout=30.0)
    assert "OK" in result


async def test_check_colliders_with_path(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "TRIGGER_NO_RB: /Player/BoxCollider"})
    result = await check_colliders(path="/Player")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Player"
    assert "TRIGGER_NO_RB" in result


async def test_check_colliders_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "scene not loaded"})
    with pytest.raises(ToolError, match="scene not loaded"):
        await check_colliders()


async def test_get_schema_sends_type(mock_bridge, bridge_response):
    bridge_response(data="Schema: Rigidbody\n  m_Mass: Float")
    result = await get_schema(type="Rigidbody")
    mock_bridge.send.assert_called_once_with("get_schema", {"type": "Rigidbody"}, timeout=30.0)
    assert "Schema" in result


async def test_get_schema_unknown_type(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Type not found: Ghost"})
    with pytest.raises(ToolError, match="Type not found"):
        await get_schema(type="Ghost")
