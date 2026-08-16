"""TDD tests for objects_in_polygon region_id support."""
from unity_mcp.tools.spatial import spatial_query


async def test_objects_in_polygon_region_id_accepted(mock_bridge):
    """region_id must pass through to bridge without raising for objects_in_polygon."""
    mock_bridge.send.return_value = {"ok": True, "data": "3 objects"}
    # Must not raise
    await spatial_query(
        action="objects_in_polygon",
        vertices="0,0;10,0;10,10",
        region_id="zone_a",
    )
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("region_id") == "zone_a"


async def test_objects_in_polygon_region_id_none_omitted(mock_bridge):
    """When region_id is None it must not appear in args sent to bridge."""
    mock_bridge.send.return_value = {"ok": True, "data": "1 object"}
    await spatial_query(
        action="objects_in_polygon",
        vertices="0,0;5,0;5,5",
    )
    sent = mock_bridge.send.call_args[0][1]
    assert "region_id" not in sent


async def test_objects_in_polygon_region_id_only_is_accepted(mock_bridge):
    await spatial_query(action="objects_in_polygon", region_id="zone_a")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["region_id"] == "zone_a"
    assert "vertices" not in sent
