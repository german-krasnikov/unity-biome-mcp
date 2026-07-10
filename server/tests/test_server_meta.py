"""Tests for tools/meta.py — M2: discover_tools/doctor/resolve_tool_schema/set_llm_config
moved out of server.py's composition root into the standard register(mcp, send, args) pattern."""


async def test_discover_tools_importable_from_meta():
    from unity_mcp.tools.meta import discover_tools
    assert discover_tools is not None


async def test_meta_tools_registered_via_register_all():
    from unity_mcp.server import mcp
    names = {t.name for t in mcp._tool_manager.list_tools()}
    assert {"discover_tools", "doctor", "resolve_tool_schema", "set_llm_config"} <= names


async def test_alias_status_registered_as_mcp_tool():
    from unity_mcp.server import mcp
    names = {t.name for t in mcp._tool_manager.list_tools()}
    assert "alias_status" in names


async def test_alias_status_sends_correct_command(mock_bridge, bridge_response):
    """alias_status() sends alias_status command with no args."""
    bridge_response(data="loaded: empty\ncount: 0\nstale: False")
    from unity_mcp.tools.meta import alias_status
    result = await alias_status()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[0] == "alias_status"
    assert call_args[1] == {}
    assert "count: 0" in result
