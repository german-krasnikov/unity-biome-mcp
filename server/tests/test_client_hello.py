"""TDD tests for client_hello handshake (T5).

Tests cover:
- _build_hello payload fields and role defaulting
- session_id / lock_token stability
- _open_reconnect_candidate hello-first path (new C#)
- fallback to get_version when hello returns ok:false (old C#)
- DomainReloadError on going_away response
"""
import json
import struct
from unittest.mock import AsyncMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import DomainReloadError, UnityBridge
from unity_mcp.bridge_socket import DomainReloadError  # noqa: F811
from helpers import make_idle_probe, make_writer


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _frame(data: dict) -> tuple[bytes, bytes]:
    """Encode dict to (4-byte-header, payload) for reader.readexactly side_effect."""
    payload = json.dumps(data).encode()
    return struct.pack("!I", len(payload)), payload


def _hello_ok(version: str = "proto:3|plugin:test|stamp:abc",
              project_path: str = "/path/to/project") -> tuple[bytes, bytes]:
    return _frame({"id": "rc0001", "ok": True, "data": "pong",
                   "helloVersion": 2, "version": version, "projectPath": project_path})


def _hello_fail() -> tuple[bytes, bytes]:
    return _frame({"id": "rc0001", "ok": False, "err": "Unknown command: client_hello"})


def _going_away() -> tuple[bytes, bytes]:
    return _frame({"ev": "going_away", "reason": "domain_reload"})


def _version_ok() -> tuple[bytes, bytes]:
    return _frame({"id": "ver", "ok": True, "data": "proto:3|plugin:test|stamp:abc"})


def _pong(msg_id: str = "0001") -> tuple[bytes, bytes]:
    """Command response — id must match the msg_id built in send() (counter 0→1)."""
    return _frame({"id": msg_id, "ok": True, "data": "pong"})


def _make_open_connection(read_frames: list[tuple[bytes, bytes]], writer=None):
    """Return (mock_open_connection, writer) with readexactly side_effect from frames."""
    if writer is None:
        writer = make_writer()
    chunks = [b for hdr, pay in read_frames for b in (hdr, pay)]

    async def _open(host, port):
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=chunks)
        return reader, writer

    return _open, writer


# ---------------------------------------------------------------------------
# _build_hello field coverage
# ---------------------------------------------------------------------------

def test_build_hello_contains_required_fields(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
    raw = bridge._build_hello("test-id")
    data = json.loads(raw.decode())
    assert data["cmd"] == "client_hello"
    assert data["id"] == "test-id"
    assert data["sessionId"] == bridge.session_id
    assert data["lockToken"] == bridge.lock_token
    assert "role" in data
    assert "agentId" in data


def test_build_hello_role_from_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_CLIENT", "codex")
    bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
    data = json.loads(bridge._build_hello("id1").decode())
    assert data["role"] == "codex"


def test_build_hello_role_defaults_to_mcp(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
    data = json.loads(bridge._build_hello("id1").decode())
    assert data["role"] == "mcp"


# ---------------------------------------------------------------------------
# session_id / lock_token stability
# ---------------------------------------------------------------------------

def test_session_id_stable_across_calls():
    bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
    assert bridge.session_id == bridge.session_id
    assert len(bridge.session_id) > 0


def test_lock_token_stable_across_calls():
    bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
    assert bridge.lock_token == bridge.lock_token
    assert len(bridge.lock_token) > 0


# ---------------------------------------------------------------------------
# _open_reconnect_candidate: new C# (helloVersion present)
# ---------------------------------------------------------------------------

async def test_open_reconnect_uses_client_hello_when_ok(monkeypatch):
    """New C#: client_hello response has helloVersion → single roundtrip, no ping."""
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    mock_open, writer = _make_open_connection([
        _hello_ok(),  # handshake (1 roundtrip)
        _pong(),      # actual send("ping") response
    ])

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        await bridge.send("ping", {})

    # Only 2 writes: client_hello frame + actual ping command frame
    assert writer.write.call_count == 2
    first_write = writer.write.call_args_list[0][0][0]
    data = json.loads(first_write[4:].decode())
    assert data["cmd"] == "client_hello"


# ---------------------------------------------------------------------------
# _open_reconnect_candidate: old C# fallback (ok:false, no helloVersion)
# ---------------------------------------------------------------------------

async def test_open_reconnect_falls_back_to_ping_when_hello_fails(monkeypatch):
    """Old C#: hello returns ok:false → fallback sends get_version, then command."""
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    mock_open, writer = _make_open_connection([
        _hello_fail(),   # hello → ok:false (old C# doesn't know client_hello)
        _version_ok(),   # get_version fallback
        _pong(),         # actual send("ping") response
    ])

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        await bridge.send("ping", {})

    # 3 writes: client_hello + get_version + actual ping
    assert writer.write.call_count == 3
    first_write = writer.write.call_args_list[0][0][0]
    data = json.loads(first_write[4:].decode())
    assert data["cmd"] == "client_hello"
    second_write = writer.write.call_args_list[1][0][0]
    data2 = json.loads(second_write[4:].decode())
    assert data2["cmd"] == "get_version"


# ---------------------------------------------------------------------------
# _open_reconnect_candidate: going_away → DomainReloadError
# ---------------------------------------------------------------------------

async def test_open_reconnect_falls_back_on_going_away(monkeypatch):
    """going_away in hello response → DomainReloadError raised."""
    monkeypatch.delenv("UNITY_MCP_CLIENT", raising=False)
    mock_open, writer = _make_open_connection([_going_away()])

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())
        with pytest.raises((DomainReloadError, ConnectionError)):
            await bridge.send("ping", {})
