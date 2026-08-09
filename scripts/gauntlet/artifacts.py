"""Build-once artifact identity manifests for release evidence."""

from __future__ import annotations

import hashlib
import os
import re
import stat
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

from gauntlet.json_io import JsonFileError, atomic_write_json, load_json_object
from gauntlet.package_archives import PackageArchiveError, validate_package_archive
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from collections.abc import Mapping

_ROOT_KEYS = {"schema_version", "head_sha", "package_version", "artifacts", "manifest_sha"}
_ARTIFACT_KEYS = {"type", "filename", "sha256", "size_bytes"}
_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
_SHA_PATTERN = re.compile(r"^[0-9a-f]+$")
_VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$")
_MAX_ARTIFACT_BYTES = 256 * 1024 * 1024


class ArtifactError(ValueError):
    """Raised when staged release artifact identity is not provable."""


@dataclass(frozen=True, slots=True)
class ArtifactRecord:
    artifact_type: str
    filename: str
    sha256: str
    size_bytes: int


@dataclass(frozen=True, slots=True)
class ArtifactManifest:
    head_sha: str
    package_version: str
    artifacts: tuple[ArtifactRecord, ...]
    manifest_sha: str

    @property
    def artifact_digests(self) -> dict[str, str]:
        return {record.artifact_type: record.sha256 for record in self.artifacts}


def build_artifact_manifest(
    head_sha: str,
    package_version: str,
    artifacts: Mapping[str, Path],
) -> ArtifactManifest:
    _validate_head_sha(head_sha)
    _validate_package_version(package_version)
    if not artifacts:
        raise ArtifactError("artifact mapping must not be empty")

    records: list[ArtifactRecord] = []
    filenames: set[str] = set()
    for artifact_type, path in artifacts.items():
        _validate_id(artifact_type, "artifact type")
        if not path.is_file():
            raise ArtifactError(f"artifact is not a regular file: {path.name}")
        filename = path.name
        _validate_filename(filename)
        _validate_artifact_filename(artifact_type, filename, package_version)
        if filename in filenames:
            raise ArtifactError(f"duplicate filename: {filename}")
        snapshot = _read_artifact_snapshot(path)
        _validate_package_archive(artifact_type, snapshot, filename, package_version)
        filenames.add(filename)
        records.append(ArtifactRecord(artifact_type, filename, hashlib.sha256(snapshot).hexdigest(), len(snapshot)))

    records.sort(key=lambda record: record.artifact_type)
    payload = _manifest_payload(head_sha, package_version, tuple(records))
    return ArtifactManifest(
        head_sha=head_sha,
        package_version=package_version,
        artifacts=tuple(records),
        manifest_sha=content_hash(payload),
    )


def write_artifact_manifest(path: Path, manifest: ArtifactManifest) -> None:
    atomic_write_json(path, _manifest_data(manifest))


def load_artifact_manifest(path: Path) -> ArtifactManifest:
    try:
        data = load_json_object(path)
    except JsonFileError as exc:
        raise ArtifactError(str(exc)) from exc
    if set(data) != _ROOT_KEYS:
        raise ArtifactError("artifact manifest schema mismatch")
    if data["schema_version"] != 1:
        raise ArtifactError("unsupported artifact manifest schema")
    head_sha = data["head_sha"]
    if not isinstance(head_sha, str):
        raise ArtifactError("head SHA must be a string")
    _validate_head_sha(head_sha)
    package_version = data["package_version"]
    if not isinstance(package_version, str):
        raise ArtifactError("package version must be a string")
    _validate_package_version(package_version)
    records = _parse_records(data["artifacts"], package_version)
    payload = _manifest_payload(head_sha, package_version, records)
    supplied_sha = data["manifest_sha"]
    if supplied_sha != content_hash(payload):
        raise ArtifactError("artifact manifest digest mismatch")
    return ArtifactManifest(head_sha, package_version, records, supplied_sha)


def verify_artifact_files(manifest: ArtifactManifest, directory: Path) -> None:
    root = _verified_artifact_root(directory)
    for record in manifest.artifacts:
        _validate_artifact_filename(record.artifact_type, record.filename, manifest.package_version)
        path = directory / record.filename
        _verify_artifact_path(path, root)
        snapshot = _read_artifact_snapshot(path)
        if len(snapshot) != record.size_bytes:
            raise ArtifactError(f"artifact size mismatch: {record.filename}")
        if hashlib.sha256(snapshot).hexdigest() != record.sha256:
            raise ArtifactError(f"artifact digest mismatch: {record.filename}")
        _validate_package_archive(record.artifact_type, snapshot, record.filename, manifest.package_version)


