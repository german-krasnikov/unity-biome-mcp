"""T15: ChangeSet coordinator — module-level singleton for session lifecycle."""
from __future__ import annotations

import uuid
from datetime import datetime, timezone
from typing import TYPE_CHECKING

from .changeset import ChangeOperation, ChangeSet, ContentRef

if TYPE_CHECKING:
    from collections.abc import Callable
    from pathlib import Path


class ChangeSetCoordinator:
    def __init__(
        self,
        get_session_id: Callable[[], str],
        _no_journal: bool = False,
        _journal_dir: Path | None = None,
    ) -> None:
        self._get_session_id = get_session_id
        self._no_journal = _no_journal
        self._journal_dir = _journal_dir
        self._current: ChangeSet | None = None
        self._journal = None

    def append(self, cmd: str, receipt: dict) -> None:  # noqa: ARG002
        sid = self._get_session_id()
        if self._current is None or self._current.session_id != sid:
            self._open(sid)
        op = ChangeOperation.from_receipt(receipt)
        self._current.operations.append(op)
        if self._journal is not None:
            self._journal.write_operation(op, self._current.changeset_id)

    def append_file_op(
        self,
        cmd: str,  # noqa: ARG002
        path: str,
        before_ref: ContentRef | None,
        after_ref: ContentRef | None,
    ) -> None:
        """Record a file-level mutation directly (not via C# receipt)."""
        sid = self._get_session_id()
        if self._current is None or self._current.session_id != sid:
            self._open(sid)
        op = ChangeOperation(
            operation_id=str(uuid.uuid4()),
            kind="modify",
            target_type="asset",
            target_path=path,
            prop=None,
            before_ref=before_ref,
            after_ref=after_ref,
            reversible=True,
            timestamp=datetime.now(timezone.utc).isoformat(),
        )
        self._current.operations.append(op)
        if self._journal is not None:
            self._journal.write_operation(op, self._current.changeset_id)

    def finalize(self) -> None:
        if self._current is None:
            return
        self._current.status = "finalized"
        self._current.finalized_at = datetime.now(timezone.utc).isoformat()

    def get_current(self) -> ChangeSet | None:
        return self._current

    def _open(self, session_id: str) -> None:
        if self._journal is not None:
            self._journal.close()
            self._journal = None

        cs = ChangeSet(
            changeset_id=str(uuid.uuid4()),
            session_id=session_id,
            created_at=datetime.now(timezone.utc).isoformat(),
        )
        self._current = cs

        if not self._no_journal:
            from .changeset_journal import ChangeSetJournal

            self._journal = ChangeSetJournal(
                session_id=session_id, _dir=self._journal_dir
            )
            self._journal.write_header(cs)


# Module-level singleton — initialized in server lifespan
_coordinator: ChangeSetCoordinator | None = None


def get_coordinator() -> ChangeSetCoordinator | None:
    return _coordinator


def init_coordinator(
    get_session_id: Callable[[], str],
) -> ChangeSetCoordinator:
    global _coordinator
    _coordinator = ChangeSetCoordinator(get_session_id=get_session_id)
    return _coordinator
