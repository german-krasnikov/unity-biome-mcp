"""TDD tests for spatial_query raycast and spatial_map actions."""
import pytest
from unittest.mock import AsyncMock
from mcp.server.mcpserver.exceptions import ToolError
from unity_mcp.tools.spatial import spatial_query, scan_scene


async def test_spatial_raycast_sends_command(mock_bridge):
    """raycast action sends path + target to spatial_query."""
    mock_bridge.send.return_value = {"ok": True, "data": "CLEAR (no hits)"}
    result = await spatial_query(action="raycast", path="/Player", target="/Wall")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "raycast"
    assert sent["path"] == "/Player"
    assert sent["target"] == "/Wall"


async def test_spatial_map_sends_command(mock_bridge):
    """spatial_map action sends cell_size param."""
    mock_bridge.send.return_value = {"ok": True, "data": "# Map: XZ, cell=2m, 10x10"}
    result = await spatial_query(action="spatial_map", path="/", cell_size=2.0)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "spatial_map"
    assert sent["cell_size"] == "2.0"


async def test_spatial_raycast_with_layer_mask(mock_bridge):
    """raycast action passes layer_mask param."""
    mock_bridge.send.return_value = {"ok": True, "data": "BLOCKED: 1 hit"}
    result = await spatial_query(action="raycast", path="/Gun", target="/Target", layer_mask="Default")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["layer_mask"] == "Default"
    assert "BLOCKED" in result


# ─── error paths ──────────────────────────────────────────────────────────────

async def test_spatial_query_error_raises_tool_error(mock_bridge):
    """spatial_query raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    with pytest.raises(ToolError, match="Object not found"):
        await spatial_query(action="nearest", path="/Missing")


async def test_scan_scene_error_raises_tool_error(mock_bridge):
    """scan_scene raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene not loaded"})
    with pytest.raises(ToolError, match="Scene not loaded"):
        await scan_scene()
