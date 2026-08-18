"""Build-once artifact identity manifests for release evidence."""


import hashlib
import re
import stat
from collections.abc import Mapping  # noqa: TC003
from pathlib import Path

from gauntlet.artifact_contracts import (
    ArtifactError,
    ArtifactManifest,
    ArtifactRecord,
    read_stable_artifact,
)
from gauntlet.json_io import JsonFileError, atomic_write_json, load_json_object
from gauntlet.package_archives import (
    inspect_package_archive,
    validate_artifact_filename,
)
from gauntlet.package_contracts import (
    PACKAGE_NAMES,
    SUPPORTED_ARTIFACT_TYPES,
    PackageArchiveError,
    PackageIdentity,
)
from gauntlet.package_versions import is_strict_semver
from gauntlet.receipts import content_hash

_ROOT_KEYS = {"schema_version", "head_sha", "product_version", "artifacts", "manifest_sha"}
_ARTIFACT_KEYS = {
    "type",
    "filename",
    "package_name",
    "package_version",
    "archive_sha256",
    "content_sha256",
    "size_bytes",
}
_PRODUCT_VERSION_TYPES = frozenset({"python_wheel", "unity_editor_upm"})
_SHA_PATTERN = re.compile(r"^[0-9a-f]+$")
_MAX_ARTIFACT_BYTES = 256 * 1024 * 1024


def build_artifact_manifest(
    head_sha: str,
    product_version: str,
    artifacts: Mapping[str, Path],
) -> ArtifactManifest:
    _validate_head_sha(head_sha)
    _validate_version(product_version, "product version")
    _validate_exact_artifact_set(artifacts)
    records = []
    filenames: set[str] = set()
    for artifact_type, path in artifacts.items():
        if not path.is_file():
            raise ArtifactError(f"artifact is not a regular file: {path.name}")
        filename = path.name
        _validate_filename(filename)
        if filename in filenames:
            raise ArtifactError(f"duplicate filename: {filename}")
        snapshot = _read_artifact_snapshot(path)
        identity = _inspect(artifact_type, snapshot, filename)
        _validate_product_version(artifact_type, identity.package_version, product_version)
        filenames.add(filename)
        records.append(
            ArtifactRecord(
                artifact_type,
                filename,
                identity.package_name,
                identity.package_version,
                hashlib.sha256(snapshot).hexdigest(),
                identity.content_sha256,
                len(snapshot),
            )
        )
    records.sort(key=lambda record: record.artifact_type)
    canonical = tuple(records)
    payload = _manifest_payload(head_sha, product_version, canonical)
    return ArtifactManifest(head_sha, product_version, canonical, content_hash(payload))


def write_artifact_manifest(path: Path, manifest: ArtifactManifest) -> None:
    atomic_write_json(path, _manifest_data(manifest))


def load_artifact_manifest(path: Path) -> ArtifactManifest:
    try:
        data = load_json_object(path)
    except JsonFileError as exc:
        raise ArtifactError(str(exc)) from exc
    if set(data) != _ROOT_KEYS or data.get("schema_version") != 2:
        raise ArtifactError("artifact manifest schema mismatch or unsupported version")
    head_sha = _require_text(data["head_sha"], "head SHA")
    _validate_head_sha(head_sha)
    product_version = _require_text(data["product_version"], "product version")
    _validate_version(product_version, "product version")
    records = _parse_records(data["artifacts"], product_version)
    supplied_sha = data["manifest_sha"]
    _validate_digest(supplied_sha, "manifest")
    if supplied_sha != content_hash(_manifest_payload(head_sha, product_version, records)):
        raise ArtifactError("artifact manifest digest mismatch")
    return ArtifactManifest(head_sha, product_version, records, supplied_sha)


def verify_artifact_files(manifest: ArtifactManifest, directory: Path) -> dict[str, PackageIdentity]:
    _validate_manifest_envelope(manifest)
    root = _verified_artifact_root(directory)
    identities = {}
    for record in manifest.artifacts:
        _validate_record_identity(record, manifest.product_version)
        path = directory / record.filename
        _verify_artifact_path(path, root)
        snapshot = _read_artifact_snapshot(path)
        if len(snapshot) != record.size_bytes:
            raise ArtifactError(f"artifact size mismatch: {record.filename}")
        if hashlib.sha256(snapshot).hexdigest() != record.archive_sha256:
            raise ArtifactError(f"artifact digest mismatch: {record.filename}")
        identity = _inspect(record.artifact_type, snapshot, record.filename)
        if (
            identity.package_name != record.package_name
            or identity.package_version != record.package_version
            or identity.content_sha256 != record.content_sha256
        ):
            raise ArtifactError(f"artifact package or content identity mismatch: {record.filename}")
        identities[record.artifact_type] = identity
    return identities


def _validate_manifest_envelope(manifest: ArtifactManifest) -> None:
    _validate_head_sha(manifest.head_sha)
    _validate_version(manifest.product_version, "product version")
    records = manifest.artifacts
    if len(records) != len(SUPPORTED_ARTIFACT_TYPES):
        raise ArtifactError("artifact manifest must contain exactly three records")
    if {record.artifact_type for record in records} != set(SUPPORTED_ARTIFACT_TYPES):
        raise ArtifactError("artifact manifest contains a duplicate or incomplete type set")
    if len({record.filename for record in records}) != len(records):
        raise ArtifactError("artifact manifest contains a duplicate filename")
    for record in records:
        _validate_record_identity(record, manifest.product_version)
    expected = content_hash(_manifest_payload(manifest.head_sha, manifest.product_version, records))
    if manifest.manifest_sha != expected:
        raise ArtifactError("artifact manifest digest does not match its in-memory envelope")


