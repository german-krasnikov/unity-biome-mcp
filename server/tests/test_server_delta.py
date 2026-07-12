import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.tools.objects import set_property_delta
from unity_mcp.tools.scene import scene_diff


async def test_set_property_delta_sends_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "1 → 6"})
    result = await set_property_delta(
        path="/Player", component="Rigidbody", prop="m_Mass", delta="+5"
    )
    mock_bridge.send.assert_called_once_with(
        "set_property_delta",
        {"path": "/Player", "component": "Rigidbody", "prop": "m_Mass", "delta": "+5"},
        timeout=30.0,
    )
    assert result == "1 → 6"


async def test_scene_diff_sends_command(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "NO CHANGES"})
    result = await scene_diff()
    mock_bridge.send.assert_called_once_with("scene_diff", {}, timeout=30.0)
    assert result == "NO CHANGES"


async def test_set_property_delta_error_raises_tool_error(mock_bridge):
    """set_property_delta raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Property not numeric"})
    with pytest.raises(ToolError, match="Property not numeric"):
        await set_property_delta(path="/Player", component="Rigidbody", prop="m_Mass", delta="+5")


async def test_scene_diff_error_raises_tool_error(mock_bridge):
    """scene_diff raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No snapshot available"})
    with pytest.raises(ToolError, match="No snapshot available"):
        await scene_diff()
