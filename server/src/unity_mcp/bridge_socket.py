import asyncio
import contextlib
import socket
import struct
import sys

_TCP_KEEPALIVE_DARWIN = 0x10
_TCP_KEEPINTVL_DARWIN = 0x101
_TCP_KEEPCNT_DARWIN   = 0x102


class DomainReloadError(ConnectionError):
    """Unity signaled domain reload via going_away frame."""


def _apply_socket_options(sock) -> None:
    setsockopt = getattr(sock, "setsockopt", None)
    if not callable(setsockopt):
        return

    def _try(level, opt, val):
        with contextlib.suppress(OSError):
            setsockopt(level, opt, val)

    _try(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    _try(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
    # Relaxed keepalive: idle=60s, interval=10s, count=3 (~90s dead peer detect).
    # Previous 10s/5s/3 was too aggressive for macOS App Nap timer coalescing.
    # App-level heartbeat (15s) handles faster liveness checks.
    if sys.platform == "darwin":
        _try(socket.IPPROTO_TCP, _TCP_KEEPALIVE_DARWIN, 60)
        _try(socket.IPPROTO_TCP, _TCP_KEEPINTVL_DARWIN, 10)
        _try(socket.IPPROTO_TCP, _TCP_KEEPCNT_DARWIN, 3)
    elif sys.platform.startswith("linux"):
        _try(socket.IPPROTO_TCP, socket.TCP_KEEPIDLE, 60)
        _try(socket.IPPROTO_TCP, socket.TCP_KEEPINTVL, 10)
        _try(socket.IPPROTO_TCP, socket.TCP_KEEPCNT, 3)
    elif sys.platform == "win32":
        # SIO_KEEPALIVE_VALS: (onoff=1, keepalivetime_ms=60000, keepaliveinterval_ms=10000)
        # Best-effort — app-level heartbeat (15s) handles faster liveness checks.
        import struct
        with contextlib.suppress(Exception):
            sock.ioctl(
                socket.SIO_KEEPALIVE_VALS,
                struct.pack("III", 1, 60_000, 10_000),
            )


# ── TCP framing helpers ───────────────────────────────────────────────────────

def frame_write(writer: asyncio.StreamWriter, payload: bytes) -> None:
    """Write a 4-byte big-endian length prefix followed by payload."""
    writer.write(struct.pack("!I", len(payload)) + payload)


async def frame_read(reader: asyncio.StreamReader) -> bytes:
    """Read a framed message: 4-byte length prefix then payload."""
    header = await reader.readexactly(4)
    length = struct.unpack("!I", header)[0]
    return await reader.readexactly(length)


async def frame_read_with_timeout(reader: asyncio.StreamReader, timeout: float) -> bytes:
    """Read a framed message with per-read timeouts."""
    header = await asyncio.wait_for(reader.readexactly(4), timeout=timeout)
    length = struct.unpack("!I", header)[0]
    return await asyncio.wait_for(reader.readexactly(length), timeout=timeout)