def _parse_records(value: object, product_version: str) -> tuple[ArtifactRecord, ...]:
    if not isinstance(value, list):
        raise ArtifactError("artifacts must be a list")
    records = []
    for raw in value:
        if not isinstance(raw, dict) or set(raw) != _ARTIFACT_KEYS:
            raise ArtifactError("artifact record schema mismatch")
        record = ArtifactRecord(
            _require_text(raw["type"], "artifact type"),
            _require_text(raw["filename"], "artifact filename"),
            _require_text(raw["package_name"], "artifact package name"),
            _require_text(raw["package_version"], "artifact package version"),
            _require_text(raw["archive_sha256"], "artifact archive digest"),
            _require_text(raw["content_sha256"], "artifact content digest"),
            _require_size(raw["size_bytes"]),
        )
        _validate_record_identity(record, product_version)
        records.append(record)
    if len(records) != len(SUPPORTED_ARTIFACT_TYPES):
        raise ArtifactError("artifact records contain a duplicate or incomplete type set")
    _validate_exact_artifact_set({record.artifact_type: Path(record.filename) for record in records})
    if len({record.filename for record in records}) != len(records):
        raise ArtifactError("duplicate artifact filename")
    return tuple(sorted(records, key=lambda record: record.artifact_type))


def _validate_record_identity(record: ArtifactRecord, product_version: str) -> None:
    if record.artifact_type not in SUPPORTED_ARTIFACT_TYPES:
        raise ArtifactError(f"unsupported release artifact type: {record.artifact_type}")
    _validate_filename(record.filename)
    _validate_version(record.package_version, "artifact package version")
    _validate_digest(record.archive_sha256, "artifact archive")
    _validate_digest(record.content_sha256, "artifact content")
    if record.package_name != PACKAGE_NAMES[record.artifact_type]:
        raise ArtifactError("artifact package name does not match its semantic type")
    _validate_product_version(record.artifact_type, record.package_version, product_version)
    try:
        validate_artifact_filename(record.artifact_type, record.filename, record.package_version)
    except PackageArchiveError as exc:
        raise ArtifactError(str(exc)) from exc


def _manifest_payload(
    head_sha: str,
    product_version: str,
    records: tuple[ArtifactRecord, ...],
) -> dict[str, object]:
    return {
        "schema_version": 2,
        "head_sha": head_sha,
        "product_version": product_version,
        "artifacts": [
            {
                "type": record.artifact_type,
                "filename": record.filename,
                "package_name": record.package_name,
                "package_version": record.package_version,
                "archive_sha256": record.archive_sha256,
                "content_sha256": record.content_sha256,
                "size_bytes": record.size_bytes,
            }
            for record in records
        ],
    }


def _manifest_data(manifest: ArtifactManifest) -> dict[str, object]:
    return {**_manifest_payload(manifest.head_sha, manifest.product_version, manifest.artifacts), "manifest_sha": manifest.manifest_sha}


def _inspect(artifact_type: str, snapshot: bytes, filename: str):
    try:
        return inspect_package_archive(artifact_type, snapshot, filename)
    except PackageArchiveError as exc:
        raise ArtifactError(str(exc)) from exc


def _validate_exact_artifact_set(artifacts: Mapping[str, object]) -> None:
    if set(artifacts) != set(SUPPORTED_ARTIFACT_TYPES) or len(artifacts) != 3:
        raise ArtifactError("artifact set must contain exactly the three supported package types")


def _validate_product_version(artifact_type: str, version: str, product_version: str) -> None:
    if artifact_type in _PRODUCT_VERSION_TYPES and version != product_version:
        raise ArtifactError(f"{artifact_type} embedded package version must be {product_version}")


def _validate_head_sha(value: str) -> None:
    if len(value) not in {40, 64} or not _SHA_PATTERN.fullmatch(value.lower()):
        raise ArtifactError("head SHA must contain 40 or 64 hexadecimal characters")


def _validate_version(value: str, label: str) -> None:
    if not is_strict_semver(value):
        raise ArtifactError(f"{label} must use semantic version form")


def _validate_digest(value: object, label: str) -> None:
    if not isinstance(value, str) or len(value) != 64 or not _SHA_PATTERN.fullmatch(value.lower()):
        raise ArtifactError(f"{label} digest must contain 64 hexadecimal characters")


def _validate_filename(value: str) -> None:
    if not value or value in {".", ".."} or Path(value).name != value or "\\" in value:
        raise ArtifactError("artifact filename must be a plain basename")


def _require_text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise ArtifactError(f"{label} must be a non-empty string")
    return value


def _require_size(value: object) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ArtifactError("artifact size must be a non-negative integer")
    return value


def _read_artifact_snapshot(path: Path) -> bytes:
    return read_stable_artifact(path, _MAX_ARTIFACT_BYTES)


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
