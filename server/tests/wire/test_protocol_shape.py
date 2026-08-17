"""Wire-level tests for TCP response format contracts (no Unity required)."""
from __future__ import annotations

import pytest

from tests.wire.helpers.fake_server import FakeUnityServer
from unity_mcp.bridge import UnityBridge

pytestmark = pytest.mark.wire


async def test_response_ok_is_bool(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """ok field is always a Python bool, never an int or string."""
    result = await wire_bridge.send("ping", {})
    assert type(result["ok"]) is bool


async def test_response_id_matches_request(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Response id echoes request id — bridge returns only when IDs match."""
    result = await wire_bridge.send("ping", {})
    assert "ok" in result  # bridge only returns if id matched


async def test_success_has_data_field(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """ok:true response carries a 'data' key."""
    result = await wire_bridge.send("ping", {})
    assert result["ok"] is True
    assert "data" in result


async def test_error_has_err_not_data(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """ok:false response has 'err' key, NOT 'data'."""
    wire_server.set_response("trigger_err", ok=False, error="something went wrong")
    result = await wire_bridge.send("trigger_err", {})
    assert result["ok"] is False
    assert "err" in result
    assert "data" not in result


async def test_ping_data_is_pong(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """ping → data == 'pong'. Catches ok:true with empty data."""
    result = await wire_bridge.send("ping", {})
    assert result["data"] == "pong"


async def test_get_version_contains_proto_3(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """get_version data contains 'proto:3' prefix."""
    wire_server.set_response("get_version", data="proto:3|plugin:1.0|stamp:test")
    result = await wire_bridge.send("get_version", {})
    assert "proto:3" in result["data"]


async def test_get_hierarchy_returns_text_lines(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """get_hierarchy data is multi-line text, not JSON."""
    result = await wire_bridge.send("get_hierarchy", {})
    assert isinstance(result["data"], str)
    assert "\n" in result["data"]


async def test_get_status_has_scene_field(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """get_status data contains 'scene=' key-value line."""
    result = await wire_bridge.send("get_status", {})
    assert "scene=" in result["data"]


async def test_unknown_command_returns_ok_false(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Command not in peer responses → ok:false (ScriptedUnityPeer default)."""
    result = await wire_bridge.send("completely_unknown_xyz_987", {})
    assert result["ok"] is False


async def test_response_frame_carries_request_id(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Two consecutive sends get correlated responses (no cross-talk)."""
    r1 = await wire_bridge.send("ping", {})
    r2 = await wire_bridge.send("get_status", {})
    assert r1["data"] == "pong"
    assert "scene=" in r2["data"]
