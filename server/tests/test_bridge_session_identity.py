"""MCP-SESS-024: Bridge-level session identity enforcement prevents split-brain.

Tests FIRST — all must fail until implementation is added.
"""
import json
import struct
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from unity_mcp.errors import SessionIdentityMismatch
from helpers import make_writer


def _hello_payload(project_id: str, project_path: str, msg_id: str = "rc0001") -> tuple[bytes, bytes]:
    """Build a new-format client_hello response frame (helloVersion: 2)."""
    resp = {
        "id": msg_id,
        "ok": True,
        "data": "pong",
        "helloVersion": 2,
        "version": "proto:3|plugin:test|stamp:test",
        "projectPath": project_path,
        "projectId": project_id,
    }
    payload = json.dumps(resp).encode()
    return struct.pack("!I", len(payload)), payload


def _mock_open(project_id: str, project_path: str):
    """Return a coroutine factory mimicking asyncio.open_connection."""
    async def _open(host, port):
        hdr, pay = _hello_payload(project_id, project_path)
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[hdr, pay])
        return reader, make_writer()
    return _open


def _make_bridge() -> UnityBridge:
    b = UnityBridge(port=9999)
    b.start_heartbeat = MagicMock()  # prevent background task in tests
    return b


# ---------------------------------------------------------------------------
# Test 1: identity captured on first reconnect
# ---------------------------------------------------------------------------

async def test_identity_captured_on_first_connect():
    """After first successful _reconnect, _editor_identity is populated.

    Red: UnityBridge has no _editor_identity attribute yet.
    """
    bridge = _make_bridge()
    assert bridge._editor_identity is None

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._editor_identity is not None
    assert bridge._editor_identity.project_id == "proj-abc"


# ---------------------------------------------------------------------------
# Test 2: reconnect to same identity succeeds
# ---------------------------------------------------------------------------

async def test_reconnect_same_identity_succeeds():
    """Reconnect to same Unity editor (same project_id) does not raise.

    Red: no enforcement exists yet.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)  # must not raise


# ---------------------------------------------------------------------------
# Test 3: reconnect to different project_id raises SessionIdentityMismatch
# ---------------------------------------------------------------------------

async def test_reconnect_different_project_fails_closed():
    """Reconnect to different project_id raises SessionIdentityMismatch (fail closed).

    Red: SessionIdentityMismatch doesn't exist yet and no enforcement exists.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    with pytest.raises(SessionIdentityMismatch):
        with patch.object(bridge_mod.asyncio, "open_connection",
                          side_effect=_mock_open("proj-xyz", "/proj/b")):
            await bridge._reconnect(fire_callbacks=False)


# ---------------------------------------------------------------------------
# Test 4: reconnect to different project_path (no project_id) raises mismatch
# ---------------------------------------------------------------------------

async def test_reconnect_different_project_path_fails_closed():
    """Fallback to path comparison when project_id is absent; raises on mismatch.

    Red: no path-based enforcement exists yet.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    with pytest.raises(SessionIdentityMismatch):
        with patch.object(bridge_mod.asyncio, "open_connection",
                          side_effect=_mock_open("", "/proj/b")):
            await bridge._reconnect(fire_callbacks=False)
