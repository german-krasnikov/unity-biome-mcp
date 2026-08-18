"""Small atomic JSON primitives shared by release evidence artifacts."""


import json
import math
import os
import stat
import tempfile
from contextlib import suppress
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


class JsonFileError(ValueError):
    """Raised when a strict JSON artifact cannot be read or written."""


class _DuplicateKeyError(ValueError):
    """Internal signal raised by the JSON object-pairs hook."""


class _NonFiniteNumberError(ValueError):
    """Internal signal raised for JSON constants outside RFC 8259."""


def parse_json_object(data: bytes, *, source: str) -> dict[str, object]:
    """Parse one UTF-8 JSON object without accepting duplicate object keys."""
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise JsonFileError(f"{source}: JSON is not valid UTF-8") from exc
    try:
        value = json.loads(
            text,
            object_pairs_hook=_unique_object,
            parse_constant=_reject_non_finite,
            parse_float=_parse_finite_float,
        )
    except _DuplicateKeyError as exc:
        raise JsonFileError(f"{source}: JSON object contains a duplicate key") from exc
    except _NonFiniteNumberError as exc:
        raise JsonFileError(f"{source}: JSON contains a non-finite number") from exc
    except json.JSONDecodeError as exc:
        raise JsonFileError(
            f"{source}: JSON is invalid at line {exc.lineno}, column {exc.colno}"
        ) from exc
    if not isinstance(value, dict):
        raise JsonFileError(f"{source}: JSON root must be an object")
    return value


def load_json_object(path: Path, *, max_bytes: int = 2 * 1024 * 1024) -> dict[str, object]:
    if isinstance(max_bytes, bool) or not isinstance(max_bytes, int) or max_bytes < 1:
        raise JsonFileError("JSON size limit must be a positive integer")
    try:
        with path.open("rb") as stream:
            path_metadata = path.lstat()
            descriptor_metadata = os.fstat(stream.fileno())
            if (
                not stat.S_ISREG(path_metadata.st_mode)
                or not stat.S_ISREG(descriptor_metadata.st_mode)
                or (path_metadata.st_dev, path_metadata.st_ino)
                != (descriptor_metadata.st_dev, descriptor_metadata.st_ino)
            ):
                raise JsonFileError("JSON path is not a stable regular file")
            data = stream.read(max_bytes + 1)
    except FileNotFoundError as exc:
        raise JsonFileError("JSON file does not exist") from exc
    except OSError as exc:
        raise JsonFileError("JSON file cannot be read") from exc
    if len(data) > max_bytes:
        raise JsonFileError("JSON file exceeds the size limit")
    return parse_json_object(data, source=path.name)


def _unique_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    value: dict[str, object] = {}
    for key, item in pairs:
        if key in value:
            raise _DuplicateKeyError
        value[key] = item
    return value


def _reject_non_finite(value: str) -> object:
    raise _NonFiniteNumberError(value)


def _parse_finite_float(value: str) -> float:
    parsed = float(value)
    if not math.isfinite(parsed):
        raise _NonFiniteNumberError(value)
    return parsed


def atomic_write_json(path: Path, value: object) -> None:
    """Replace one JSON file only after bytes are flushed to stable storage."""
    try:
        encoded = (
            json.dumps(
                value,
                ensure_ascii=False,
                indent=2,
                allow_nan=False,
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
