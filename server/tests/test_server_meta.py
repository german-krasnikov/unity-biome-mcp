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


# ── Phase 7a: release_smoke ───────────────────────────────────────────────────

async def test_release_smoke_registered_as_mcp_tool():
    from unity_mcp.server import mcp
    names = {t.name for t in mcp._tool_manager.list_tools()}
    assert "release_smoke" in names


async def test_release_smoke_returns_pass_when_all_ok():
    """release_smoke returns PASS; compile leg calls code_intel.await_compile, not TCP."""
    from unittest.mock import AsyncMock, patch
    import unity_mcp.tools.meta as mod
    mod._send = AsyncMock(return_value="ok: healthy")
    with patch("unity_mcp.tools.code_intel.await_compile", AsyncMock(return_value="compile clean (0s)")):
        result = await mod.release_smoke()
    assert result.startswith("PASS"), f"Expected PASS, got: {result!r}"
    assert "status: ok" in result
    assert "aliases: ok" in result
    assert "compile: ok" in result


async def test_release_smoke_returns_fail_on_error():
    """release_smoke returns FAIL when a C# command returns an error."""
    from unittest.mock import AsyncMock, patch
    import unity_mcp.tools.meta as mod

    async def side_effect(cmd, args, **kw):
        if cmd == "get_status":
            return "err: unity not connected"
        return "ok: healthy"

    mod._send = side_effect
    with patch("unity_mcp.tools.code_intel.await_compile", AsyncMock(return_value="compile clean (0s)")):
        result = await mod.release_smoke()
    assert result.startswith("FAIL"), f"Expected FAIL, got: {result!r}"
    assert "status: FAIL" in result
