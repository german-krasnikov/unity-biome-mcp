"""Tests for TCP framing helpers in bridge_socket."""
import asyncio
import struct
import pytest
from unity_mcp.bridge_socket import frame_read, frame_write, frame_read_with_timeout


class FakeWriter:
    def __init__(self):
        self.data = b""

    def write(self, data: bytes) -> None:
        self.data += data

    async def drain(self) -> None:
        pass


def _make_reader(payload: bytes) -> asyncio.StreamReader:
    r = asyncio.StreamReader()
    r.feed_data(struct.pack("!I", len(payload)) + payload)
    return r


@pytest.mark.asyncio
async def test_frame_write_produces_length_prefix():
    payload = b'{"cmd":"ping"}'
    writer = FakeWriter()
    frame_write(writer, payload)
    assert writer.data == struct.pack("!I", len(payload)) + payload


@pytest.mark.asyncio
async def test_frame_read_round_trip():
    payload = b'{"cmd":"ping","args":{}}'
    reader = _make_reader(payload)
    assert await frame_read(reader) == payload


@pytest.mark.asyncio
async def test_frame_read_with_timeout_returns_payload():
    payload = b'{"ok":true}'
    reader = _make_reader(payload)
    assert await frame_read_with_timeout(reader, timeout=1.0) == payload


@pytest.mark.asyncio
async def test_frame_read_with_timeout_raises_on_empty():
    reader = asyncio.StreamReader()
    reader.feed_eof()
    with pytest.raises((asyncio.TimeoutError, asyncio.IncompleteReadError)):
        await frame_read_with_timeout(reader, timeout=0.01)
