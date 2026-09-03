"""Shared test helpers for monkey/stress chat relay tests (no Unity required)."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, MagicMock

import pytest

from unity_mcp.chat_relay import ChatRelay


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
