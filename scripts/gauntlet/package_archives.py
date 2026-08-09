"""Fail-closed semantic validation for staged release package archives."""

from __future__ import annotations

import io
import json
import stat
import tarfile
import zipfile
from email.parser import BytesParser
from pathlib import PurePosixPath

SUPPORTED_ARTIFACT_TYPES = frozenset({"python_wheel", "unity_upm"})
_PACKAGE_NAMES = {
    "python_wheel": "unity-biome-mcp",
    "unity_upm": "com.unity-biome-mcp.editor",
}
_MAX_ARCHIVE_MEMBERS = 20_000
_MAX_EXPANDED_BYTES = 1024 * 1024 * 1024
_MAX_COMPRESSION_RATIO = 1_000


class PackageArchiveError(ValueError):
    """Raised when staged package bytes are unsafe or semantically wrong."""


def validate_package_archive(
    artifact_type: str,
    snapshot: bytes,
    filename: str,
    package_version: str,
) -> None:
    if artifact_type == "python_wheel":
        _validate_wheel(snapshot, filename, package_version)
    elif artifact_type == "unity_upm":
        _validate_upm(snapshot, filename, package_version)
    else:
        raise PackageArchiveError(f"unsupported release artifact type: {artifact_type}")


def _validate_wheel(snapshot: bytes, filename: str, package_version: str) -> None:
    try:
        with zipfile.ZipFile(io.BytesIO(snapshot)) as archive:
            members = archive.infolist()
            _validate_zip_members(members)
            names = [member.filename for member in members]
            metadata_names = [name for name in names if name.endswith(".dist-info/METADATA")]
            if len(metadata_names) != 1:
                raise PackageArchiveError("python wheel must contain one dist-info/METADATA")
            metadata_name = metadata_names[0]
            if metadata_name.count("/") != 1:
                raise PackageArchiveError("python wheel dist-info must be at archive root")
            dist_info = metadata_name.removesuffix("/METADATA")
            wheel_name = f"{dist_info}/WHEEL"
            record_name = f"{dist_info}/RECORD"
            if names.count(wheel_name) != 1 or names.count(record_name) != 1:
                raise PackageArchiveError("python wheel is missing WHEEL or RECORD metadata")
            if not archive.read(wheel_name).strip() or not archive.read(record_name).strip():
                raise PackageArchiveError("python wheel WHEEL and RECORD must not be empty")
            if not any(name.startswith("unity_mcp/") and not name.endswith("/") for name in names):
                raise PackageArchiveError("python wheel contains no unity_mcp package files")
            bad_member = archive.testzip()
            if bad_member is not None:
                raise PackageArchiveError(f"python wheel has invalid CRC: {bad_member}")
            metadata = BytesParser().parsebytes(archive.read(metadata_name))
    except (OSError, zipfile.BadZipFile) as exc:
        raise PackageArchiveError(f"artifact is not a valid python wheel archive: {filename}") from exc
    _validate_embedded(
        "python_wheel",
        metadata.get_all("Name", []),
        metadata.get_all("Version", []),
        package_version,
    )
    if dist_info != f"unity_biome_mcp-{package_version}.dist-info":
        raise PackageArchiveError("python wheel dist-info identity does not match package")


def _validate_upm(snapshot: bytes, filename: str, package_version: str) -> None:
    try:
        with tarfile.open(fileobj=io.BytesIO(snapshot), mode="r:gz") as archive:
            members = archive.getmembers()
            _validate_tar_members(members, len(snapshot))
            package_members = [member for member in members if member.name == "package/package.json"]
            if len(package_members) != 1 or not package_members[0].isfile():
                raise PackageArchiveError("unity UPM must contain one package/package.json")
            stream = archive.extractfile(package_members[0])
            if stream is None:
                raise PackageArchiveError("unity UPM package/package.json is not readable")
            package_data = json.loads(stream.read())
    except (OSError, EOFError, tarfile.TarError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise PackageArchiveError(f"artifact is not a valid unity UPM archive: {filename}") from exc
    if not isinstance(package_data, dict):
        raise PackageArchiveError("unity UPM package.json must be an object")
    _validate_embedded(
        "unity_upm",
        [package_data.get("name")],
        [package_data.get("version")],
        package_version,
    )


def _validate_zip_members(members: list[zipfile.ZipInfo]) -> None:
    if not members or len(members) > _MAX_ARCHIVE_MEMBERS:
        raise PackageArchiveError("python wheel member count is unsafe")
    names = [member.filename for member in members]
    if len(names) != len(set(names)):
        raise PackageArchiveError("python wheel contains duplicate members")
    expanded = 0
    for member in members:
        _validate_member_path(member.filename, "python wheel")
        mode = member.external_attr >> 16
        if stat.S_ISLNK(mode):
            raise PackageArchiveError("python wheel contains a symbolic link")
        expanded += member.file_size
        if member.file_size and member.compress_size == 0:
            raise PackageArchiveError("python wheel member compression is invalid")
        if member.compress_size and member.file_size / member.compress_size > _MAX_COMPRESSION_RATIO:
            raise PackageArchiveError("python wheel compression ratio is unsafe")
    _validate_expanded_size(expanded, "python wheel")


def _validate_tar_members(members: list[tarfile.TarInfo], compressed_size: int) -> None:
    if not members or len(members) > _MAX_ARCHIVE_MEMBERS:
        raise PackageArchiveError("unity UPM member count is unsafe")
    names = [member.name for member in members]
    if len(names) != len(set(names)):
        raise PackageArchiveError("unity UPM contains duplicate members")
    expanded = 0
    for member in members:
        _validate_member_path(member.name, "unity UPM")
        is_root_directory = member.name.rstrip("/") == "package" and member.isdir()
        if (not is_root_directory and not member.name.startswith("package/")) or member.issym() or member.islnk():
            raise PackageArchiveError("unity UPM contains an unsafe member")
        if not (member.isfile() or member.isdir()):
            raise PackageArchiveError("unity UPM contains an unsupported member")
        expanded += member.size
    _validate_expanded_size(expanded, "unity UPM")
    if compressed_size and expanded / compressed_size > _MAX_COMPRESSION_RATIO:
        raise PackageArchiveError("unity UPM compression ratio is unsafe")


def _validate_member_path(value: str, label: str) -> None:
    normalized = value[:-1] if value.endswith("/") else value
    path = PurePosixPath(normalized)
    if (
        not normalized
        or value.endswith("//")
        or path.is_absolute()
        or path.as_posix() != normalized
        or "\\" in value
        or any(part in {"", ".", ".."} for part in path.parts)
    ):
        raise PackageArchiveError(f"{label} contains an unsafe member path")


def _validate_expanded_size(size: int, label: str) -> None:
    if size > _MAX_EXPANDED_BYTES:
        raise PackageArchiveError(f"{label} expanded size is unsafe")


def _validate_embedded(
    artifact_type: str,
    names: list[object],
    versions: list[object],
    package_version: str,
) -> None:
    expected_name = _PACKAGE_NAMES[artifact_type]
    if names != [expected_name]:
        raise PackageArchiveError(f"{artifact_type} embedded package name must be {expected_name}")
    if versions != [package_version]:
        raise PackageArchiveError(f"{artifact_type} embedded package version must be {package_version}")
