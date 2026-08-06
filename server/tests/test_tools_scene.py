"""P-160: bounds_info contract — physics must be synced before reading bounds.

Python-side tests verify the MCP tool sends the correct command; the C# side
is responsible for calling Physics.SyncTransforms() before reading col.bounds.
"""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools.spatial import spatial_query


async def test_bounds_info_sends_correct_command(mock_bridge):
    """bounds_info action forwards path to spatial_query command."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "center=(10.00,5.00,3.00) size=(1.00,1.00,1.00) min=(9.50,4.50,2.50) max=(10.50,5.50,3.50)"
    })
    result = await spatial_query(action="bounds_info", path="/Cube")
    sent = mock_bridge.send.call_args[0]
    assert sent[0] == "spatial_query"
    assert sent[1]["action"] == "bounds_info"
    assert sent[1]["path"] == "/Cube"


async def test_bounds_info_returns_data_string(mock_bridge):
    """bounds_info result contains center/size/min/max from Unity response."""
    expected = "center=(10.00,5.00,3.00) size=(1.00,1.00,1.00) min=(9.50,4.50,2.50) max=(10.50,5.50,3.50)"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": expected})
    result = await spatial_query(action="bounds_info", path="/Cube")
    assert "center=" in result
    assert "10.00" in result


async def test_bounds_info_error_raises_tool_error(mock_bridge):
    """bounds_info raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "'/Missing' not found"})
    with pytest.raises(ToolError, match="not found"):
        await spatial_query(action="bounds_info", path="/Missing")
