"""Shared test helpers for monkey/stress chat relay tests (no Unity required)."""
import asyncio
import json
import socket
import struct
from unittest.mock import AsyncMock, MagicMock

import pytest

from unity_mcp.chat_relay import ChatRelay


def _find_free_port() -> int:
    """Probe-then-bind helper — carries an inherent TOCTOU window (another
    process can claim the port between this probe and a later bind on it).
    There is no in-process production caller anywhere in this repo:
    chat_relay._main() binds port 0 directly via ChatRelay.serve(0) and reads
    the OS-assigned port back off the live socket via bound_port/wait_bound(),
    closing that window (A06); the relay_server fixture below does the same.
    Kept ONLY for test coverage of this helper's own contract
    (test_free_port_finds_available) and as a documented trap: do not wire
    this into relay_server or any other in-process fixture — that reopens the
    exact TOCTOU gap A05/A06 closed (guarded by
    test_relay_server_fixture_does_not_use_probe_then_bind_helper in
    test_port_allocation.py). A genuine subprocess-owned bind (an external
    binary that must be told its port before it starts, so the parent can't
    read the port back off a socket it controls) cannot use this helper
    without reopening the same gap either — the only real fix for that case
    is a self-reporting handshake, the same pattern serve()/_main() use."""
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def make_proc(pid: int = 9999, returncode=None) -> MagicMock:
    """Build a mock asyncio subprocess process."""
    p = MagicMock()
    p.pid = pid
    p.returncode = returncode
    p.stdin = MagicMock()
    p.stdin.write = MagicMock()
    p.stdin.drain = AsyncMock()
    p.stdin.close = MagicMock()
    p.stdout = MagicMock()
    p.stdout.readline = AsyncMock(return_value=b"")
    p.terminate = MagicMock()
    p.kill = MagicMock()
    p.wait = AsyncMock()
    return p


def mock_sess(pid: int = 1234, alive: bool = True, exit_code=None) -> MagicMock:
    """Build a mock CliSession."""
    s = MagicMock()
    s.alive = alive
    s.pid = pid
    s.exit_code = exit_code
    s.kill = AsyncMock()
    s.write_line = AsyncMock()
    s.read_stdout_line = AsyncMock(return_value=None)
    s._binary = "/bin/cli"
    s._proc = MagicMock()
    s._proc.stdin = MagicMock()
    s._proc.stdin.close = MagicMock()
    return s


def fresh_relay() -> ChatRelay:
    """New relay instance with no side effects."""
    return ChatRelay()


async def tcp_cmd(port: int, cmd: str, args: dict = None, rid: str = "1") -> dict:
    """Send one framed JSON command to a ChatRelay server and return parsed response."""
    r, w = await asyncio.wait_for(
        asyncio.open_connection("127.0.0.1", port), timeout=5
    )
    req = json.dumps({"id": rid, "cmd": cmd, "args": args or {}}).encode()
    w.write(struct.pack("!I", len(req)) + req)
    await w.drain()
    hdr = await asyncio.wait_for(r.readexactly(4), timeout=5)
    body = await asyncio.wait_for(
        r.readexactly(struct.unpack("!I", hdr)[0]), timeout=5
    )
    w.close()
    await w.wait_closed()
    return json.loads(body)


@pytest.fixture
async def relay_server():
    """ChatRelay TCP server on free port, no ppid watchdog."""
    relay = ChatRelay()
    server = await asyncio.start_server(relay._handle_client, "127.0.0.1", 0)
    port = server.sockets[0].getsockname()[1]
    yield relay, port
    server.close()
    await server.wait_closed()
    await relay._kill_current()
