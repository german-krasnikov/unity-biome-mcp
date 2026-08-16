"""TDD tests for spatial_query objects_in_polygon action — Python validation and forwarding.
No Unity required. All tests use mock_bridge fixture from conftest.py.
"""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.tools.spatial import spatial_query, _validate_polygon


# ── Test data ──────────────────────────────────────────────────────────────────
TRIANGLE = "0,0;1,0;0.5,1"
SQUARE   = "0,0;10,0;10,10;0,10"
V64      = ";".join(f"{i}.00,{i}.00" for i in range(64))   # exactly max (64 pairs — below 256 limit)
V257     = ";".join(f"{i}.00,{i}.00" for i in range(257))  # over hard limit


# ── _validate_polygon: valid inputs ───────────────────────────────────────────

def test_validate_triangle_accepted():
    """Minimum polygon (3 vertices) is accepted without raising."""
    _validate_polygon(TRIANGLE)  # should not raise


def test_validate_64_vertices_accepted():
    """64 vertices is well within 256 limit."""
    _validate_polygon(V64)  # should not raise


def test_validate_negative_coords_accepted():
    """Negative coordinates are valid."""
    _validate_polygon("-5.5,-3.2;2.1,-1.0;0,4.7")  # should not raise


# ── _validate_polygon: invalid inputs ─────────────────────────────────────────

def test_validate_none_raises():
    with pytest.raises(ToolError, match="vertices required"):
        _validate_polygon(None)


def test_validate_empty_raises():
    with pytest.raises(ToolError, match="vertices required"):
        _validate_polygon("")


def test_validate_2_vertices_raises():
    with pytest.raises(ToolError, match=">=3 vertices.*got 2"):
        _validate_polygon("0,0;1,0")


def test_validate_1_vertex_raises():
    with pytest.raises(ToolError, match=">=3 vertices.*got 1"):
        _validate_polygon("0,0")


def test_validate_257_vertices_raises():
    with pytest.raises(ToolError, match="max 256.*got 257"):
        _validate_polygon(V257)


def test_validate_bad_format_reports_vertex_index():
    """Malformed vertex: error message includes vertex index."""
    with pytest.raises(ToolError, match="vertex 1"):
        _validate_polygon("0,0;bad;1,1")


def test_validate_single_number_per_vertex_raises():
    """Vertex with only 1 number (not x,z pair)."""
    with pytest.raises(ToolError, match="vertex 0.*expected"):
        _validate_polygon("5;10;15")


def test_validate_non_numeric_reports_index():
    with pytest.raises(ToolError, match="vertex 2.*non-numeric"):
        _validate_polygon("0,0;1,0;abc,def")


def test_validate_out_of_range_raises():
    with pytest.raises(ToolError, match="out of range"):
        _validate_polygon("0,0;200000,0;0,1")


def test_validate_trailing_semicolon_raises():
    with pytest.raises(ToolError):
        _validate_polygon("0,0;1,0;1,1;")


# ── spatial_query forwarding ───────────────────────────────────────────────────

async def test_polygon_vertices_forwarded(mock_bridge):
    """vertices param is forwarded as-is to C# bridge."""
    mock_bridge.send.return_value = {"ok": True, "data": "3 objects in polygon (area=100.0m2):\n  /Obj1"}
    result = await spatial_query(action="objects_in_polygon", vertices=SQUARE)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "objects_in_polygon"
    assert sent["vertices"] == SQUARE
    assert "3 objects" in result


async def test_polygon_component_filter_forwarded(mock_bridge):
    """component filter is forwarded."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await spatial_query(action="objects_in_polygon", vertices=TRIANGLE, component="Tree")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("component") == "Tree"


async def test_polygon_cap_forwarded_as_string(mock_bridge):
    """cap int is converted to string before forwarding (matches _args pattern)."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await spatial_query(action="objects_in_polygon", vertices=TRIANGLE, cap=30)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("cap") == "30"


async def test_polygon_region_id_forwarded(mock_bridge):
    """region_id is forwarded."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await spatial_query(action="objects_in_polygon", vertices=TRIANGLE, region_id="north_forest")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("region_id") == "north_forest"


async def test_polygon_no_vertices_raises_before_send(mock_bridge):
    """Validation runs before TCP send — bridge never called."""
    with pytest.raises(ToolError, match="vertices or region_id required"):
        await spatial_query(action="objects_in_polygon")
    mock_bridge.send.assert_not_called()


async def test_polygon_region_id_is_an_alternative_source(mock_bridge):
    await spatial_query(action="objects_in_polygon", region_id="north_forest")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["region_id"] == "north_forest"
    assert "vertices" not in sent


async def test_polygon_supplied_vertices_are_validated_with_region_id(mock_bridge):
    with pytest.raises(ToolError, match=">=3 vertices"):
        await spatial_query(
            action="objects_in_polygon",
            vertices="0,0;1,0",
            region_id="north_forest",
        )
    mock_bridge.send.assert_not_called()


async def test_polygon_bridge_error_propagates(mock_bridge):
    """C# error (ok=False) propagates as ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Parse error in CSV"})
    with pytest.raises(ToolError, match="Parse error"):
        await spatial_query(action="objects_in_polygon", vertices=TRIANGLE)


async def test_other_actions_unaffected(mock_bridge):
    """Other spatial_query actions do NOT trigger polygon validation."""
    mock_bridge.send.return_value = {"ok": True, "data": "/Enemy dist=3.00"}
    # nearest action should work fine without vertices
    result = await spatial_query(action="nearest", path="/Player")
    assert "dist" in result
    mock_bridge.send.assert_called_once()
