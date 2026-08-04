"""Standalone CLI sidecar. Spawned by Unity, survives domain reload.

Entry: python -m unity_mcp.chat_relay
Protocol: 4-byte BE length prefix + JSON, same as MCP bridge.
"""
import asyncio
import json
import os
import signal
import struct
import tempfile

from .backend_def import (
    BACKENDS,
    OUTPUT_FORMAT_CODEX_JSON,
    OUTPUT_FORMAT_KIMI_JSON,
    OUTPUT_FORMAT_OPENCODE_JSON,
    OUTPUT_FORMAT_PLAIN_TEXT,
    OUTPUT_FORMAT_STREAM_JSON,
)
from .bridge_socket import frame_write
from .cli_session import (  # noqa: F401
    KILL_WAIT,
    MAX_FRAME,
    PPID_POLL,
    BufLine,
    CliSession,
    SessionMeta,
    _find_free_port,
)
from .relay_buffer import MAX_BUF, RelayBuffer  # noqa: F401
from .stream_transform import (
    _ToolCallAcc,
    _transform_codex_line,
    _transform_kimi_line,
    _transform_line,
    _transform_opencode_line,
    _transform_plain_text_line,
)

_TRANSFORM_FNS = {
    OUTPUT_FORMAT_STREAM_JSON:   _transform_line,
    OUTPUT_FORMAT_PLAIN_TEXT:    _transform_plain_text_line,
    OUTPUT_FORMAT_CODEX_JSON:    _transform_codex_line,
    OUTPUT_FORMAT_OPENCODE_JSON: _transform_opencode_line,
    OUTPUT_FORMAT_KIMI_JSON:     _transform_kimi_line,
}


