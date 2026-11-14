"""TDD tests for mcp_status tool (P2.4)."""


async def test_mcp_status_sends_get_status(mock_bridge, bridge_response):
    """mcp_status() sends 'get_status' command to Unity."""
    bridge_response(data="scene=Main\ndirty=False\nplaying=False\ncompiling=False\nport=9500\naliases=0")
    from unity_mcp.tools.meta import mcp_status
    await mcp_status()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "get_status"


async def test_mcp_status_returns_raw(mock_bridge, bridge_response):
    """mcp_status() passes Unity response through unchanged."""
    data = "scene=SampleScene\ndirty=True\nplaying=False\ncompiling=False\nport=9500\naliases=3"
    bridge_response(data=data)
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert "scene=SampleScene" in result
    assert "aliases=3" in result


async def test_mcp_status_registered_as_mcp_tool():
    from unity_mcp.server import mcp
    names = {t.name for t in mcp._tool_manager.list_tools()}
    assert "mcp_status" in names
