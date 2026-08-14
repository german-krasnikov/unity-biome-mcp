"""LegacyCliAdapter: wraps BackendDef + CliSession, yields AgentEvent objects."""
import os
import uuid
from collections.abc import AsyncIterator
from dataclasses import replace

from ..agent_event import AgentEvent, ProviderCapabilities
from ..backend_def import (
    OUTPUT_FORMAT_CODEX_JSON,
    OUTPUT_FORMAT_KIMI_JSON,
    OUTPUT_FORMAT_OPENCODE_JSON,
    OUTPUT_FORMAT_PLAIN_TEXT,
    OUTPUT_FORMAT_STREAM_JSON,
    BackendDef,
)
from ..cli_session import CliSession, SessionMeta
from ..stream_transform import (
    _ToolCallAcc,
    _transform_codex_line,
    _transform_kimi_line,
    _transform_line,
    _transform_opencode_line,
    _transform_plain_text_line,
)
from .pipe_parser import parse_pipe_string
from .protocol import EventContext

_TRANSFORM_FNS = {
    OUTPUT_FORMAT_STREAM_JSON:   _transform_line,
    OUTPUT_FORMAT_PLAIN_TEXT:    _transform_plain_text_line,
    OUTPUT_FORMAT_CODEX_JSON:    _transform_codex_line,
    OUTPUT_FORMAT_OPENCODE_JSON: _transform_opencode_line,
    OUTPUT_FORMAT_KIMI_JSON:     _transform_kimi_line,
}


class LegacyCliAdapter:
    """Wrap one BackendDef + one CliSession, emitting AgentEvent from stdout."""

    def __init__(self, backend: BackendDef) -> None:
        self._backend        = backend
        self._transform_fn   = _TRANSFORM_FNS.get(backend.output_format, _transform_plain_text_line)
        self._session: CliSession | None = None
        self._meta: SessionMeta | None   = None
        self._acc: _ToolCallAcc          = _ToolCallAcc()
        self._conversation_id: str       = str(uuid.uuid4())
        self._session_id:      str       = ""
        self._turn_id:         int       = 0
        self._seq:             int       = 0

    async def probe(self) -> ProviderCapabilities:
        caps = await self._backend.probe_capabilities()
        return ProviderCapabilities.from_probe(self._backend.name, caps)

    async def start(self, meta: SessionMeta) -> None:
        await self.cancel()
        resolved = await self._backend.resolve_binary()
        if resolved is None:
            raise RuntimeError(f"binary '{self._backend.binary}' not found in PATH")
        argv, env_set, env_strip = self._backend.build_args(
            mode=meta.mode, model=meta.model, mcp_port=meta.mcp_port,
            prompt=meta.prompt, session_id=meta.internal_session_id,
            config_dir=meta.config_dir, **meta.extra,
        )
        self._session = CliSession(
            binary=resolved, argv=argv,
            env_set=env_set, env_strip=env_strip,
            reads_stdin=self._backend.reads_stdin,
        )
        await self._session.start()
        if not self._backend.reads_stdin:
            self._session.close_stdin()
        self._meta = meta
        self._acc  = _ToolCallAcc()
        self._seq  = 0

    async def prompt(self, text: str, turn_id: int) -> None:
        self._turn_id = turn_id
        if not self._backend.reads_stdin:
            if self._meta is None:
                raise RuntimeError("no active session meta")
            await self.start(replace(self._meta, prompt=text))
            return
        if self._session is None:
            raise RuntimeError("no active session")
        await self._session.write_line(text)

    async def cancel(self) -> None:
        if self._session is not None:
            await self._session.kill()
            self._session = None

    async def set_mode(self, mode: str) -> None:
        if self._meta is not None:
            await self.start(replace(self._meta, mode=mode))

    async def close(self) -> None:
        await self.cancel()

    async def events(self) -> AsyncIterator[AgentEvent]:
        session = self._session
        if session is None:
            return
        fn  = self._transform_fn
        acc = self._acc

        while session is self._session:
            line = await session.read_stdout_line()
            if line is None:
                await session.wait()
                if session.exit_code not in (None, 0):
                    name   = os.path.basename(session._binary)
                    stderr = await session.drain_stderr()
                    msg    = f"{name} exited {session.exit_code}"
                    if stderr:
                        msg += f": {stderr}"
                    self._seq += 1
                    yield AgentEvent(
                        sequence=self._seq, kind="error",
                        conversation_id=self._conversation_id,
                        session_id=self._session_id,
                        turn_id=self._turn_id,
                        payload={"message": msg},
                    )
                elif self._backend.output_format == OUTPUT_FORMAT_PLAIN_TEXT:
                    self._seq += 1
                    yield AgentEvent(
                        sequence=self._seq, kind="turn_completed",
                        conversation_id=self._conversation_id,
                        session_id=self._session_id,
                        turn_id=self._turn_id,
                    )
                break

            for pipe_str in fn(line, acc):
                for evt in parse_pipe_string(pipe_str, self._next_ctx()):
                    self._seq += 1
                    yield evt.model_copy(update={"sequence": self._seq})

    def _next_ctx(self) -> EventContext:
        """Capture current correlation state (caller increments _seq after yielding)."""
        return EventContext(
            conversation_id=self._conversation_id,
            session_id=self._session_id,
            turn_id=self._turn_id,
            sequence=self._seq,
        )

