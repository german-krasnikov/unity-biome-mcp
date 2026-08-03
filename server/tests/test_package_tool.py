"""TDD tests for package MCP tool (RED phase)."""
import pytest
from unittest.mock import AsyncMock, MagicMock


# ── package sends correct args ────────────────────────────────────────────────

async def test_package_list_sends_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "name:com.unity.timeline ver:1.8.6 src:registry"}
    from unity_mcp.tools.packages import package
    await package(action="list")
    cmd, args = mock_bridge.send.call_args[0]
    assert cmd == "package"
    assert args["action"] == "list"
    assert "name" not in args
    assert "version" not in args
    assert "query" not in args


async def test_package_add_sends_name_and_version(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok\nname:com.foo ver:1.0.0"}
    from unity_mcp.tools.packages import package
    await package(action="add", name="com.foo", version="1.0.0")
    _, args = mock_bridge.send.call_args[0]
    assert args["action"] == "add"
    assert args["name"] == "com.foo"
    assert args["version"] == "1.0.0"


async def test_package_add_omits_version_when_none(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok\nname:com.foo ver:2.0.0"}
    from unity_mcp.tools.packages import package
    await package(action="add", name="com.foo")
    _, args = mock_bridge.send.call_args[0]
    assert args["name"] == "com.foo"
    assert "version" not in args


async def test_package_search_sends_query(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "id:com.unity.cinemachine ver:3.1.2"}
    from unity_mcp.tools.packages import package
    await package(action="search", query="cinemachine")
    _, args = mock_bridge.send.call_args[0]
    assert args["action"] == "search"
    assert args["query"] == "cinemachine"


async def test_package_remove_sends_name(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.packages import package
    await package(action="remove", name="com.foo")
    _, args = mock_bridge.send.call_args[0]
    assert args["action"] == "remove"
    assert args["name"] == "com.foo"


# ── tool_specs ────────────────────────────────────────────────────────────────

def test_package_in_tool_specs():
    from unity_mcp.tools.tool_specs import _SPECS
    assert "package" in _SPECS
    assert _SPECS["package"].category == "ASSETS"
    assert _SPECS["package"].timeout_s == 60.0


def test_package_registered_with_mcp():
    import unity_mcp.tools.packages as m
    registered = {}

    def mock_tool(annotations=None):
        def decorator(fn):
            registered[fn.__name__] = annotations
            return fn
        return decorator

    mock_mcp = MagicMock()
    mock_mcp.tool = mock_tool
    orig_send, orig_args = m._send, m._args
    try:
        m.register(mock_mcp, AsyncMock(), lambda **kw: {k: v for k, v in kw.items() if v is not None})
        assert "package" in registered
    finally:
        m._send, m._args = orig_send, orig_args
