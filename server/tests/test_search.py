"""Tests for search_scene MCP tool."""
import json
import struct
import pytest
from unittest.mock import AsyncMock, Mock, patch
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.bridge import UnityBridge
from unity_mcp.server import search_scene, scene
from unity_mcp.tools.scene import get_hierarchy


def make_response(data=None, ok=True, err=None, msg_id="0001"):
    """Helper to create mock TCP response."""
    resp = {"id": msg_id, "ok": ok}
    if ok:
        resp["data"] = data or ""
    else:
        resp["err"] = err or "error"
    payload = json.dumps(resp).encode("utf-8")
    header = struct.pack("!I", len(payload))
    return header, payload


async def test_search_scene_sends_correct_command(mock_connection):
    mock_reader, mock_writer = mock_connection
    header, payload = make_response("Player #123 [Rigidbody]")
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": "t:Rigidbody"})

    assert result["ok"] is True
    assert "Player" in result["data"]
    assert "[Rigidbody]" in result["data"]

    # Verify sent payload contains search_scene command
    sent = mock_writer.write.call_args[0][0]
    sent_json = json.loads(sent[4:].decode("utf-8"))
    assert sent_json["cmd"] == "search_scene"
    assert sent_json["args"]["query"] == "t:Rigidbody"


async def test_search_scene_no_matches(mock_connection):
    mock_reader, mock_writer = mock_connection
    header, payload = make_response("no matches")
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": "NonExistent"})

    assert result["ok"] is True
    assert result["data"] == "no matches"


async def test_search_scene_error_response(mock_connection):
    mock_reader, mock_writer = mock_connection
    header, payload = make_response(ok=False, err="query is required")
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": ""})

    assert result["ok"] is False
    assert "query is required" in result["err"]


async def test_search_scene_combined_query(mock_connection):
    mock_reader, mock_writer = mock_connection
    header, payload = make_response("SpotLight #456 [Light]\nPointLight #789 [Light]")
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": "t:Light active=true"})

    assert result["ok"] is True
    assert "SpotLight" in result["data"]
    assert "PointLight" in result["data"]

    sent = mock_writer.write.call_args[0][0]
    sent_json = json.loads(sent[4:].decode("utf-8"))
    assert sent_json["args"]["query"] == "t:Light active=true"


async def test_search_scene_multiple_results(mock_connection):
    mock_reader, mock_writer = mock_connection
    data = "Obj1 #100 [BoxCollider]\nObj2 #200 [BoxCollider,Rigidbody]\nObj3 #300 [BoxCollider]"
    header, payload = make_response(data)
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": "t:BoxCollider"})

    assert result["ok"] is True
    lines = result["data"].strip().split("\n")
    assert len(lines) == 3


async def test_search_scene_inactive_objects(mock_connection):
    mock_reader, mock_writer = mock_connection
    header, payload = make_response("HiddenObj #555 ! ")
    mock_reader.readexactly = AsyncMock(side_effect=[header, payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()
        result = await bridge.send("search_scene", {"query": "active=false"})

    assert result["ok"] is True
    assert "!" in result["data"]


async def test_search_scene_tool_calls_bridge(mock_bridge):
    """Test search_scene MCP tool calls bridge with correct args"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Player #123 [Rigidbody]"})
    result = await search_scene(query="t:Rigidbody")
    mock_bridge.send.assert_called_once_with("search_scene", {"query": "t:Rigidbody"}, timeout=15.0)
    assert "Player" in result


async def test_search_scene_tool_error(mock_bridge):
    """Test search_scene MCP tool raises ToolError"""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "query is required"})
    with pytest.raises(ToolError, match="query is required"):
        await search_scene(query="")


# ─── scene error paths ─────────────────────────────────────────────────────────

async def test_scene_open_error_raises_tool_error(mock_bridge):
    """scene open raises ToolError when Unity returns ok=False."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene file not found"})
    with pytest.raises(ToolError, match="Scene file not found"):
        await scene(action="open", path="Assets/Missing.unity")


async def test_scene_save_error_raises_tool_error(mock_bridge):
    """scene save raises ToolError when Unity returns ok=False."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Save failed: read-only"})
    with pytest.raises(ToolError, match="Save failed"):
        await scene(action="save", path="Assets/Scene.unity")


async def test_get_hierarchy_error_raises_tool_error(mock_bridge):
    """get_hierarchy raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene not loaded"})
    with pytest.raises(ToolError, match="Scene not loaded"):
        await get_hierarchy()