class ChatRelay:
    """TCP server: single Unity client, one CLI session, reconnect-safe buffer."""

    def __init__(self):
        self._session:        CliSession | None          = None
        self._session_meta:   SessionMeta | None         = None
        self._relay_buf:      RelayBuffer                = RelayBuffer()
        self._current_writer: asyncio.StreamWriter | None = None  # B4: single-client guard
        self._orig_ppid:      int                        = os.getppid()
        self._drain_task:     asyncio.Task | None        = None  # M3: prevent GC
        self._watchdog_task:  asyncio.Task | None        = None  # M3: prevent GC
        self._transform_fn                               = _transform_line  # default: stream-json

    # ── Public TCP server ────────────────────────────────────────────────

    async def serve(self, port: int) -> None:
        server = await asyncio.start_server(
            self._handle_client, "127.0.0.1", port)
        self._watchdog_task = asyncio.create_task(self._ppid_watchdog())
        async with server:
            await server.serve_forever()

    # ── Client handler ───────────────────────────────────────────────────

    async def _handle_client(self, reader: asyncio.StreamReader,
                             writer: asyncio.StreamWriter) -> None:
        # B4: displace previous client (relay is single-client by design)
        if self._current_writer is not None:
            try:
                self._current_writer.close()
            except Exception:
                pass
        self._current_writer = writer
        try:
            while True:
                hdr = await reader.readexactly(4)
                length = struct.unpack("!I", hdr)[0]
                if length == 0 or length > MAX_FRAME:
                    break
                payload = await reader.readexactly(length)
                req     = json.loads(payload.decode("utf-8"))
                resp    = await self._dispatch(req)
                resp["id"] = req.get("id", "?")
                out = json.dumps(resp, ensure_ascii=False).encode("utf-8")
                frame_write(writer, out)
                await writer.drain()
        except (asyncio.IncompleteReadError, ConnectionResetError, BrokenPipeError,
                json.JSONDecodeError, AttributeError):
            pass
        finally:
            if self._current_writer is writer:
                self._current_writer = None
            writer.close()

    # ── Command dispatch ─────────────────────────────────────────────────

    async def _dispatch(self, req: dict) -> dict:
        cmd  = req.get("cmd", "")
        args = req.get("args", {})
        handlers = {
            "send":        self._cmd_send,
            "events":      self._cmd_events,
            "kill":        self._cmd_kill,
            "status":      self._cmd_status,
            "close_stdin": self._cmd_close_stdin,
            "start":       self._cmd_start,
            "set_mode":    self._cmd_set_mode,
        }
        handler = handlers.get(cmd)
        if handler is None:
            return {"ok": False, "err": f"unknown cmd: {cmd}"}
        try:
            return await handler(args)
        except Exception as e:
            return {"ok": False, "err": str(e)}

    async def _cmd_spawn(self, args: dict) -> dict:
        await self._kill_current()
        session = CliSession(
            binary    = args["binary"],
            argv      = args.get("argv", []),
            env_set   = args.get("env_set", {}),
            env_strip = args.get("env_strip", []),
        )
        try:
            await session.start()
        except Exception as e:
            return {"ok": False, "err": f"spawn failed: {e}"}
        self._session    = session
        self._drain_task = asyncio.create_task(self._drain_stdout_loop())
        return {"ok": True, "data": f"spawned pid={session.pid}"}

    async def _cmd_send(self, args: dict) -> dict:
        meta = self._session_meta
        if meta:
            backend = BACKENDS.get(meta.backend)
            if backend and not backend.reads_stdin:
                # Single-turn CLI: extract plain text and respawn with actual prompt.
                prompt = _extract_text_from_turn(args.get("line") or "")
                return await self._cmd_start({
                    "backend":    meta.backend,
                    "mode":       meta.mode,
                    "model":      meta.model,
                    "mcp_port":   meta.mcp_port,
                    "prompt":     prompt,
                    "config_dir": meta.config_dir,
                    **meta.extra,
                })
        if self._session is None:
            return {"ok": False, "err": "no session"}
        try:
            await self._session.write_line(args["line"])
        except (RuntimeError, KeyError) as e:
            return {"ok": False, "err": str(e)}
        return {"ok": True, "data": "sent"}

    async def _cmd_events(self, args: dict) -> dict:
        after      = int(args.get("after_seq", -1))
        timeout_ms = int(args.get("timeout_ms", 0))
        data = await self._relay_buf.cmd_events(after, timeout_ms)
        return {"ok": True, "data": data}

    async def _cmd_kill(self, args: dict) -> dict:
        await self._kill_current()
        return {"ok": True, "data": "killed"}

    async def _cmd_status(self, args: dict) -> dict:
        tail = self._relay_buf.status_tail()
        if self._session is None:
            return {"ok": True, "data": f"no_session{tail}"}
        if self._session.alive:
            return {"ok": True, "data": f"alive|pid={self._session.pid}{tail}"}
        return {"ok": True, "data": f"dead|exit={self._session.exit_code}{tail}"}

    async def _cmd_switch(self, args: dict) -> dict:
        return await self._cmd_spawn(args)

    async def _cmd_close_stdin(self, args: dict) -> dict:
        if self._session:
            self._session.close_stdin()
        return {"ok": True, "data": "stdin closed"}

    async def _cmd_start(self, args: dict) -> dict:
        """Resolve binary, build argv, write config files, then spawn."""
        backend_name = args.get("backend", "")
        backend = BACKENDS.get(backend_name)
        if backend is None:
            return {"ok": False, "err": f"unknown backend: {backend_name}"}

        resolved = await backend.resolve_binary()
        if resolved is None:
            return {"ok": False, "err": f"binary '{backend.binary}' not found in PATH"}

        mode       = args.get("mode") or "ask"
        model      = args.get("model")
        mcp_port   = int(args.get("mcp_port") or 0)
        prompt     = args.get("prompt") or ""
        session_id = args.get("resume_session_id") or args.get("session_id")
        config_dir = args.get("config_dir") or tempfile.gettempdir()
        extra_keys = {k: v for k, v in args.items()
                      if k not in {"backend", "mode", "model", "mcp_port",
                                   "prompt", "session_id", "resume_session_id", "config_dir"}}

        try:
            argv, env_set, env_strip = backend.build_args(
                mode=mode, model=model, mcp_port=mcp_port,
                prompt=prompt, session_id=session_id,
                config_dir=config_dir, **extra_keys,
            )
        except Exception as e:
            return {"ok": False, "err": f"build_args failed: {e}"}

        # Defer spawn for single-turn backends: wait for first _cmd_send to provide the prompt.
        if not backend.reads_stdin and not prompt:
            await self._kill_current()
            self._session_meta = SessionMeta(
                backend=backend_name, mode=mode, model=model,
                mcp_port=mcp_port, prompt=prompt, config_dir=config_dir,
                extra=extra_keys,
            )
            return {"ok": True, "data": "deferred|no prompt yet"}

        await self._kill_current()
        session = CliSession(binary=resolved, argv=argv,
                             env_set=env_set, env_strip=env_strip,
                             reads_stdin=backend.reads_stdin)
        try:
            await session.start()
        except Exception as e:
            return {"ok": False, "err": f"spawn failed: {e}"}

        if not backend.reads_stdin:
            session.close_stdin()

        self._session      = session
        self._transform_fn = _TRANSFORM_FNS.get(backend.output_format, _transform_plain_text_line)
        self._session_meta = SessionMeta(
            backend=backend_name, mode=mode, model=model,
            mcp_port=mcp_port, prompt=prompt, config_dir=config_dir,
            extra=extra_keys,
        )
        self._drain_task = asyncio.create_task(self._drain_stdout_loop())
        return {"ok": True, "data": f"spawned pid={session.pid}"}

    async def _cmd_set_mode(self, args: dict) -> dict:
        """Kill current session and respawn with new mode, reusing stored meta."""
        meta = self._session_meta
        if meta is None:
            return {"ok": False, "err": "no active session"}

        backend = BACKENDS.get(meta.backend)
        if backend is None or not backend.has_resume:
            return {"ok": False, "err": f"backend {meta.backend!r} does not support resume"}

        return await self._cmd_start({
            "backend":    meta.backend,
            "mode":       args.get("mode", meta.mode),
            "model":      meta.model,
            "mcp_port":   meta.mcp_port,
            "prompt":     meta.prompt,
            "session_id": args.get("session_id"),
            "config_dir": meta.config_dir,
            **meta.extra,
        })

    # ── Background tasks ─────────────────────────────────────────────────

    async def _drain_stdout_loop(self) -> None:
        """Read subprocess stdout line-by-line, buffer with seq_id."""
        session = self._session
        fn      = self._transform_fn   # capture — avoids TOCTOU
        acc     = _ToolCallAcc()       # fresh accumulator per subprocess
        while session is self._session:
            line = await session.read_stdout_line()
            if line is None:
                await session.wait()   # populate returncode before checking (EOF/exit race)
                if session.exit_code not in (None, 0):
                    name   = os.path.basename(session._binary)
                    stderr = await session.drain_stderr()
                    detail = f": {stderr}" if stderr else ""
                    msg    = f"{name} exited {session.exit_code}{detail}"
                    raw    = f'{{"type":"result","is_error":true,"error":"{_esc(msg)}"}}'
                else:
                    # B3: notify C# of clean exit so spinner clears
                    raw = '{"type":"result","subtype":"done","is_error":false}'
                # EOF synthetic events always use _transform_line (our own format, not backend's)
                for p in _transform_line(raw, acc):
                    self._relay_buf.enqueue(p)
                break
            for p in fn(line, acc):
                self._relay_buf.enqueue(p)

    async def _ppid_watchdog(self) -> None:
        """Exit relay if Unity (parent) dies."""
        while True:
            await asyncio.sleep(PPID_POLL)
            if os.getppid() != self._orig_ppid:
                await self._kill_current()
                os._exit(0)

    # ── Buffer delegation (backward compat for tests) ─────────────────────

    def _enqueue(self, line: str) -> None:
        self._relay_buf.enqueue(line)

    @property
    def _buf(self):         return self._relay_buf._buf
    @property
    def _dropped(self):     return self._relay_buf._dropped
    @property
    def _next_seq(self):    return self._relay_buf._next_seq
    @property
    def _new_data(self):    return self._relay_buf._new_data

    # ── Helpers ──────────────────────────────────────────────────────────

    async def _kill_current(self) -> None:
        if self._drain_task is not None:
            self._drain_task.cancel()
            try:
                await self._drain_task
            except (asyncio.CancelledError, Exception):
                pass
            self._drain_task = None
        if self._session is not None:
            await self._session.kill()
            self._session = None
            self._relay_buf.clear()  # prevent cross-session contamination; seq stays monotonic

    async def _shutdown(self) -> None:
        """Graceful shutdown: kill child then exit. Wired to SIGTERM."""
        await self._kill_current()
        os._exit(0)


def _esc(s: str) -> str:
    return (s.replace("\\", "\\\\")
             .replace('"', '\\"')
             .replace("\n", "\\n")
             .replace("\r", "\\r")
             .replace("\t", "\\t"))


def _extract_text_from_turn(line: str) -> str:
    """Extract plain text from a stream-json user turn envelope. Fallback: raw line."""
    try:
        obj = json.loads(line)
        content = (obj.get("message") or {}).get("content") or []
        return "\n".join(b["text"] for b in content if b.get("type") == "text")
    except Exception:
        return line


async def _main() -> None:
    port = _find_free_port()
    relay = ChatRelay()
    loop = asyncio.get_running_loop()
    try:
        for _sig in (signal.SIGTERM, signal.SIGINT):
            loop.add_signal_handler(
                _sig,
                lambda: asyncio.create_task(relay._shutdown()),
            )
    except NotImplementedError:
        pass
    print(f"relay_port:{port}", flush=True)
    await relay.serve(port)


def main() -> None:
    """Sync entrypoint for the `unity-biome-mcp-relay` console script."""
    asyncio.run(_main())


if __name__ == "__main__":
    main()
