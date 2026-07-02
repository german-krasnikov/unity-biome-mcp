"""Tests for tools/meta.py — M2: discover_tools/doctor/resolve_tool_schema/set_llm_config
moved out of server.py's composition root into the standard register(mcp, send, args) pattern."""


async def test_discover_tools_importable_from_meta():
    from unity_mcp.tools.meta import discover_tools
    assert discover_tools is not None


async def test_meta_tools_registered_via_register_all():
    from unity_mcp.server import mcp
    names = {t.name for t in mcp._tool_manager.list_tools()}
    assert {"discover_tools", "doctor", "resolve_tool_schema", "set_llm_config"} <= names
