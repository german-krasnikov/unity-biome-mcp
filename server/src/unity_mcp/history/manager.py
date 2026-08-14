"""T23: HistoryManager — route events to HistoryStore."""
from __future__ import annotations

import contextlib
import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING

from .models import EXCLUDED_KINDS, ConversationHeader
from .store import HistoryStore

if TYPE_CHECKING:
    from pathlib import Path

    from ..agent_event import AgentEvent


def _utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


class HistoryManager:
    def __init__(self, history_dir: Path, fingerprint: str) -> None:
        self._history_dir = history_dir
        self._fingerprint = fingerprint
        self._conv_id: str | None = None
        self._store: HistoryStore | None = None
        self._header: ConversationHeader | None = None
        self._session_id: str = ""

    def open_conversation(self, backend: str) -> str:
        self._conv_id = str(uuid.uuid4())
        self._session_id = ""
        self._history_dir.mkdir(parents=True, exist_ok=True)
        self._store = HistoryStore(self._history_dir, self._conv_id)
        now = _utcnow()
        self._header = ConversationHeader(
            conv_id=self._conv_id,
            title="",
            created_at=now,
            updated_at=now,
            backend=backend,
            session_id="",
            turn_count=0,
            fingerprint=self._fingerprint,
        )
        return self._conv_id

    def observe(self, event: AgentEvent) -> None:
        """Called for every event. Synchronous, best-effort."""
        if self._store is None or self._header is None:
            return
        if event.kind in EXCLUDED_KINDS:
            return

        self._store.append_event(event)

        if event.kind == "session_started":
            self._session_id = event.payload.get("session_id", "") or event.session_id

        if event.kind == "turn_started" and self._header.title == "":
            title = event.payload.get("text", "")[:80]
            self._header = ConversationHeader(
                conv_id=self._header.conv_id,
                title=title,
                created_at=self._header.created_at,
                updated_at=_utcnow(),
                backend=self._header.backend,
                session_id=self._session_id,
                turn_count=self._header.turn_count,
                fingerprint=self._header.fingerprint,
            )

        if event.kind == "turn_completed":
            self._header = ConversationHeader(
                conv_id=self._header.conv_id,
                title=self._header.title,
                created_at=self._header.created_at,
                updated_at=_utcnow(),
                backend=self._header.backend,
                session_id=self._session_id,
                turn_count=self._header.turn_count + 1,
                fingerprint=self._header.fingerprint,
            )
            self._store.flush_header(self._header)

    def close_conversation(self) -> None:
        """Finalize header, run retention eviction."""
        if self._store is not None and self._header is not None:
            self._store.flush_header(self._header)
        from .retention import evict
        with contextlib.suppress(OSError):
            evict(self._history_dir)
        self._conv_id = None
        self._store = None
        self._header = None

    def current_conv_id(self) -> str | None:
        return self._conv_id


_manager: HistoryManager | None = None


def get_history_manager() -> HistoryManager | None:
    return _manager


def init_history_manager(fingerprint: str) -> HistoryManager:
    global _manager
    from ..paths import history_dir
    _manager = HistoryManager(history_dir(fingerprint), fingerprint)
    return _manager


def ensure_history_manager(fingerprint: str) -> HistoryManager:
    """Initialize or re-initialize the manager when fingerprint changes (M12)."""
    mgr = get_history_manager()
    if mgr is None or mgr._fingerprint != fingerprint:
        return init_history_manager(fingerprint)
    return mgr
