"""Minimal one-sync/drop-reply proxy for the S8 lost-ACK regression test.

Forwards every frame to Unity untouched except: the first `sync` request is
forwarded but its matching reply is dropped (connection closed instead of
written back); any later `sync` or `force_refresh` is rejected before it
reaches Unity. See Plans/MUTATION-REGRESSION-MODULE.md matrix row S8.
"""
import asyncio
import contextlib
import json

from fault_proxy import read_frame, write_frame


class LostAckProxy:
    def __init__(self, upstream_host: str, upstream_port: int) -> None:
        self.upstream_host = upstream_host
        self.upstream_port = upstream_port
        self.forwarded_sync = 0
        self.rejected: list[str] = []
        self.connections = 0
        self.commands: list[str] = []  # every cmd seen, across all connections

    async def handle_client(self, client_r: asyncio.StreamReader, client_w: asyncio.StreamWriter) -> None:
        self.connections += 1
        up_r, up_w = await asyncio.open_connection(self.upstream_host, self.upstream_port)
        try:
            with contextlib.suppress(asyncio.IncompleteReadError, ConnectionError):
                while True:
                    request = await read_frame(client_r)
                    cmd = json.loads(request).get("cmd")
                    self.commands.append(cmd)
                    if cmd == "force_refresh" or (cmd == "sync" and self.forwarded_sync):
                        self.rejected.append(cmd)
                        return
                    write_frame(up_w, request)
                    await up_w.drain()
                    response = await read_frame(up_r)
                    if cmd == "sync":
                        self.forwarded_sync += 1
                        return  # drop the matching ACK: close both sides
                    write_frame(client_w, response)
                    await client_w.drain()
        finally:
            up_w.close()
            client_w.close()
