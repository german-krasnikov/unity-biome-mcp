"""TDD tests for bake MCP tool (RED phase)."""
import pytest
from unittest.mock import AsyncMock, MagicMock


# ── bake sends correct cmd ───────────────────────────────────────────────────

async def test_bake_sends_cmd_and_target(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "status:started"}
    from unity_mcp.tools.rendering import bake
    await bake("lighting")
    cmd, args = mock_bridge.send.call_args[0]
    assert cmd == "bake"
    assert args["target"] == "lighting"


async def test_bake_action_none_not_in_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "status:started"}
    from unity_mcp.tools.rendering import bake
    await bake("lighting")
    _, args = mock_bridge.send.call_args[0]
    assert "action" not in args


async def test_bake_action_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "status:baking\nprogress:0.5"}
    from unity_mcp.tools.rendering import bake
    await bake("lighting", action="status")
    _, args = mock_bridge.send.call_args[0]
    assert args["action"] == "status"


async def test_bake_occlusion_target(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok:cleared"}
    from unity_mcp.tools.rendering import bake
    await bake("occlusion", action="clear")
    _, args = mock_bridge.send.call_args[0]
    assert args["target"] == "occlusion"
    assert args["action"] == "clear"


# ── tool_specs ───────────────────────────────────────────────────────────────

def test_bake_in_tool_specs():
    from unity_mcp.tools.tool_specs import _SPECS
    assert "bake" in _SPECS
    assert _SPECS["bake"].category == "ASSETS"


def test_bake_is_registered_with_mcp():
    import unity_mcp.tools.rendering as m
    from mcp.types import ToolAnnotations

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
        assert "bake" in registered, "bake not registered"
    finally:
        m._send, m._args = orig_send, orig_args
