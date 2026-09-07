"""CounterProxy: lost-ACK adapter for T3 slice 3 (a non-idempotent owned
counter, not a repeated `SET x=1`). Adapted from the proven
server/tests/mutation/_proxy.py::LostAckProxy (forward one request to
upstream, drop the matching reply, close both sides) -- here targeting the
`execute_code` counter-increment command instead of `sync`. See
Plans/N3-T3-T4-live-reload-identity.md Part 1.
"""
from __future__ import annotations

import asyncio
import contextlib
import json

from fault_proxy import read_frame, write_frame


class CounterProxy:
    """Forwards every request to upstream A. The first `execute_code` on a
    connection is forwarded and its actual effect really executes on A --
    then its matching reply is dropped (connection closed instead of
    written back): the fault lands AFTER the real increment, BEFORE the
    ACK. Any other command, and any later execute_code, is forwarded and
    answered normally."""

    def __init__(self, upstream_host: str, upstream_port: int) -> None:
        self.upstream_host = upstream_host
        self.upstream_port = upstream_port
        self.forwarded = 0
        self.dropped_ack = 0
        self.commands: list[str] = []

    async def handle_client(self, client_r: asyncio.StreamReader, client_w: asyncio.StreamWriter) -> None:
        up_r, up_w = await asyncio.open_connection(self.upstream_host, self.upstream_port)
        try:
            with contextlib.suppress(asyncio.IncompleteReadError, ConnectionError):
                while True:
                    request = await read_frame(client_r)
                    cmd = json.loads(request).get("cmd")
                    self.commands.append(cmd)
                    write_frame(up_w, request)
                    await up_w.drain()
                    response = await read_frame(up_r)
                    if cmd == "execute_code" and self.forwarded == 0:
                        self.forwarded += 1
                        self.dropped_ack += 1
                        return  # drop the matching ACK: close both sides
                    write_frame(client_w, response)
                    await client_w.drain()
        finally:
            up_w.close()
            client_w.close()
