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
        pass

    async def prompt(self, text: str, turn_id: int) -> None:
        pass

    async def cancel(self) -> None:
        pass

    async def set_mode(self, mode: str) -> None:
        pass

    async def close(self) -> None:
        pass

    async def events(self) -> AsyncIterator[AgentEvent]:
        for line in self._path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line:
                yield AgentEvent.model_validate(json.loads(line))
