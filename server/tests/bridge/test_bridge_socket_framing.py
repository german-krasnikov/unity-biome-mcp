"""TCP socket framing unit tests — bridge_socket.py layer.

Tests 1-2: frame_write wire format
Tests 3-6: frame_read normal / zero / IncompleteReadError propagation
Test  7:   frame_write + frame_read round-trip with real asyncio.StreamReader
Test  8:   frame_read_with_timeout fires on hung read
Test  9:   _read_response going_away frame → DomainReloadError
Test 10:   _raw_ping ID mismatch → ProtocolDesyncError
"""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, Mock

import pytest

from unity_mcp.bridge_socket import (
    DomainReloadError,
    frame_read,
    frame_read_with_timeout,
    frame_write,
)
from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_heartbeat import ProtocolDesyncError
from helpers import make_writer


# ── helpers ───────────────────────────────────────────────────────────────────

def _make_reader(*chunks):
    """AsyncMock whose readexactly() yields chunks (or raises exceptions) in order."""
    r = AsyncMock()
    r.readexactly = AsyncMock(side_effect=list(chunks))
    return r


def _frame(payload: bytes) -> tuple[bytes, bytes]:
    return struct.pack("!I", len(payload)), payload


# ── frame_write ───────────────────────────────────────────────────────────────

def test_frame_write_produces_correct_wire_format():
    payload = b'{"id":"0001","cmd":"ping","args":{}}'
    writer = make_writer()
    frame_write(writer, payload)
    assert writer.write.call_count == 1
    written = writer.write.call_args[0][0]
    assert written[:4] == struct.pack("!I", len(payload))
    assert written[4:] == payload
    assert len(written) == 4 + len(payload)


def test_frame_write_empty_payload():
    writer = make_writer()
    frame_write(writer, b"")
    written = writer.write.call_args[0][0]
    assert written == b"\x00\x00\x00\x00"
    assert len(written) == 4


# ── frame_read ────────────────────────────────────────────────────────────────

async def test_frame_read_normal():
    payload = b'{"id":"0001","ok":true}'
    hdr, pay = _frame(payload)
    reader = _make_reader(hdr, pay)
    result = await frame_read(reader)
    assert result == payload
    assert reader.readexactly.call_count == 2
    reader.readexactly.assert_any_call(4)
    reader.readexactly.assert_any_call(len(payload))


async def test_frame_read_zero_length():
    """Zero-length frame is rejected by the frame size guard."""
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", 0))
    reader.feed_eof()
    with pytest.raises(ConnectionError):
        await frame_read(reader)


# ── size guard (OOM protection) ───────────────────────────────────────────────

async def test_frame_read_rejects_zero_length():
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", 0))
    reader.feed_eof()
    with pytest.raises(ConnectionError, match="Frame size"):
        await frame_read(reader)


async def test_frame_read_rejects_oversized():
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", 10_000_001))
    reader.feed_eof()
    with pytest.raises(ConnectionError, match="Frame size"):
        await frame_read(reader)


async def test_frame_read_accepts_max_valid():
    payload = b"x" * 10_000_000
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", len(payload)) + payload)
    reader.feed_eof()
    result = await frame_read(reader)
    assert result == payload


async def test_frame_read_accepts_small():
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", 5) + b"hello")
    reader.feed_eof()
    assert await frame_read(reader) == b"hello"


async def test_frame_read_with_timeout_rejects_oversized():
    reader = asyncio.StreamReader()
    reader.feed_data(struct.pack("!I", 10_000_001))
    reader.feed_eof()
    with pytest.raises(ConnectionError, match="Frame size"):
        await frame_read_with_timeout(reader, timeout=1.0)


async def test_frame_read_header_incomplete_raises():
    reader = _make_reader(asyncio.IncompleteReadError(b"\x00", 4))
    with pytest.raises(asyncio.IncompleteReadError):
        await frame_read(reader)


async def test_frame_read_payload_incomplete_raises():
    hdr = struct.pack("!I", 20)
    reader = _make_reader(hdr, asyncio.IncompleteReadError(b"", 20))
    with pytest.raises(asyncio.IncompleteReadError):
        await frame_read(reader)


# ── round-trip via real asyncio.StreamReader ──────────────────────────────────

async def test_frame_read_write_roundtrip():
    original = json.dumps(
        {"id": "0001", "cmd": "get_hierarchy", "args": {"path": "/"}}
    ).encode()

    buf = bytearray()

    class _FakeWriter:
        def write(self, data: bytes) -> None:
            buf.extend(data)

    frame_write(_FakeWriter(), original)

    reader = asyncio.StreamReader()
    reader.feed_data(bytes(buf))
    reader.feed_eof()

    result = await frame_read(reader)
    assert result == original
    assert json.loads(result) == json.loads(original)


# ── frame_read_with_timeout ───────────────────────────────────────────────────

async def test_frame_read_with_timeout_fires():
    async def _hang(n: int) -> bytes:
        await asyncio.Future()  # never resolves
        return b""  # unreachable

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=_hang)
    with pytest.raises(asyncio.TimeoutError):
        await frame_read_with_timeout(reader, timeout=0.01)


# ── _read_response going_away ─────────────────────────────────────────────────

async def test_read_response_going_away_raises_domain_reload_error():
    """_read_response on a going_away frame raises DomainReloadError with reason."""
    body = json.dumps({"ev": "going_away", "reason": "recompile"}).encode()
    hdr = struct.pack("!I", len(body))

    bridge = UnityBridge.__new__(UnityBridge)
    bridge._reader = _make_reader(hdr, body)

    with pytest.raises(DomainReloadError) as exc:
        await bridge._read_response()

    assert "recompile" in str(exc.value)
    assert str(exc.value) == "Unity domain reload: recompile"


# ── _raw_ping ProtocolDesyncError ─────────────────────────────────────────────

async def test_raw_ping_protocol_desync_error():
    """_raw_ping raises ProtocolDesyncError when response id doesn't match sent id."""
    wrong_resp = json.dumps({"id": "WRONG", "ok": True, "data": "pong"}).encode()
    hdr, pay = _frame(wrong_resp)

    bridge = UnityBridge.__new__(UnityBridge)
    bridge._lock = asyncio.Lock()
    bridge._counter = 0
    bridge._writer = make_writer()
    bridge._reader = _make_reader(hdr, pay)

    with pytest.raises(ProtocolDesyncError) as exc:
        await bridge._raw_ping(timeout=1.0)

    msg = str(exc.value)
    assert "WRONG" in msg
    assert "hb0001" in msg
    assert bridge._counter == 1
