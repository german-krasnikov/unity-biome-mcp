"""AcpAgentAdapter: runs a CLI subprocess in ACP output mode."""

import contextlib
import json
import os
import uuid
from collections.abc import AsyncIterator  # noqa: TC003
from dataclasses import replace

from ..agent_event import AgentEvent, ProviderCapabilities
from ..backend_def import BackendDef, sanitize_extra_args
from ..cli_session import CliSession, SessionMeta
from ..permission_broker import PermissionBroker  # noqa: TC001
from .acp_parser import parse_acp_line
from .protocol import EventContext

_ACP_FORMAT_FLAG  = "--format"
_ACP_FORMAT_VALUE = "acp"


def _build_acp_argv(meta: SessionMeta) -> list[str]:
    argv = ["run", _ACP_FORMAT_FLAG, _ACP_FORMAT_VALUE, "--dangerously-skip-permissions"]
    if meta.model:
        argv += ["--model", meta.model]
    if meta.internal_session_id:
        argv += ["-s", meta.internal_session_id]
    extra_args = meta.extra.get("extra_args", "")
    if extra_args:
        argv += sanitize_extra_args(extra_args)
    argv.append(meta.prompt)
    return argv


class AcpAgentAdapter:
    """CLI subprocess in ACP output mode."""

    def __init__(self, backend: BackendDef, broker: PermissionBroker) -> None:
        self._backend         = backend
        self._broker          = broker
        self._session: CliSession | None = None
        self._meta: SessionMeta | None   = None
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
        # Call backend.build_args() to trigger config file write and get env vars;
        # discard its argv — we override with ACP format flag.
        _, env_set, env_strip = self._backend.build_args(
            mode=meta.mode, model=meta.model, mcp_port=meta.mcp_port,
            prompt=meta.prompt, session_id=meta.internal_session_id,
            config_dir=meta.config_dir, **meta.extra,
        )
        self._session = CliSession(
            binary=resolved, argv=self._build_argv(meta),
            env_set=env_set, env_strip=env_strip,
            reads_stdin=True,
        )
        await self._session.start()
        self._meta = meta
        self._seq  = 0

    def _build_argv(self, meta: SessionMeta) -> list[str]:
        return _build_acp_argv(meta)

    async def prompt(self, text: str, turn_id: int) -> None:
        """Write raw text prompt to stdin (OpenCode ACP reads plain text, not JSON)."""
        self._turn_id = turn_id
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

        while session is self._session:
            line = await session.read_stdout_line()
            if line is None:
                await session.wait()
                if session.exit_code not in (None, 0):
                    name = os.path.basename(session._binary)
                    stderr = await session.drain_stderr()
                    msg = f"{name} exited {session.exit_code}"
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
                break

            for evt in parse_acp_line(line, self._next_ctx()):
                self._seq += 1
                evt = evt.model_copy(update={"sequence": self._seq})

                if evt.kind == "permission_requested":
                    yield evt
                    await self._respond_to_permission(evt)
                else:
                    yield evt

    async def _respond_to_permission(self, evt: AgentEvent) -> None:
        if self._session is None:
            return
        decision  = self._broker.decide(evt.payload.get("tool_name", ""))
        outcome   = "allow" if "allow" in decision.outcome else "deny"
        response  = json.dumps({
            "type":       "session/permission_response",
            "request_id": evt.payload.get("request_id", ""),
            "outcome":    outcome,
        })
        with contextlib.suppress(Exception):
            await self._session.write_line(response)

    def _next_ctx(self) -> EventContext:
        return EventContext(
            conversation_id=self._conversation_id,
            session_id=self._session_id,
            turn_id=self._turn_id,
            sequence=self._seq,
        )
