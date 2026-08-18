"""FakeUnityServer: in-process TCP server speaking the 4-byte-prefix JSON protocol."""

import asyncio
import json
import struct
from contextlib import suppress
from pathlib import Path

from gauntlet.fake_unity_peer import (
    ScriptedUnityPeer,
    _read_request,
    _write_reply,
)


class FakeUnityServer:
    """Thin wrapper around ScriptedUnityPeer that owns its own asyncio server."""

    def __init__(self, project_path: Path = Path("fake-project")) -> None:
        self._peer = ScriptedUnityPeer(project_path=project_path)
        self._server: asyncio.AbstractServer | None = None
        self._writers: set[asyncio.StreamWriter] = set()
        self._pending_going_away = False

    async def start(self) -> None:
        self._server = await asyncio.start_server(self._handle, "127.0.0.1", 0)

    async def close(self) -> None:
        if self._server is not None:
            self._server.close()
            await self._server.wait_closed()
            self._server = None
        writers = tuple(self._writers)
        for w in writers:
            w.close()
        for w in writers:
            with suppress(ConnectionError, OSError):
                await w.wait_closed()
        self._writers.clear()

    @property
    def port(self) -> int:
        if self._server is None or not self._server.sockets:
            raise RuntimeError("FakeUnityServer is not running")
        return int(self._server.sockets[0].getsockname()[1])

    @property
    def peer(self) -> ScriptedUnityPeer:
        return self._peer

    def set_response(self, cmd: str, *, ok: bool = True, data: str = "", error: str = "") -> None:
        self._peer.set_response(cmd, ok=ok, data=data, error=error)

    def inject_going_away(self) -> None:
        """Queue a going_away frame in place of the next normal response."""
        self._pending_going_away = True

    def load_cassette(self, path: Path) -> None:
        """JSONL cassette: each non-comment line configures a scripted response."""
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            rec = json.loads(line)
            resp = rec["response"]
            self._peer.set_response(
                rec["cmd"],
                ok=resp.get("ok", True),
                data=resp.get("data", ""),
                error=resp.get("error", ""),
            )

    async def _handle(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        self._writers.add(writer)
        try:
            while True:
                request = await _read_request(reader)
                self._peer.transcript.append(request)
                if self._pending_going_away:
                    self._pending_going_away = False
                    frame = json.dumps({"ev": "going_away"}, separators=(",", ":")).encode()
                    writer.write(struct.pack("!I", len(frame)) + frame)
                    await writer.drain()
                    break
                reply = self._peer._reply(request)
                await _write_reply(writer, request.get("id"), reply)
        except asyncio.IncompleteReadError:
            pass
        finally:
            self._writers.discard(writer)
            writer.close()
            with suppress(ConnectionError, OSError):
                await writer.wait_closed()

    async def __aenter__(self) -> "FakeUnityServer":
        await self.start()
        return self

    async def __aexit__(self, *_: object) -> None:
        await self.close()
