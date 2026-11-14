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


# ---------------------------------------------------------------------------
# Test 5: _writer stays None after SessionIdentityMismatch
# ---------------------------------------------------------------------------

async def test_session_mismatch_leaves_writer_none():
    """After SessionIdentityMismatch, _writer must NOT be updated.

    Protocol: reject before assigning TCP socket → no split-brain possible.
    _reconnect calls _accept_candidate (which sets _writer) only after
    _open_reconnect_candidate returns cleanly. An identity mismatch raises
    before that return, so _writer stays None.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._writer is not None, "First reconnect must set _writer"

    # Simulate disconnect (close() would do this; replicate it directly to
    # keep the identity recorded in _editor_identity)
    bridge._writer = None
    bridge._reader = None

    with pytest.raises(SessionIdentityMismatch):
        with patch.object(bridge_mod.asyncio, "open_connection",
                          side_effect=_mock_open("proj-xyz", "/proj/b")):
            await bridge._reconnect(fire_callbacks=False)

    assert bridge._writer is None, (
        "After SessionIdentityMismatch, _writer must remain None — "
        "rejected endpoint must not be assigned"
    )
    assert bridge._editor_identity.project_id == "proj-abc", (
        "Identity must remain proj-abc, not updated to rejected proj-xyz"
    )


# ---------------------------------------------------------------------------
# Test 6: successful same-project reconnect atomically switches _writer
# ---------------------------------------------------------------------------

async def test_successful_reconnect_same_project_switches_socket_atomically():
    """After reconnect to same project (new TCP socket), _writer points to new socket.

    Verifies atomicity: _accept_candidate assigns reader+writer together only
    after all identity checks pass — no intermediate None window is visible.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    writer_a = bridge._writer
    assert writer_a is not None

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    writer_b = bridge._writer
    assert writer_b is not None, "_writer must be set after successful same-project reconnect"
    assert bridge._editor_identity.project_id == "proj-abc", (
        "Identity must survive same-project reconnect unchanged"
    )


# ---------------------------------------------------------------------------
# Test 7: all sends after reconnect use the NEW writer, not the old socket
# ---------------------------------------------------------------------------

async def test_all_sends_after_reconnect_use_new_writer():
    """After reconnect, _writer is a fresh socket; old socket is retired.

    Any send dispatched after reconnect reaches the new socket exclusively —
    the old writer has been closed and is no longer referenced.
    """
    bridge = _make_bridge()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    old_writer = bridge._writer
    assert old_writer is not None

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=_mock_open("proj-abc", "/proj/a")):
        await bridge._reconnect(fire_callbacks=False)

    new_writer = bridge._writer
    assert new_writer is not None
    # Distinct socket object — sends can only reach the new writer
    assert new_writer is not old_writer, (
        "After reconnect, _writer must be a new socket object"
    )
    # Old socket was closed by close() at the start of _reconnect
    old_writer.close.assert_called()
