"""Content-addressed release evidence and strict runtime receipts."""

from __future__ import annotations

import hashlib
import json
import os
import stat
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from collections.abc import Mapping

_MAX_EVIDENCE_BYTES = 16 * 1024 * 1024
_REF_KEYS = {"path", "sha256", "size_bytes"}
_RUNTIME_KEYS = {
    "profile",
    "run_id",
    "run_manifest_sha",
    "driver",
    "head_sha",
    "os",
    "python",
    "unity",
    "plugin_scope",
    "consumed_artifacts",
    "junit_sha",
    "journal_sha",
    "exit_code",
}
_WORKER_KEYS = {
    "profile",
    "run_id",
    "run_manifest_sha",
    "role",
    "worker_id",
    "project_id",
    "port",
    "os",
    "unity",
    "plugin_scope",
    "loaded_artifacts",
    "clean_before",
}
_CLEANUP_KEYS = {
    "profile",
    "run_id",
    "run_manifest_sha",
    "obligation",
    "clean",
    "details_hash",
}
_KIND_KEYS = {
    "runtime": _RUNTIME_KEYS,
    "worker_identity": _WORKER_KEYS,
    "cleanup": _CLEANUP_KEYS,
}


class AttestationError(ValueError):
    """Raised when evidence bytes or their semantic receipt are invalid."""


@dataclass(frozen=True, slots=True)
class FileReference:
    relative_path: str
    sha256: str
    size_bytes: int

    def as_dict(self) -> dict[str, object]:
        return {
            "path": self.relative_path,
            "sha256": self.sha256,
            "size_bytes": self.size_bytes,
        }


def build_file_reference(path: Path, root: Path) -> FileReference:
    """Describe one evidence file relative to its owned bundle root."""
    resolved_root = root.resolve()
    resolved_path = path.resolve()
    try:
        relative = resolved_path.relative_to(resolved_root)
    except ValueError as exc:
        raise AttestationError("evidence path escapes its bundle root") from exc
    normalized = PurePosixPath(relative.as_posix())
    _validate_relative_path(normalized.as_posix())
    payload = _read_bounded_file(resolved_path, _MAX_EVIDENCE_BYTES)
    return FileReference(
        relative_path=normalized.as_posix(),
        sha256=hashlib.sha256(payload).hexdigest(),
        size_bytes=len(payload),
    )


def parse_file_reference(value: object) -> FileReference:
    if not isinstance(value, dict) or set(value) != _REF_KEYS:
        raise AttestationError("evidence file reference schema mismatch")
    path = value["path"]
    digest = value["sha256"]
    size = value["size_bytes"]
    if not isinstance(path, str):
        raise AttestationError("evidence file path must be a string")
    _validate_relative_path(path)
    _validate_digest(digest, "evidence file")
    if isinstance(size, bool) or not isinstance(size, int) or size < 0:
        raise AttestationError("evidence file size must be non-negative")
    return FileReference(path, digest, size)


def read_verified_file(
    reference: FileReference,
    root: Path,
    *,
    max_bytes: int = _MAX_EVIDENCE_BYTES,
) -> bytes:
    """Read once, then verify size and digest to avoid parse/hash TOCTOU."""
    resolved_root = root.resolve()
    candidate = (resolved_root / reference.relative_path).resolve()
    try:
        candidate.relative_to(resolved_root)
    except ValueError as exc:
        raise AttestationError("evidence file resolves outside its bundle") from exc
    if reference.size_bytes > max_bytes:
        raise AttestationError("evidence file exceeds its size limit")
    payload = _read_bounded_file(candidate, max_bytes)
    if len(payload) != reference.size_bytes:
        raise AttestationError("evidence file size does not match its reference")
    if hashlib.sha256(payload).hexdigest() != reference.sha256:
        raise AttestationError("evidence file digest does not match its reference")
    return payload


def _read_bounded_file(path: Path, max_bytes: int) -> bytes:
    try:
        with path.open("rb") as stream:
            metadata = os.fstat(stream.fileno())
            if not stat.S_ISREG(metadata.st_mode):
                raise AttestationError("evidence path is not a regular file")
            payload = stream.read(max_bytes + 1)
    except OSError as exc:
        raise AttestationError("evidence file cannot be read") from exc
    if len(payload) > max_bytes:
        raise AttestationError("evidence file exceeds its size limit")
    return payload


