"""Tests for UNITY_MCP_CLIENT env var role injection in ping frame."""
import json
import struct
from unittest.mock import AsyncMock, Mock, patch

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, reconnect_preamble


def _ok_response(msg_id="0001"):
    r = {"id": msg_id, "ok": True, "data": "ok"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def _role_from_writer(writer) -> str:
    """Extract 'role' from the first write call (ping frame, bytes[4:] is JSON)."""
    first_write = writer.write.call_args_list[0][0][0]
    return json.loads(first_write[4:].decode())["role"]


def _make_open_connection(writer=None):
    """Async mock open_connection: preamble + one ok command response."""
    if writer is None:
        writer = make_writer()
    hdr, pay = _ok_response()

    async def mock_open(host, port):
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), hdr, pay])
        return reader, writer

    return mock_open, writer


async def test_bridge_role_default(monkeypatch):
    """No env vars → role is 'mcp'."""
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    monkeypatch.delenv("UNITY_MCP_CHAT", raising=False)
    mock_open, writer = _make_open_connection()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        await bridge.send("ping", {})

    assert _role_from_writer(writer) == "mcp"


async def test_bridge_role_env_var(monkeypatch):
    """UNITY_MCP_CLIENT=codex → role is 'codex'."""
    monkeypatch.setenv("UNITY_MCP_CLIENT", "codex")
    monkeypatch.delenv("UNITY_MCP_CHAT", raising=False)
    mock_open, writer = _make_open_connection()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        await bridge.send("ping", {})

    assert _role_from_writer(writer) == "codex"


async def test_bridge_role_chat_relay_takes_precedence(monkeypatch):
    """UNITY_MCP_CHAT=1 wins over UNITY_MCP_CLIENT=codex."""
    monkeypatch.setenv("UNITY_MCP_CHAT", "1")
    monkeypatch.setenv("UNITY_MCP_CLIENT", "codex")
    mock_open, writer = _make_open_connection()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        await bridge.send("ping", {})

    assert _role_from_writer(writer) == "chat-relay"
