"""AgentAdapter Protocol + EventContext dataclass."""
from collections.abc import AsyncIterator
from dataclasses import dataclass
from typing import Protocol, runtime_checkable

from ..agent_event import AgentEvent, ProviderCapabilities
from ..cli_session import SessionMeta


@runtime_checkable
class AgentAdapter(Protocol):
    """Structural interface for all provider adapters (duck-typed)."""

    async def probe(self) -> ProviderCapabilities: ...
    async def start(self, meta: SessionMeta) -> None: ...
    async def prompt(self, text: str, turn_id: int) -> None: ...
    async def cancel(self) -> None: ...
    async def set_mode(self, mode: str) -> None: ...
    async def close(self) -> None: ...
    def events(self) -> AsyncIterator[AgentEvent]: ...


@dataclass
class EventContext:
    """Correlation fields passed into parse_pipe_string()."""

    conversation_id: str
    session_id:      str
    turn_id:         int
    sequence:        int
