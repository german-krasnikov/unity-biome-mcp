"""TDD tests for Q7 (layer targeting) + L1 (Layer CRUD) animator params."""
import pytest
from unittest.mock import AsyncMock

from unity_mcp.server import animator


# Q7: layer param forwarding

async def test_animator_add_state_with_layer(mock_bridge):
    """layer=1 is forwarded to bridge for add_state."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "added: Run"})
    await animator(action="add_state", path="/Hero", states="Run", layer=1)
    args = mock_bridge.send.call_args[0][1]
    assert args["layer"] == 1


async def test_animator_add_state_default_layer_not_sent(mock_bridge):
    """layer absent when not provided (default None)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "added: Idle"})
    await animator(action="add_state", path="/Hero", states="Idle")
    args = mock_bridge.send.call_args[0][1]
    assert "layer" not in args


# L1: add_layer

async def test_animator_add_layer_sends_all_params(mock_bridge):
    """add_layer forwards name, weight, blending."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "layer added: Upperbody (idx:1) w:1.0 blend:Override"})
    await animator(action="add_layer", path="/Hero", name="Upperbody", weight=1.0, blending="Override")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_layer"
    assert args["name"] == "Upperbody"
    assert args["weight"] == 1.0
    assert args["blending"] == "Override"


# L1: remove_layer

async def test_animator_remove_layer_by_name(mock_bridge):
    """remove_layer forwards layer name."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "layer removed: Upperbody (idx:1)"})
    await animator(action="remove_layer", path="/Hero", layer="Upperbody")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "remove_layer"
    assert args["layer"] == "Upperbody"


# L1: set_layer_weight

async def test_animator_set_layer_weight(mock_bridge):
    """set_layer_weight forwards layer and weight."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "layer weight: Upperbody = 0.5"})
    await animator(action="set_layer_weight", path="/Hero", layer="Upperbody", weight=0.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_layer_weight"
    assert args["layer"] == "Upperbody"
    assert args["weight"] == 0.5


# L1: set_layer_blending

async def test_animator_set_layer_blending(mock_bridge):
    """set_layer_blending forwards layer and blending."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "layer blending: Upperbody = Additive"})
    await animator(action="set_layer_blending", path="/Hero", layer="Upperbody", blending="Additive")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_layer_blending"
    assert args["layer"] == "Upperbody"
    assert args["blending"] == "Additive"


# L1: rename_layer

async def test_animator_rename_layer(mock_bridge):
    """rename_layer forwards name (old) and new_name via name+layer."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "layer renamed: Base → BaseLayer (idx:0)"})
    await animator(action="rename_layer", path="/Hero", layer="Base", name="BaseLayer")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "rename_layer"
    assert args["layer"] == "Base"
    assert args["name"] == "BaseLayer"


# L1: None params excluded

async def test_animator_excludes_none_params(mock_bridge):
    """None params (layer, weight, blending) are not sent to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "..."})
    await animator(action="get", path="/Hero")
    args = mock_bridge.send.call_args[0][1]
    assert "layer" not in args
    assert "weight" not in args
    assert "blending" not in args


# M7: set_state_speed

async def test_animator_set_state_speed(mock_bridge):
    """set_state_speed forwards state and value."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "state speed: Run = 1.5"})
    await animator(action="set_state_speed", path="/Hero", state="Run", value="1.5")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_state_speed"
    assert args["state"] == "Run"
    assert args["value"] == "1.5"


# M8: update_transition

async def test_animator_update_transition(mock_bridge):
    """update_transition forwards source, target, duration."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "transition updated: Idle → Walk"})
    await animator(action="update_transition", path="/Hero", source="Idle", target="Walk", duration=0.3)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "update_transition"
    assert args["source"] == "Idle"
    assert args["target"] == "Walk"
    assert args["duration"] == 0.3


# M9: set_avatar

async def test_animator_set_avatar(mock_bridge):
    """set_avatar forwards avatar_path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "avatar set: HumanoidAvatar"})
    await animator(action="set_avatar", path="/Hero", avatar_path="Assets/Models/Human.fbx")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_avatar"
    assert args["avatar_path"] == "Assets/Models/Human.fbx"


# M10: rename_state

async def test_animator_rename_state(mock_bridge):
    """rename_state forwards state (old) and name (new)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "state renamed: Run → Sprint"})
    await animator(action="rename_state", path="/Hero", state="Run", name="Sprint")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "rename_state"
    assert args["state"] == "Run"
    assert args["name"] == "Sprint"


# M10: rename_param

async def test_animator_rename_param(mock_bridge):
    """rename_param forwards param (old) and name (new)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "param renamed: Speed → MoveSpeed"})
    await animator(action="rename_param", path="/Hero", param="Speed", name="MoveSpeed")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "rename_param"
    assert args["param"] == "Speed"
    assert args["name"] == "MoveSpeed"


# M7: value param excluded when None

async def test_animator_value_excluded_when_none(mock_bridge):
    """value param not sent when None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "..."})
    await animator(action="get", path="/Hero")
    args = mock_bridge.send.call_args[0][1]
    assert "value" not in args
    assert "avatar_path" not in args
