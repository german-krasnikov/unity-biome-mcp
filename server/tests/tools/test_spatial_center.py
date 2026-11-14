"""TDD tests for spatial_query center param extension (F13)."""
from unity_mcp.tools.spatial import spatial_query


# Scenario 7: center="0,5,0" forwarded as 'center' key
async def test_spatial_center_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "3 objects within 5m:"}
    await spatial_query(action="objects_in_radius", path="/", radius=5.0, center="0,5,0")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("center") == "0,5,0"


# Scenario 8: center=None omits key entirely
async def test_spatial_center_none_omitted(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "3 objects within 5m:"}
    await spatial_query(action="objects_in_radius", path="/Player", radius=5.0)
    sent = mock_bridge.send.call_args[0][1]
    assert "center" not in sent


# Scenario 9: path + center both forwarded (C# picks center as winner)
async def test_spatial_center_and_path_both_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "2 objects within 3m:"}
    await spatial_query(action="objects_in_radius", path="/Player", radius=3.0, center="1,2,3")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("path") == "/Player"
    assert sent.get("center") == "1,2,3"


# Scenario 10: center param is irrelevant/ignored for other actions (nearest, bounds_info)
async def test_spatial_center_ignored_for_nearest(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "/Enemy dist=3.00"}
    await spatial_query(action="nearest", path="/Player")
    sent = mock_bridge.send.call_args[0][1]
    # center not passed → not in sent
    assert "center" not in sent


async def test_spatial_center_ignored_for_bounds_info(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "center=(0,1,0) size=(2,2,2)"}
    await spatial_query(action="bounds_info", path="/Player")
    sent = mock_bridge.send.call_args[0][1]
    assert "center" not in sent


# Scenario 11: path omitted — center-only call works, no 'path' key sent
async def test_spatial_objects_in_radius_center_only_no_path(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "2 objects within 5m:"}
    await spatial_query(action="objects_in_radius", center="0,0,0", radius=5.0)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("center") == "0,0,0"
    assert "path" not in sent
