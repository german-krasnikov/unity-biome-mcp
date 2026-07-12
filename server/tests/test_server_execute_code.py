"""Tests for execute_code tool."""
from unittest.mock import AsyncMock
from unity_mcp.server import execute_code


async def test_execute_code_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "2"}
    result = await execute_code("return (1+1).ToString();")
    call = mock_bridge.send.call_args
    assert call[0][0] == "execute_code"
    assert call[0][1] == {"code": "return (1+1).ToString();", "undo_label": "execute_code"}
    assert result == "2"


async def test_execute_code_with_undo_label(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "done"}
    await execute_code("var go = new GameObject();", undo_label="create_test_object")
    call = mock_bridge.send.call_args
    assert call[0][1]["undo_label"] == "create_test_object"
