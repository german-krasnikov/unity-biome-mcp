"""Typed artifact-manifest contracts shared by producers and gates."""

from __future__ import annotations

import os
import stat
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Sequence
    from pathlib import Path


class ArtifactError(ValueError):
    """Raised when staged release artifact identity is not provable."""


@dataclass(frozen=True, slots=True)
class ArtifactRecord:
    artifact_type: str
    filename: str
    package_name: str
    package_version: str
    archive_sha256: str
    content_sha256: str
    size_bytes: int


@dataclass(frozen=True, slots=True)
class ArtifactManifest:
    head_sha: str
    product_version: str
    artifacts: tuple[ArtifactRecord, ...]
    manifest_sha: str

    @property
    def archive_digests(self) -> dict[str, str]:
        return {record.artifact_type: record.archive_sha256 for record in self.artifacts}

    @property
    def content_digests(self) -> dict[str, str]:
        return {record.artifact_type: record.content_sha256 for record in self.artifacts}

    @property
    def artifact_digests(self) -> dict[str, str]:
        """Compatibility name for exact staged archive digests."""
        return self.archive_digests


def read_stable_artifact(path: Path, max_bytes: int) -> bytes:
    """Read once while proving descriptor and pathname identity stayed stable."""
    try:
        with path.open("rb") as stream:
            path_before = path.lstat()
            descriptor_before = os.fstat(stream.fileno())
            _require_regular((path_before, descriptor_before), path.name)
            _require_same_file(path_before, descriptor_before, path.name)
            snapshot = stream.read(max_bytes + 1)
            descriptor_after = os.fstat(stream.fileno())
            path_after = path.lstat()
    except ArtifactError:
        raise
    except (OSError, ValueError) as exc:
        raise ArtifactError(f"artifact is not readable: {path.name}") from exc
    _require_regular((path_before, descriptor_before, descriptor_after, path_after), path.name)
    _require_unchanged(path_before, path_after, path.name)
    _require_unchanged(descriptor_before, descriptor_after, path.name)
    _require_same_file(path_after, descriptor_after, path.name)
    if len(snapshot) > max_bytes:
        raise ArtifactError(f"artifact exceeds {max_bytes} byte safety limit: {path.name}")
    return snapshot


def _require_regular(observations: Sequence[os.stat_result], filename: str) -> None:
    if any(not stat.S_ISREG(metadata.st_mode) for metadata in observations):
        raise ArtifactError(f"artifact is not a stable regular file: {filename}")


def _require_unchanged(before: os.stat_result, after: os.stat_result, filename: str) -> None:
    if _stable_token(before) != _stable_token(after):
        raise ArtifactError(f"artifact is not a stable regular file: {filename}")


def _require_same_file(path_metadata: os.stat_result, descriptor_metadata: os.stat_result, filename: str) -> None:
    path_identity = _file_identity(path_metadata)
    descriptor_identity = _file_identity(descriptor_metadata)
    if os.name != "nt" and path_identity and descriptor_identity and path_identity != descriptor_identity:
        raise ArtifactError(f"artifact is not a stable regular file: {filename}")
    if _content_token(path_metadata) != _content_token(descriptor_metadata):
        raise ArtifactError(f"artifact is not a stable regular file: {filename}")


def _stable_token(metadata: os.stat_result) -> tuple[int, int, int]:
    return (
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_ctime_ns,
    )


def _content_token(metadata: os.stat_result) -> tuple[int, int]:
    return (metadata.st_size, metadata.st_mtime_ns)


def _file_identity(metadata: os.stat_result) -> tuple[int, int] | None:
    inode = getattr(metadata, "st_ino", 0)
    device = getattr(metadata, "st_dev", 0)
    if inode == 0:
        return None
    return (device, inode)
