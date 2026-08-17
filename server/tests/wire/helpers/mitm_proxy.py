"""MITM proxy for wire-level fault injection tests."""
from __future__ import annotations

import asyncio
import json
import struct
from collections.abc import Callable
from contextlib import suppress
from typing import Any

# Sentinel: proxy sends a corrupt 4-byte header (0xFFFFFFFF) instead of a valid frame.
CORRUPT_FRAME = object()

Transform = Callable[[dict], Any]


# Built-in transforms ──────────────────────────────────────────────────────────

def flip_ok(response: dict) -> dict:
    """Force ok:true — simulates C# lying about success."""
    response["ok"] = True
    response.pop("err", None)
    return response


def corrupt_length(_: dict) -> object:
    """Return CORRUPT_FRAME sentinel so proxy writes an invalid length header."""
    return CORRUPT_FRAME


def blank_data(response: dict) -> dict:
    """Clear data field — catches callers that trust data without checking."""
    response["data"] = ""
    return response


def swap_id(response: dict) -> dict:
    """Replace response id — triggers bridge 'Response ID mismatch' error."""
    response["id"] = "ffff"
    return response


# MitmProxy ────────────────────────────────────────────────────────────────────

class MitmProxy:
    """TCP proxy that intercepts server→client frames and applies transforms."""

    def __init__(
        self,
        target_host: str,
        target_port: int,
        transforms: tuple[Transform, ...] | list[Transform] = (),
    ) -> None:
        self._target_host = target_host
        self._target_port = target_port
        self._transforms = list(transforms)
        self._server: asyncio.AbstractServer | None = None
        self._client_writers: set[asyncio.StreamWriter] = set()

    async def start(self) -> None:
        self._server = await asyncio.start_server(self._handle, "127.0.0.1", 0)

    async def close(self) -> None:
        if self._server is not None:
            self._server.close()
            # Force-close all active client connections so wait_closed() can return.
            writers = tuple(self._client_writers)
            for w in writers:
                with suppress(Exception):
                    w.close()
            for w in writers:
                with suppress(Exception):
                    await w.wait_closed()
            self._client_writers.clear()
            await self._server.wait_closed()
            self._server = None

    @property
    def port(self) -> int:
        if self._server is None or not self._server.sockets:
            raise RuntimeError("MitmProxy is not running")
        return int(self._server.sockets[0].getsockname()[1])

    async def _handle(
        self, client_reader: asyncio.StreamReader, client_writer: asyncio.StreamWriter
    ) -> None:
        self._client_writers.add(client_writer)
        try:
            target_reader, target_writer = await asyncio.open_connection(
                self._target_host, self._target_port
            )
        except OSError:
            client_writer.close()
            self._client_writers.discard(client_writer)
            return

        async def forward_requests() -> None:
            """Forward client→target: raw bytes, untransformed."""
            try:
                while True:
                    header = await client_reader.readexactly(4)
                    length = struct.unpack("!I", header)[0]
                    payload = await client_reader.readexactly(length)
                    target_writer.write(header + payload)
                    await target_writer.drain()
            except (asyncio.IncompleteReadError, ConnectionError, OSError):
                pass
            finally:
                with suppress(Exception):
                    target_writer.close()

        async def forward_responses() -> None:
            """Forward target→client: deserialize, apply transforms, re-serialize."""
            try:
                while True:
                    header = await target_reader.readexactly(4)
                    length = struct.unpack("!I", header)[0]
                    payload = await target_reader.readexactly(length)
                    response = json.loads(payload.decode("utf-8"))

                    result: Any = response
                    for transform in self._transforms:
                        result = transform(result)
                        if result is CORRUPT_FRAME:
                            break

                    if result is CORRUPT_FRAME:
                        # Write invalid length header; bridge raises ConnectionError.
                        client_writer.write(struct.pack("!I", 0xFFFFFFFF))
                        with suppress(Exception):
                            await client_writer.drain()
                        client_writer.close()
                        return

                    out = json.dumps(result, separators=(",", ":")).encode("utf-8")
                    client_writer.write(struct.pack("!I", len(out)) + out)
                    await client_writer.drain()
            except (asyncio.IncompleteReadError, ConnectionError, OSError):
                pass

        await asyncio.gather(forward_requests(), forward_responses(), return_exceptions=True)
        self._client_writers.discard(client_writer)
        with suppress(Exception):
            client_writer.close()

    async def __aenter__(self) -> "MitmProxy":
        await self.start()
        return self

    async def __aexit__(self, *_: object) -> None:
        await self.close()
