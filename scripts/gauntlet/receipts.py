from __future__ import annotations

import hashlib
import json
import os
from datetime import datetime, timezone
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from collections.abc import Callable, Mapping
    from pathlib import Path

_GENESIS_HASH = "0" * 64


class JournalError(ValueError):
    """Raised when a receipt journal is malformed or has been modified."""


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _canonical_bytes(value: object) -> bytes:
    try:
        encoded = json.dumps(
            value,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
    except (TypeError, ValueError) as exc:
        raise JournalError(f"event is not JSON serializable: {exc}") from exc
    return encoded.encode("utf-8")


def _event_hash(event_without_hash: Mapping[str, Any]) -> str:
    return hashlib.sha256(_canonical_bytes(event_without_hash)).hexdigest()


def content_hash(value: object) -> str:
    """Return a stable digest without persisting potentially sensitive content."""

    return hashlib.sha256(_canonical_bytes(value)).hexdigest()


def verify_journal(path: Path) -> list[dict[str, Any]]:
    """Validate sequence and hash continuity, returning canonical event objects."""

    if not path.exists():
        raise JournalError(f"journal does not exist: {path}")

    events: list[dict[str, Any]] = []
    expected_previous = _GENESIS_HASH
    expected_run_id: str | None = None

    with path.open(encoding="utf-8") as stream:
        for line_number, raw_line in enumerate(stream, start=1):
            if not raw_line.strip():
                raise JournalError(f"blank journal line at {line_number}")
            try:
                event = json.loads(raw_line)
            except json.JSONDecodeError as exc:
                raise JournalError(f"invalid JSON at line {line_number}: {exc}") from exc
            if not isinstance(event, dict):
                raise JournalError(f"event at line {line_number} is not an object")

            expected_sequence = len(events) + 1
            if event.get("seq") != expected_sequence:
                raise JournalError(
                    f"sequence mismatch at line {line_number}: "
                    f"expected {expected_sequence}, got {event.get('seq')}"
                )

            run_id = event.get("run_id")
            if not isinstance(run_id, str) or not run_id:
                raise JournalError(f"invalid run_id at line {line_number}")
            if expected_run_id is None:
                expected_run_id = run_id
            elif run_id != expected_run_id:
                raise JournalError(f"run_id changed at line {line_number}")

            if event.get("prev_hash") != expected_previous:
                raise JournalError(f"previous hash mismatch at line {line_number}")

            recorded_hash = event.get("event_hash")
            if not isinstance(recorded_hash, str):
                raise JournalError(f"missing event hash at line {line_number}")
            unhashed = dict(event)
            del unhashed["event_hash"]
            calculated_hash = _event_hash(unhashed)
            if recorded_hash != calculated_hash:
                raise JournalError(f"event hash mismatch at line {line_number}")

            events.append(event)
            expected_previous = recorded_hash

    return events


class ReceiptJournal:
    """Single-writer append-only JSONL journal with SHA-256 hash chaining."""

    def __init__(
        self,
        path: Path,
        run_id: str,
        clock: Callable[[], str] | None = None,
    ) -> None:
        if not run_id:
            raise JournalError("run_id must not be empty")
        self._path = path
        self._run_id = run_id
        self._clock = clock or _utc_now
        self._sequence = 0
        self._previous_hash = _GENESIS_HASH

        if path.exists() and path.stat().st_size:
            events = verify_journal(path)
            existing_run_id = events[0]["run_id"]
            if existing_run_id != run_id:
                raise JournalError(
                    f"run_id mismatch: journal has {existing_run_id!r}, requested {run_id!r}"
                )
            self._sequence = events[-1]["seq"]
            self._previous_hash = events[-1]["event_hash"]

    @property
    def path(self) -> Path:
        return self._path

    def append(self, event_type: str, payload: Mapping[str, Any]) -> dict[str, Any]:
        if not event_type:
            raise JournalError("event_type must not be empty")

        event: dict[str, Any] = {
            "schema_version": 1,
            "run_id": self._run_id,
            "seq": self._sequence + 1,
            "event_type": event_type,
            "timestamp": self._clock(),
            "prev_hash": self._previous_hash,
            "payload": dict(payload),
        }
        event["event_hash"] = _event_hash(event)

        self._path.parent.mkdir(parents=True, exist_ok=True)
        line = _canonical_bytes(event).decode("utf-8") + "\n"
        with self._path.open("a", encoding="utf-8", newline="\n") as stream:
            stream.write(line)
            stream.flush()
            os.fsync(stream.fileno())

        self._sequence = event["seq"]
        self._previous_hash = event["event_hash"]
        return event
