import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import autofit_collider


async def test_autofit_collider_sends_command(mock_bridge, bridge_response):
    bridge_response(data="BoxCollider fitted: center=(0, 0, 0), size=(1, 1, 1)")
    result = await autofit_collider(path="/Player")
    mock_bridge.send.assert_called_once_with(
        "autofit_collider", {"path": "/Player", "type": "box"}, timeout=30.0)
    assert "BoxCollider fitted" in result


async def test_autofit_collider_sphere(mock_bridge, bridge_response):
    bridge_response(data="SphereCollider fitted: center=(0, 0, 0), radius=0.5")
    result = await autofit_collider(path="/Player", type="sphere")
    args = mock_bridge.send.call_args[0][1]
    assert args["type"] == "sphere"
    assert "SphereCollider fitted" in result


async def test_autofit_collider_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "object not found"})
    with pytest.raises(ToolError, match="object not found"):
        await autofit_collider(path="/Missing")