def build_receipt(kind: str, fields: Mapping[str, object]) -> dict[str, object]:
    """Build one strict, self-hashed receipt for a trusted producer."""
    if kind not in _KIND_KEYS:
        raise AttestationError(f"unsupported receipt kind: {kind}")
    receipt: dict[str, object] = {
        "schema_version": 1,
        "kind": kind,
        **dict(fields),
    }
    _validate_receipt_shape(receipt, include_hash=False)
    _validate_receipt_values(receipt)
    receipt["receipt_hash"] = content_hash(receipt)
    return receipt


def parse_receipt_bytes(payload: bytes, expected_kind: str) -> dict[str, object]:
    try:
        value = json.loads(payload.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AttestationError("receipt is not valid UTF-8 JSON") from exc
    if not isinstance(value, dict):
        raise AttestationError("receipt root must be an object")
    _validate_receipt_shape(value, include_hash=True)
    if value["kind"] != expected_kind:
        raise AttestationError(f"expected {expected_kind} receipt")
    supplied = value["receipt_hash"]
    unhashed = dict(value)
    del unhashed["receipt_hash"]
    if supplied != content_hash(unhashed):
        raise AttestationError("receipt hash mismatch")
    _validate_receipt_values(value)
    return value


def _validate_receipt_shape(value: dict[str, object], *, include_hash: bool) -> None:
    kind = value.get("kind")
    if kind not in _KIND_KEYS:
        raise AttestationError("receipt kind is invalid")
    expected = {"schema_version", "kind", *_KIND_KEYS[str(kind)]}
    if include_hash:
        expected.add("receipt_hash")
    if set(value) != expected:
        raise AttestationError("receipt schema mismatch")
    if value.get("schema_version") != 1:
        raise AttestationError("unsupported receipt schema version")


def _validate_receipt_values(value: dict[str, object]) -> None:
    kind = value["kind"]
    for key in ("profile", "run_id"):
        _require_text(value.get(key), key)
    _validate_digest(value.get("run_manifest_sha"), "run manifest")
    if kind == "runtime":
        _validate_runtime(value)
    elif kind == "worker_identity":
        _validate_worker(value)
    else:
        _validate_cleanup(value)


def _validate_runtime(value: dict[str, object]) -> None:
    _require_text(value.get("head_sha"), "head_sha")
    _require_text(value.get("driver"), "driver")
    _require_text(value.get("os"), "os")
    _require_text(value.get("python"), "python")
    _require_optional_text(value.get("unity"), "unity")
    _require_text(value.get("plugin_scope"), "plugin_scope")
    _validate_digest_map(value.get("consumed_artifacts"), "consumed artifacts")
    _validate_digest(value.get("junit_sha"), "JUnit")
    _validate_digest(value.get("journal_sha"), "journal")
    exit_code = value.get("exit_code")
    if isinstance(exit_code, bool) or not isinstance(exit_code, int):
        raise AttestationError("runtime exit_code must be an integer")


def _validate_worker(value: dict[str, object]) -> None:
    for key in ("role", "worker_id", "project_id", "os", "unity", "plugin_scope"):
        _require_text(value.get(key), key)
    port = value.get("port")
    if isinstance(port, bool) or not isinstance(port, int) or not 1 <= port <= 65535:
        raise AttestationError("worker port is invalid")
    if value.get("clean_before") is not True:
        raise AttestationError("worker did not attest a clean pre-state")
    _validate_digest(value.get("project_id"), "worker project")
    _validate_digest_map(value.get("loaded_artifacts"), "loaded artifacts")


def _validate_cleanup(value: dict[str, object]) -> None:
    _require_text(value.get("obligation"), "obligation")
    if value.get("clean") is not True:
        raise AttestationError("cleanup receipt is not clean")
    _validate_digest(value.get("details_hash"), "cleanup details")


def _validate_digest_map(value: object, label: str) -> None:
    if not isinstance(value, dict) or not value:
        raise AttestationError(f"{label} must be a non-empty object")
    for key, digest in value.items():
        _require_text(key, f"{label} key")
        _validate_digest(digest, label)


def _validate_relative_path(value: str) -> None:
    path = PurePosixPath(value)
    if (
        not value
        or path.as_posix() != value
        or path.is_absolute()
        or "\\" in value
        or any(part in {"", ".", ".."} for part in path.parts)
    ):
        raise AttestationError("evidence file path must be normalized and relative")


def _validate_digest(value: object, label: str) -> None:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value.lower())
    ):
        raise AttestationError(f"{label} digest is invalid")


def _require_text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise AttestationError(f"{label} must be a non-empty string")
    return value


def _require_optional_text(value: object, label: str) -> str | None:
    if value is None:
        return None
    return _require_text(value, label)
