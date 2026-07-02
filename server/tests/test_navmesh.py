"""TDD tests for navmesh_query tool."""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.tools.spatial import navmesh_query


async def test_navmesh_sample_sends_correct_args(mock_bridge):
    """sample action sends center + max_distance + area_mask."""
    mock_bridge.send.return_value = {"ok": True, "data": "walkable: true\nposition: (1, 0, 2)\ndistance: 0.5"}
    result = await navmesh_query(action="sample", center="1,0,2", max_distance=3.0, area_mask=-1)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "sample"
    assert sent["center"] == "1,0,2"
    assert sent["max_distance"] == "3.0"
    assert sent["area_mask"] == "-1"
    assert "walkable" in result


async def test_navmesh_path_sends_correct_args(mock_bridge):
    """path action sends from + to + area_mask."""
    mock_bridge.send.return_value = {"ok": True, "data": "status: Complete\ncorners: 3"}
    result = await navmesh_query(action="path", from_pos="0,0,0", to="5,0,5")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "path"
    assert sent["from"] == "0,0,0"
    assert sent["to"] == "5,0,5"
    assert sent["area_mask"] == "-1"
    assert "status" in result


async def test_navmesh_raycast_sends_correct_args(mock_bridge):
    """raycast action sends from + to."""
    mock_bridge.send.return_value = {"ok": True, "data": "hit: false\nposition: (5, 0, 5)\ndistance: 7.071"}
    result = await navmesh_query(action="raycast", from_pos="0,0,0", to="5,0,5")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "raycast"
    assert sent["from"] == "0,0,0"
    assert sent["to"] == "5,0,5"
    assert "hit" in result


async def test_navmesh_sample_omits_from_to(mock_bridge):
    """sample action does not send 'from' or 'to' keys."""
    mock_bridge.send.return_value = {"ok": True, "data": "walkable: false"}
    await navmesh_query(action="sample", center="0,0,0")
    sent = mock_bridge.send.call_args[0][1]
    assert "from" not in sent
    assert "to" not in sent


async def test_navmesh_custom_area_mask(mock_bridge):
    """area_mask passes through as string."""
    mock_bridge.send.return_value = {"ok": True, "data": "walkable: true\nposition: (0, 0, 0)\ndistance: 0"}
    await navmesh_query(action="sample", center="0,0,0", area_mask=3)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["area_mask"] == "3"


async def test_navmesh_error_raises_tool_error(mock_bridge):
    """navmesh_query raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "NavMesh not baked"})
    with pytest.raises(ToolError, match="NavMesh not baked"):
        await navmesh_query(action="sample", center="0,0,0")


async def test_navmesh_bake_sends_command(mock_bridge):
    """bake action sends action=bake to Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "baked (legacy NavMeshBuilder)"}
    result = await navmesh_query(action="bake")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "bake"
    assert "baked" in result


async def test_navmesh_status_sends_command(mock_bridge):
    """status action sends action=status to Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "triangles:42\nvertices:128\nareas:42"}
    result = await navmesh_query(action="status")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "status"
    assert "triangles" in result


async def test_navmesh_clear_sends_command(mock_bridge):
    """clear action sends action=clear to Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "cleared"}
    result = await navmesh_query(action="clear")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "clear"
    assert result == "cleared"
