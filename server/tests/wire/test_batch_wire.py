"""Wire-level tests for the batch command protocol (no Unity required)."""

import pytest

from tests.wire.helpers.fake_server import FakeUnityServer
from unity_mcp.bridge import UnityBridge

pytestmark = pytest.mark.wire


async def test_batch_single_command_ok_prefix(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Single batch command response starts with '[0] ok'."""
    wire_server.set_response("batch", data="[0] ok: /Cube")
    result = await wire_bridge.send("batch", {"commands": "create_object name=Cube"})
    assert result["ok"] is True
    assert result["data"].startswith("[0] ok")


async def test_batch_two_commands_two_lines(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """N commands → N result lines."""
    wire_server.set_response("batch", data="[0] ok: /A\n[1] ok")
    result = await wire_bridge.send("batch", {"commands": "create_object name=A\ncreate_object name=B"})
    assert len(result["data"].splitlines()) == 2


async def test_batch_lines_indexed_sequentially(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Result lines carry sequential [N] prefix matching command order."""
    wire_server.set_response("batch", data="[0] ok\n[1] ok\n[2] ok")
    result = await wire_bridge.send("batch", {"commands": "a\nb\nc"})
    for i, line in enumerate(result["data"].splitlines()):
        assert line.startswith(f"[{i}]"), f"line {i} missing index: {line!r}"


async def test_batch_error_line_has_err_prefix(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Error result lines use 'err:' prefix, not 'error:' or 'failed:'."""
    wire_server.set_response("batch", data="[0] err: Not found")
    result = await wire_bridge.send("batch", {"commands": "bad_cmd"})
    assert "err:" in result["data"]
    assert "error:" not in result["data"]


async def test_batch_skip_after_error_stop(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """on_error=stop produces '[N] skip' for commands after the failure."""
    wire_server.set_response("batch", data="[0] err: Not found\n[1] skip")
    result = await wire_bridge.send("batch", {"commands": "bad\ngood", "on_error": "stop"})
    assert any("skip" in line for line in result["data"].splitlines())


async def test_batch_ok_true_on_success(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """Bridge returns ok:true for successful batch responses."""
    wire_server.set_response("batch", data="[0] ok")
    result = await wire_bridge.send("batch", {"commands": "anything"})
    assert result["ok"] is True


async def test_batch_large_50_commands_intact(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """50-command batch returns 50 result lines without truncation."""
    data = "\n".join(f"[{i}] ok" for i in range(50))
    wire_server.set_response("batch", data=data)
    result = await wire_bridge.send(
        "batch", {"commands": "\n".join(f"cmd_{i}" for i in range(50))}
    )
    assert len(result["data"].splitlines()) == 50


async def test_batch_command_arrives_at_server(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """batch command frame reaches FakeUnityServer (not swallowed by bridge)."""
    wire_server.set_response("batch", data="[0] ok")
    await wire_bridge.send("batch", {"commands": "create_object name=X"})
    assert wire_server.peer.count("batch") == 1
