"""Integration tests for Unity MCP TCP communication.

Uses a REAL async TCP server to test bridge communication.
"""
import asyncio
import json
import struct
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.bridge import UnityBridge


async def test_full_roundtrip_send_receive(mock_unity_server):
    """Send command to real TCP server and receive response."""
    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    response = await bridge.send("get_hierarchy", {})

    assert response["ok"] is True
    assert response["data"] == "echo:get_hierarchy"

    await bridge.close()


async def test_multiple_commands_sequential(mock_unity_server):
    """Send 3 commands in sequence, all return correct responses."""
    mock_unity_server.set_response("cmd1", "result1")
    mock_unity_server.set_response("cmd2", "result2")
    mock_unity_server.set_response("cmd3", "result3")

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    r1 = await bridge.send("cmd1", {})
    r2 = await bridge.send("cmd2", {})
    r3 = await bridge.send("cmd3", {})

    assert r1["data"] == "result1"
    assert r2["data"] == "result2"
    assert r3["data"] == "result3"

    await bridge.close()


async def test_server_returns_error(mock_unity_server):
    """Server returns error response, bridge returns it correctly."""
    responses = {}

    async def error_handler(reader, writer):
        try:
            header = await reader.readexactly(4)
            length = struct.unpack("!I", header)[0]
            payload = await reader.readexactly(length)
            request = json.loads(payload.decode("utf-8"))

            msg_id = request["id"]
            response = {"id": msg_id, "ok": False, "err": "GameObject not found"}

            resp_payload = json.dumps(response).encode("utf-8")
            resp_header = struct.pack("!I", len(resp_payload))
            writer.write(resp_header + resp_payload)
            await writer.drain()
        except asyncio.IncompleteReadError:
            pass
        writer.close()

    server = await asyncio.start_server(error_handler, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]

    bridge = UnityBridge(port=port)
    await bridge.connect()

    response = await bridge.send("get_object", {"path": "Invalid/Path"})

    assert response["ok"] is False
    assert response["err"] == "GameObject not found"

    await bridge.close()
    server.close()
    await server.wait_closed()


async def test_large_payload(mock_unity_server):
    """Send large args dict, verify received correctly."""
    large_data = {f"key_{i}": f"value_{i}" * 100 for i in range(100)}
    mock_unity_server.set_response("large_cmd", large_data)

    bridge = UnityBridge(port=mock_unity_server.port)
    await bridge.connect()

    response = await bridge.send("large_cmd", {"test": "data"})

    assert response["ok"] is True
    assert response["data"] == large_data

    await bridge.close()


# ─── write-tool ok=False → ToolError ─────────────────────────────────────────

async def test_set_active_error_raises_tool_error_integration(mock_bridge):
    """set_active raises ToolError when Unity returns ok=False."""
    from unity_mcp.tools.objects import set_active
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    with pytest.raises(ToolError, match="Object not found"):
        await set_active("/Missing", True)


async def test_wire_event_error_raises_tool_error_integration(mock_bridge):
    """wire_event raises ToolError when Unity returns ok=False."""
    from unity_mcp.tools.objects import wire_event
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Event field not found"})
    with pytest.raises(ToolError, match="Event field not found"):
        await wire_event("/Btn", "Button", "onClick", "/Target", "SetActive")


async def test_unwire_event_error_raises_tool_error_integration(mock_bridge):
    """unwire_event raises ToolError when Unity returns ok=False."""
    from unity_mcp.tools.objects import unwire_event
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No listeners found"})
    with pytest.raises(ToolError, match="No listeners found"):
        await unwire_event("/Btn", "Button", "onClick")


async def test_set_material_error_raises_tool_error_integration(mock_bridge):
    """set_material raises ToolError when Unity returns ok=False."""
    from unity_mcp.tools.objects import set_material
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Renderer not found"})
    with pytest.raises(ToolError, match="Renderer not found"):
        await set_material("/Missing", "#FF0000")
