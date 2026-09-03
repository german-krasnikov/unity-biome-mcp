"""DRY guard: connect() and _open_reconnect_candidate() share one client_hello
response dispatcher (C1 round1 #9) instead of each carrying its own inline
going_away/capacity/identity-capture handling.
"""
import json
import struct
from unittest.mock import AsyncMock, patch

from unity_mcp.bridge import PROTOCOL_VERSION, UnityBridge
from helpers import make_writer, reconnect_preamble


def _hello_ok_frame() -> list[bytes]:
    """A modern (helloVersion-carrying) client_hello response, framed."""
    payload = json.dumps({
        "id": "rc0001", "ok": True, "helloVersion": 2,
        "projectId": "abc123", "projectPath": "/some/project",
        "version": f"proto:{PROTOCOL_VERSION}|plugin:test|stamp:x",
    }).encode()
    return [struct.pack("!I", len(payload)), payload]


async def test_hello_response_dispatch_shared_between_connect_and_reconnect(monkeypatch):
    """Both connect() and _open_reconnect_candidate() must delegate hello-
    response handling (going_away/capacity/identity+version) to the same
    private dispatcher. If either call site is reverted to its own inline
    dispatch instead of calling the shared helper, that path never reaches
    the spy below and this test goes red — proving the two sides share one
    implementation rather than duplicating it.
    """
    seen_contexts: list[str] = []

    async def spy(self, hello, *, reload_context):
        seen_contexts.append(reload_context)
        return False  # force each caller into its own legacy fallback

    monkeypatch.setattr(UnityBridge, "_dispatch_client_hello_response", spy)

    # -- connect() path --
    reader1 = AsyncMock()
    writer1 = make_writer()
    reader1.readexactly = AsyncMock(side_effect=[*reconnect_preamble(proto=PROTOCOL_VERSION)])
    with patch("asyncio.open_connection", return_value=(reader1, writer1)):
        bridge1 = UnityBridge("127.0.0.1", 9999, expected_project_path="/some/project")
        with patch.object(bridge1, "_verify_candidate_project", new=AsyncMock()):
            await bridge1.connect()
    assert "initial connect" in seen_contexts
    assert bridge1.connected

    # -- reconnect candidate path --
    seen_contexts.clear()
    reader2 = AsyncMock()
    writer2 = make_writer()
    version_frames = reconnect_preamble(proto=PROTOCOL_VERSION)[2:]  # ver_hdr, ver_payload
    reader2.readexactly = AsyncMock(side_effect=[*_hello_ok_frame(), *version_frames])
    with patch("asyncio.open_connection", return_value=(reader2, writer2)):
        bridge2 = UnityBridge("127.0.0.1", 9999)
        with patch.object(bridge2, "_verify_candidate_project", new=AsyncMock()):
            await bridge2._open_reconnect_candidate(9999)
    assert "reconnect" in seen_contexts
