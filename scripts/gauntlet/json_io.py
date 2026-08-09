"""Small atomic JSON primitives shared by release evidence artifacts."""

from __future__ import annotations

import json
import os
import tempfile
from contextlib import suppress
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


class JsonFileError(ValueError):
    """Raised when a strict JSON artifact cannot be read or written."""


def load_json_object(path: Path, *, max_bytes: int = 2 * 1024 * 1024) -> dict[str, object]:
    try:
        size = path.stat().st_size
    except FileNotFoundError as exc:
        raise JsonFileError("JSON file does not exist") from exc
    except OSError as exc:
        raise JsonFileError("JSON file metadata cannot be read") from exc
    if size > max_bytes:
        raise JsonFileError("JSON file exceeds the size limit")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except UnicodeError as exc:
        raise JsonFileError("JSON file is not valid UTF-8") from exc
    except json.JSONDecodeError as exc:
        raise JsonFileError(f"JSON is invalid at line {exc.lineno}, column {exc.colno}") from exc
    except OSError as exc:
        raise JsonFileError("JSON file cannot be read") from exc
    if not isinstance(value, dict):
        raise JsonFileError("JSON root must be an object")
    return value


def atomic_write_json(path: Path, value: object) -> None:
    """Replace one JSON file only after bytes are flushed to stable storage."""
    try:
        encoded = (
            json.dumps(
                value,
                ensure_ascii=False,
                indent=2,
                sort_keys=True,
            ).encode("utf-8")
            + b"\n"
        )
    except (TypeError, ValueError) as exc:
        raise JsonFileError(f"value is not JSON serializable: {exc}") from exc

    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        dir=path.parent,
        prefix=f".{path.name}.",
        suffix=".tmp",
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, path)
        _fsync_directory(path.parent)
    except Exception:
        with suppress(FileNotFoundError):
            os.unlink(temporary_name)
        raise


def _fsync_directory(path: Path) -> None:
    if not hasattr(os, "O_DIRECTORY"):
        return
    descriptor = os.open(path, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