def _parse_records(value: object, package_version: str) -> tuple[ArtifactRecord, ...]:
    if not isinstance(value, list) or not value:
        raise ArtifactError("artifacts must be a non-empty list")
    records: list[ArtifactRecord] = []
    types: set[str] = set()
    filenames: set[str] = set()
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != _ARTIFACT_KEYS:
            raise ArtifactError("artifact record schema mismatch")
        artifact_type = raw["type"]
        filename = raw["filename"]
        digest = raw["sha256"]
        size = raw["size_bytes"]
        if not isinstance(artifact_type, str):
            raise ArtifactError("artifact type must be a string")
        if not isinstance(filename, str):
            raise ArtifactError("artifact filename must be a string")
        _validate_id(artifact_type, "artifact type")
        _validate_filename(filename)
        _validate_artifact_filename(artifact_type, filename, package_version)
        _validate_digest(digest)
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            raise ArtifactError("artifact size must be a non-negative integer")
        if artifact_type in types or filename in filenames:
            raise ArtifactError("duplicate artifact type or filename")
        types.add(artifact_type)
        filenames.add(filename)
        records.append(ArtifactRecord(artifact_type, filename, digest, size))
    records.sort(key=lambda record: record.artifact_type)
    return tuple(records)


def _manifest_payload(
    head_sha: str,
    package_version: str,
    records: tuple[ArtifactRecord, ...],
) -> dict[str, object]:
    return {
        "schema_version": 1,
        "head_sha": head_sha,
        "package_version": package_version,
        "artifacts": [
            {
                "type": record.artifact_type,
                "filename": record.filename,
                "sha256": record.sha256,
                "size_bytes": record.size_bytes,
            }
            for record in records
        ],
    }


def _manifest_data(manifest: ArtifactManifest) -> dict[str, object]:
    data = _manifest_payload(manifest.head_sha, manifest.package_version, manifest.artifacts)
    data["manifest_sha"] = manifest.manifest_sha
    return data


def _validate_head_sha(value: str) -> None:
    if len(value) not in {40, 64} or not _SHA_PATTERN.fullmatch(value.lower()):
        raise ArtifactError("head SHA must contain 40 or 64 hexadecimal characters")


def _validate_package_version(value: str) -> None:
    if not _VERSION_PATTERN.fullmatch(value):
        raise ArtifactError("package version must use semantic version form")


def _validate_digest(value: object) -> None:
    if not isinstance(value, str) or len(value) != 64 or not _SHA_PATTERN.fullmatch(value.lower()):
        raise ArtifactError("artifact digest must contain 64 hexadecimal characters")


def _validate_id(value: str, label: str) -> None:
    if not _ID_PATTERN.fullmatch(value):
        raise ArtifactError(f"{label} contains unsupported characters")


def _validate_filename(value: str) -> None:
    if not value or value in {".", ".."} or Path(value).name != value or "\\" in value:
        raise ArtifactError("artifact filename must be a plain basename")


def _validate_artifact_filename(
    artifact_type: str,
    filename: str,
    package_version: str,
) -> None:
    if artifact_type == "python_wheel":
        component = r"[0-9A-Za-z_.]+"
        build = r"(?:-[0-9][0-9A-Za-z_]*)?"
        pattern = rf"unity_biome_mcp-{re.escape(package_version)}{build}-{component}-{component}-{component}\.whl"
        if re.fullmatch(pattern, filename) is None:
            raise ArtifactError("python_wheel artifact filename does not match package identity")
        return
    if artifact_type == "unity_upm":
        if filename != f"unity-biome-mcp-{package_version}.tgz":
            raise ArtifactError("unity_upm artifact filename does not match package identity")
        return
    raise ArtifactError(f"unsupported release artifact type: {artifact_type}")


def _validate_package_archive(artifact_type: str, snapshot: bytes, filename: str, package_version: str) -> None:
    try:
        validate_package_archive(artifact_type, snapshot, filename, package_version)
    except PackageArchiveError as exc:
        raise ArtifactError(str(exc)) from exc


def _read_artifact_snapshot(path: Path) -> bytes:
    try:
        with path.open("rb") as stream:
            opened = path.lstat()
            try:
                descriptor = os.fstat(stream.fileno())
            except OSError as exc:
                raise ArtifactError(f"artifact descriptor cannot be inspected: {path.name}") from exc
            if (
                not stat.S_ISREG(opened.st_mode)
                or not stat.S_ISREG(descriptor.st_mode)
                or (opened.st_dev, opened.st_ino) != (descriptor.st_dev, descriptor.st_ino)
            ):
                raise ArtifactError(f"artifact is not a stable regular file: {path.name}")
            snapshot = stream.read(_MAX_ARTIFACT_BYTES + 1)
    except OSError as exc:
        raise ArtifactError(f"artifact is not readable: {path.name}") from exc
    if len(snapshot) > _MAX_ARTIFACT_BYTES:
        raise ArtifactError(f"artifact exceeds {_MAX_ARTIFACT_BYTES} byte safety limit: {path.name}")
    return snapshot


def _verified_artifact_root(directory: Path) -> Path:
    try:
        metadata = directory.lstat()
        resolved = directory.resolve(strict=True)
    except OSError as exc:
        raise ArtifactError("artifact root is not accessible") from exc
    if not stat.S_ISDIR(metadata.st_mode):
        raise ArtifactError("artifact root must be a real directory")
    return resolved


def _verify_artifact_path(path: Path, root: Path) -> None:
    try:
        metadata = path.lstat()
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise ArtifactError(f"artifact is not a regular file: {path.name}") from exc
    if not stat.S_ISREG(metadata.st_mode) or resolved.parent != root:
        raise ArtifactError(f"artifact escapes its staging root or is not a regular file: {path.name}")
