"""transfer_asset and copy_asset are removed (dead code, required 2+ Unity instances).
This file keeps the test count stable with replacement tests for reconnect_unity."""
import pytest
from unittest.mock import AsyncMock, Mock, patch


async def test_reconnect_unity_calls_slot_connect(mock_bridge):
    from unity_mcp.server import reconnect_unity
    import unity_mcp.server as srv
    srv.slot.connect = AsyncMock(return_value="Connected to Unity on port 9500")
    result = await reconnect_unity(9500)
    srv.slot.connect.assert_awaited_once_with(9500)
    assert "Connected" in result


async def test_reconnect_unity_no_slot_raises():
    from mcp.server.fastmcp.exceptions import ToolError
    with patch("unity_mcp.tools.connection._get_slot", return_value=None):
        from unity_mcp.tools.connection import reconnect_unity
        with pytest.raises(ToolError, match="Server not initialized"):
            await reconnect_unity(9500)
