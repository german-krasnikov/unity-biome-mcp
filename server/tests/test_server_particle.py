import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import particle


async def test_particle_get_sends_action_and_path(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "main:\nstartSpeed: 5"})
    result = await particle(action="get", path="/FX")
    mock_bridge.send.assert_called_once_with("particle", {"action": "get", "path": "/FX"}, timeout=30.0)
    assert "startSpeed" in result


async def test_particle_get_with_module_sends_module(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "emission:\nrateOverTime: 10"})
    result = await particle(action="get", path="/FX", module="emission")
    mock_bridge.send.assert_called_once_with(
        "particle", {"action": "get", "path": "/FX", "module": "emission"}, timeout=30.0
    )
    assert "rateOverTime" in result


async def test_particle_create_sends_action_path_name(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: /FX/Smoke"})
    result = await particle(action="create", path="/FX", name="Smoke")
    mock_bridge.send.assert_called_once_with(
        "particle", {"action": "create", "path": "/FX", "name": "Smoke"}, timeout=30.0
    )
    assert "Created" in result


async def test_particle_create_with_preset_sends_preset(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: /FX/Fire"})
    result = await particle(action="create", path="/FX", name="Fire", preset="fire")
    mock_bridge.send.assert_called_once_with(
        "particle", {"action": "create", "path": "/FX", "name": "Fire", "preset": "fire"}, timeout=30.0
    )
    assert "Created" in result


async def test_particle_set_sends_module_prop_value(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Set main.startSpeed = 10"})
    result = await particle(action="set", path="/FX", module="main", prop="startSpeed", value="10")
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX", "module": "main", "prop": "startSpeed", "value": "10"},
        timeout=30.0,
    )
    assert "startSpeed" in result


async def test_particle_apply_sends_preset(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Applied preset: snow"})
    result = await particle(action="apply", path="/FX", preset="snow")
    mock_bridge.send.assert_called_once_with(
        "particle", {"action": "apply", "path": "/FX", "preset": "snow"}, timeout=30.0
    )
    assert "snow" in result


async def test_particle_excludes_none_params(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await particle(action="get", path="/FX", name=None, module=None, prop=None, value=None, preset=None)
    args = mock_bridge.send.call_args[0][1]
    assert "name" not in args
    assert "module" not in args
    assert "prop" not in args
    assert "value" not in args
    assert "preset" not in args


async def test_particle_error_raises_tool_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "ParticleSystem not found"})
    with pytest.raises(ToolError, match="ParticleSystem not found"):
        await particle(action="get", path="/Missing")


# Unique Phase 18 scenarios: specific prop/value combinations

@pytest.mark.parametrize("module,prop,value,response_data,assert_in", [
    ("main",  "startspeed", "2",       "Set main.startSpeed = 2",      "startSpeed"),
    ("main",  "startsize",  "0.3,0.8", "Set main.startSize = 0.3,0.8", "startSize"),
    ("main",  "startcolor", "#FF6600", "Set main.startColor = #FF6600", "#FF6600"),
    ("noise", "enabled",    "true",    "Set noise.enabled = true",      "enabled"),
    ("noise", "strength",   "0.3",     "Set noise.strength = 0.3",      "strength"),
])
async def test_particle_set_phase18_scenarios(mock_bridge, module, prop, value, response_data, assert_in):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": response_data})
    result = await particle(action="set", path="/FX/Campfire", module=module, prop=prop, value=value)
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX/Campfire", "module": module, "prop": prop, "value": value},
        timeout=30.0,
    )
    assert assert_in in result


# L2: VFX gradient/curve forwarding tests


async def test_particle_set_color_gradient_forwards(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "set: colorOverLifetime.gradient = #FF0000@0;#00FF00@1"})
    result = await particle(action="set", path="/FX", module="colorOverLifetime", prop="gradient", value="#FF0000@0;#00FF00@1")
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX", "module": "colorOverLifetime", "prop": "gradient", "value": "#FF0000@0;#00FF00@1"},
        timeout=30.0,
    )
    assert "gradient" in result


async def test_particle_set_size_curve_forwards(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "set: sizeOverLifetime.curve = 0:0.5;1:1.0"})
    result = await particle(action="set", path="/FX", module="sizeOverLifetime", prop="curve", value="0:0.5;1:1.0")
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX", "module": "sizeOverLifetime", "prop": "curve", "value": "0:0.5;1:1.0"},
        timeout=30.0,
    )
    assert "curve" in result


async def test_particle_set_velocity_x_forwards(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "set: velocityOverLifetime.x = 0:0;1:3"})
    result = await particle(action="set", path="/FX", module="velocityOverLifetime", prop="x", value="0:0;1:3")
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX", "module": "velocityOverLifetime", "prop": "x", "value": "0:0;1:3"},
        timeout=30.0,
    )
    assert "x" in result


async def test_particle_set_color_enabled_still_works(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "set: colorOverLifetime.enabled = true"})
    result = await particle(action="set", path="/FX", module="colorOverLifetime", prop="enabled", value="true")
    mock_bridge.send.assert_called_once_with(
        "particle",
        {"action": "set", "path": "/FX", "module": "colorOverLifetime", "prop": "enabled", "value": "true"},
        timeout=30.0,
    )
    assert "enabled" in result


# M16: trails expanded props

async def test_particle_set_trails_ratio_forwards(mock_bridge):
    """trails module ratio property forwarded."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "set: trails.ratio = 0.5"})
    result = await particle(action="set", path="/FX", module="trails", prop="ratio", value="0.5")
    args = mock_bridge.send.call_args[0][1]
    assert args["module"] == "trails"
    assert args["prop"] == "ratio"
    assert args["value"] == "0.5"


# M17: play/stop/pause

async def test_particle_play_sends_action(mock_bridge):
    """play action sends action and path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: playing"})
    result = await particle(action="play", path="/FX/Fire")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "play"
    assert args["path"] == "/FX/Fire"


async def test_particle_stop_sends_action(mock_bridge):
    """stop action sends action and path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: stopped"})
    result = await particle(action="stop", path="/FX/Fire")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "stop"


async def test_particle_pause_sends_action(mock_bridge):
    """pause action sends action and path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: paused"})
    result = await particle(action="pause", path="/FX/Fire")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "pause"
