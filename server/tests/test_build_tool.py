"""TDD tests for build MCP tool (RED phase)."""
import pytest
from unittest.mock import AsyncMock, MagicMock


# ── build sends correct args ─────────────────────────────────────────────────

async def test_build_sends_action_and_target(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok\ntarget:WebGL"}
    from unity_mcp.tools.build import build
    await build(action="build", target="WebGL")
    cmd, args = mock_bridge.send.call_args[0]
    assert cmd == "build"
    assert args["action"] == "build"
    assert args["target"] == "WebGL"


async def test_build_dev_true_sends_string_true(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok\ntarget:StandaloneOSX"}
    from unity_mcp.tools.build import build
    await build(action="build", dev=True)
    _, args = mock_bridge.send.call_args[0]
    assert args.get("dev") == "true"


async def test_build_dev_false_omits_key(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.build import build
    await build(action="build", dev=False)
    _, args = mock_bridge.send.call_args[0]
    assert "dev" not in args


async def test_build_scenes_forwarded_as_string(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.build import build
    await build(action="build", scenes="A.unity,B.unity")
    _, args = mock_bridge.send.call_args[0]
    assert args["scenes"] == "A.unity,B.unity"


async def test_build_path_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.build import build
    await build(action="build", path="out/game.exe")
    _, args = mock_bridge.send.call_args[0]
    assert args["path"] == "out/game.exe"


async def test_build_target_none_not_in_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    from unity_mcp.tools.build import build
    await build(action="build")
    _, args = mock_bridge.send.call_args[0]
    assert "target" not in args


# ── tool_specs ───────────────────────────────────────────────────────────────

def test_build_in_tool_specs():
    from unity_mcp.tools.tool_specs import _SPECS
    assert "build" in _SPECS
    assert _SPECS["build"].timeout_s == 300.0


def test_build_registered_with_mcp():
    import unity_mcp.tools.build as m
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
        assert "build" in registered
    finally:
        m._send, m._args = orig_send, orig_args
