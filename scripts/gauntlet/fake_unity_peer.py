"""Independent scripted Unity TCP peer for public stdio contract tests."""

from __future__ import annotations

import asyncio
import json
import struct
from contextlib import suppress
from dataclasses import dataclass, field
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

_MAX_FRAME_BYTES = 10_000_000


@dataclass(slots=True)
class PeerReply:
    ok: bool = True
    data: str = "ok"
    error: str = ""


@dataclass(slots=True)
class ScriptedUnityPeer:
    project_path: Path
    plugin_version: str = "1.26.0"
    reported_project_path: Path | None = None
    transcript: list[dict[str, object]] = field(default_factory=list)
    unexpected_commands: list[str] = field(default_factory=list)
    _responses: dict[str, PeerReply] = field(default_factory=dict, repr=False)
    _server: asyncio.AbstractServer | None = field(default=None, repr=False)
    _writers: set[asyncio.StreamWriter] = field(default_factory=set, repr=False)

    @property
    def port(self) -> int:
        if self._server is None or not self._server.sockets:
            raise RuntimeError("scripted peer is not running")
        return int(self._server.sockets[0].getsockname()[1])

    @property
    def active_connections(self) -> int:
        return len(self._writers)

    async def start(self) -> None:
        if self._server is not None:
            raise RuntimeError("scripted peer is already running")
        self._server = await asyncio.start_server(self._handle, "127.0.0.1", 0)

    async def close(self) -> None:
        if self._server is not None:
            self._server.close()
            await self._server.wait_closed()
            self._server = None
        writers = tuple(self._writers)
        for writer in writers:
            writer.close()
        for writer in writers:
            with suppress(ConnectionError, OSError):
                await writer.wait_closed()
        self._writers.clear()

    def set_response(
        self,
        command: str,
        *,
        ok: bool,
        data: str = "",
        error: str = "",
    ) -> None:
        self._responses[command] = PeerReply(ok=ok, data=data, error=error)

    def count(self, command: str) -> int:
        return sum(request.get("cmd") == command for request in self.transcript)

    async def _handle(
        self,
        reader: asyncio.StreamReader,
        writer: asyncio.StreamWriter,
    ) -> None:
        self._writers.add(writer)
        try:
            while True:
                request = await _read_request(reader)
                self.transcript.append(request)
                reply = self._reply(request)
                await _write_reply(writer, request.get("id"), reply)
        except asyncio.IncompleteReadError:
            pass
        finally:
            self._writers.discard(writer)
            writer.close()
            with suppress(ConnectionError, OSError):
                await writer.wait_closed()

    def _reply(self, request: dict[str, object]) -> PeerReply:
        command = request.get("cmd")
        arguments = request.get("args")
        if (
            command == "editor"
            and isinstance(arguments, dict)
            and arguments.get("action") == "project_path"
        ):
            reported = self.reported_project_path or self.project_path
            return PeerReply(data=str(reported.resolve()))
        if isinstance(command, str) and command in self._responses:
            return self._responses[command]

        defaults = {
            "get_disabled_tools": "",
            "get_aliases": "",
            "get_capabilities": "mutating_cmds:\nruntime_cmds:",
            "set_tool_catalog": "catalog accepted",
            "search_context": "",
            "set_client_label": "label accepted",
            "ping": "pong",
            "get_version": (
                f"proto:3|plugin:{self.plugin_version}|stamp:fake-epoch"
            ),
            "get_hierarchy": "Scene: Synthetic\n/Main Camera",
            "get_status": (
                f"scene=Synthetic\ndirty=false\nplaying=false\ncompiling=false\n"
                f"port={self.port}\naliases=0"
            ),
        }
        if isinstance(command, str) and command in defaults:
            return PeerReply(data=defaults[command])
        label = str(command)
        self.unexpected_commands.append(label)
        return PeerReply(ok=False, error=f"unexpected scripted command: {label}")


async def _read_request(reader: asyncio.StreamReader) -> dict[str, object]:
    header = await reader.readexactly(4)
    length = struct.unpack("!I", header)[0]
    if length == 0 or length > _MAX_FRAME_BYTES:
        raise ConnectionError(f"invalid scripted frame length: {length}")
    payload = await reader.readexactly(length)
    value = json.loads(payload.decode("utf-8"))
    if not isinstance(value, dict):
        raise ConnectionError("scripted request must be a JSON object")
    return value


async def _write_reply(
    writer: asyncio.StreamWriter,
    request_id: object,
    reply: PeerReply,
) -> None:
    value: dict[str, object] = {"id": request_id, "ok": reply.ok}
    if reply.ok:
        value["data"] = reply.data
    else:
        value["err"] = reply.error
    payload = json.dumps(value, separators=(",", ":")).encode("utf-8")
    writer.write(struct.pack("!I", len(payload)) + payload)
    await writer.drain()
