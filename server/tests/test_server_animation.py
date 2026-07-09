"""TDD tests for Q3 (binding_path) and Q4 (tangent) animation params."""
import pytest
from unittest.mock import AsyncMock

from unity_mcp.server import animation


# Q3: binding_path param
async def test_animation_binding_path_forwarded(mock_bridge):
    """binding_path is forwarded to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await animation(action="edit", path="/Obj", binding_path="Hand/Finger")
    args = mock_bridge.send.call_args[0][1]
    assert args["binding_path"] == "Hand/Finger"


async def test_animation_binding_path_default_not_sent(mock_bridge):
    """binding_path absent when not provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await animation(action="edit", path="/Obj", clip="Walk")
    args = mock_bridge.send.call_args[0][1]
    assert "binding_path" not in args


# Q4: tangent param
async def test_animation_tangent_linear_forwarded(mock_bridge):
    """tangent='linear' is forwarded to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await animation(action="edit", path="/Obj", clip="Walk", tangent="linear")
    args = mock_bridge.send.call_args[0][1]
    assert args["tangent"] == "linear"


async def test_animation_tangent_default_not_sent(mock_bridge):
    """tangent absent when not provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await animation(action="edit", path="/Obj", clip="Walk")
    args = mock_bridge.send.call_args[0][1]
    assert "tangent" not in args


# L3: Animation Events

async def test_animation_add_event_sends_all_params(mock_bridge):
    """add_event forwards function_name, time, int_param to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "event added: OnStep @ 0.5s"})
    await animation(action="add_event", path="/Char", clip="Run",
                    time=0.5, function_name="OnStep", int_param=1)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_event"
    assert args["function_name"] == "OnStep"
    assert args["time"] == 0.5
    assert args["int_param"] == 1


async def test_animation_remove_event_sends_time(mock_bridge):
    """remove_event forwards time to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "event removed: 1 at 0.5s"})
    await animation(action="remove_event", path="/Char", clip="Run", time=0.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "remove_event"
    assert args["time"] == 0.5


async def test_animation_get_events_no_extra_params(mock_bridge):
    """get_events sends only action, path, clip — no None extras."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "events: 0"})
    await animation(action="get_events", path="/Char", clip="Run")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_events", "path": "/Char", "clip": "Run"}


async def test_animation_add_event_excludes_none_optional(mock_bridge):
    """None int_param, float_param, string_param not sent to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "event added: F @ 0.5s"})
    await animation(action="add_event", path="/Char", clip="Run", time=0.5,
                    function_name="F", int_param=None, float_param=None, string_param=None)
    args = mock_bridge.send.call_args[0][1]
    assert "int_param" not in args
    assert "float_param" not in args
    assert "string_param" not in args


async def test_animation_event_error_propagates(mock_bridge):
    """Bridge error raises ToolError."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Clip not found"})
    with pytest.raises(ToolError):
        await animation(action="add_event", path="/Char", clip="Missing",
                        time=0.5, function_name="OnStep")


# M11: color curves

async def test_animation_color_keys_forwarded(mock_bridge):
    """Color hex keys are forwarded to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Walk | set_keys m_Color"})
    await animation(action="set_keys", path="/Light", clip="ColorAnim",
                    property="m_Color", keys="t:0 v:#FF0000; t:1 v:#0000FF",
                    component_type="Light")
    args = mock_bridge.send.call_args[0][1]
    assert args["keys"] == "t:0 v:#FF0000; t:1 v:#0000FF"
    assert args["property"] == "m_Color"


# M12: wrap mode

async def test_animation_set_wrap_forwards_keys(mock_bridge):
    """set_wrap uses keys param for wrap mode string."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Walk | set_wrap None"})
    await animation(action="set_wrap", path="/Char", clip="Walk", keys="loop")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_wrap"
    assert args["keys"] == "loop"


# M13: framerate

async def test_animation_set_framerate_forwards_keys(mock_bridge):
    """set_framerate uses keys param for framerate value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "edited: Walk | set_framerate None"})
    await animation(action="set_framerate", path="/Char", clip="Walk", keys="30")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_framerate"
    assert args["keys"] == "30"


# M14: get clip path

async def test_animation_get_clip_path(mock_bridge):
    """get_clip_path sends action and clip."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Animations/Walk.anim"})
    await animation(action="get_clip_path", path="/Char", clip="Walk")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "get_clip_path"
    assert args["clip"] == "Walk"
