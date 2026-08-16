"""Tests for get_changes tool (Feature 4)."""
import pytest

from unity_mcp.server import get_changes


async def test_get_changes_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "12:00:00 HIERARCHY_CHANGED"}
    result = await get_changes()
    mock_bridge.send.assert_called_once_with(
        "get_changes", {"clear": "true"}, timeout=30.0
    )
    assert result == "12:00:00 HIERARCHY_CHANGED"


async def test_get_changes_clear_false(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "12:00:00 UNDO_REDO"}
    result = await get_changes(clear=False)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["clear"] == "false"
    assert result == "12:00:00 UNDO_REDO"


async def test_get_changes_default_clear_blocked_in_read_only(mock_bridge, monkeypatch):
    from mcp.server.fastmcp.exceptions import ToolError

    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await get_changes()
    mock_bridge.send.assert_not_awaited()


async def test_get_changes_clear_false_allowed_in_read_only(mock_bridge, monkeypatch):
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    mock_bridge.send.return_value = {"ok": True, "data": "NO_CHANGES"}

    assert await get_changes(clear=False) == "NO_CHANGES"
    mock_bridge.send.assert_awaited_once()
