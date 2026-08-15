import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import validate_triggers, get_spatial_context


async def test_validate_triggers_sends_correct_command(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Layout: 3 triggers, 5 solids\nOK: no trigger overlaps"})
    result = await validate_triggers(root="/Arena", min_distance=2.0)
    mock_bridge.send.assert_called_once_with(
        "validate_triggers", {"root": "/Arena", "min_distance": "2.0"}, timeout=30.0
    )
    assert "no trigger overlaps" in result


async def test_validate_triggers_default_min_distance(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await validate_triggers(root="/Root")
    args = mock_bridge.send.call_args[0][1]
    assert args["root"] == "/Root"
    assert args["min_distance"] == "3.0"


async def test_validate_triggers_default_root(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK"})
    await validate_triggers()
    args = mock_bridge.send.call_args[0][1]
    assert args["root"] == "/"


async def test_validate_triggers_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "'/Arena' not found"})
    with pytest.raises(ToolError, match="not found"):
        await validate_triggers(root="/Arena")


async def test_get_spatial_context_sends_correct_command(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Position: (0.0,0.0,0.0)\nApproach vectors: N/A"})
    result = await get_spatial_context(path="/Player", radius=10.0)
    mock_bridge.send.assert_called_once_with(
        "get_spatial_context", {"path": "/Player", "radius": "10.0"}, timeout=30.0
    )
    assert "Position" in result


async def test_get_spatial_context_default_radius(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Position: (1.0,0.0,2.0)"})
    await get_spatial_context(path="/Enemy")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Enemy"
    assert args["radius"] == "5.0"


async def test_get_spatial_context_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "'/Ghost' not found"})
    with pytest.raises(ToolError, match="not found"):
        await get_spatial_context(path="/Ghost")
