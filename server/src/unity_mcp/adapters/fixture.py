"""FixtureAdapter: deterministic replay of JSONL fixture files.

No process management. Used exclusively for offline TDD.
"""
import json
from collections.abc import AsyncIterator  # noqa: TC003
from pathlib import Path  # noqa: TC003

from ..agent_event import AgentEvent, ProviderCapabilities
from ..cli_session import SessionMeta  # noqa: TC001


class FixtureAdapter:
    """Replay a JSONL fixture file line-by-line. All lifecycle methods are no-ops."""

    def __init__(self, fixture_path: Path) -> None:
        self._path = fixture_path
        self._caps = ProviderCapabilities()

    async def probe(self) -> ProviderCapabilities:
        return self._caps

    async def start(self, meta: SessionMeta) -> None:
        # Fixture playback is offline and already deterministic; no runtime process.
        return None

    async def prompt(self, text: str, turn_id: int) -> None:
        # Prompt text is not executed for fixture mode; events are replayed as-is.
        return None

    async def cancel(self) -> None:
        # Cancellation is replay-driven only; the fixture file remains unchanged.
        return None

    async def set_mode(self, mode: str) -> None:
        # Mode is fixed in fixture fixtures; runtime adapters implement switching.
        return None

    async def close(self) -> None:
        # Explicit close is a no-op for file-backed replay adapters.
        return None

    async def events(self) -> AsyncIterator[AgentEvent]:
        for line in self._path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line:
                yield AgentEvent.model_validate(json.loads(line))
