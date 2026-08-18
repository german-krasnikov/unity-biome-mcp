"""Archive-member safety checks and normalized logical-content hashing."""


import hashlib
import stat
import zipfile
from typing import TYPE_CHECKING, BinaryIO

from gauntlet.package_contracts import MemberFingerprint, PackageArchiveError
from gauntlet.package_paths import validate_logical_paths, validate_member_tree
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    import tarfile

_MAX_ARCHIVE_MEMBERS = 20_000
_MAX_EXPANDED_BYTES = 1024 * 1024 * 1024
_MAX_COMPRESSION_RATIO = 1_000
_CANONICAL_TAR_MTIME = 499_162_500
_CANONICAL_WHEEL_TIMESTAMP = (2020, 2, 2, 0, 0, 0)


def zip_fingerprints(
    archive: zipfile.ZipFile,
    members: list[zipfile.ZipInfo],
) -> tuple[MemberFingerprint, ...]:
    result = []
    for member in members:
        if member.is_dir():
            continue
        try:
            with archive.open(member) as stream:
                result.append(_fingerprint(member.filename, stream, member.file_size))
        except zipfile.BadZipFile as exc:
            raise PackageArchiveError(f"python wheel has invalid CRC: {member.filename}") from exc
    return tuple(result)


def tar_fingerprints(
    archive: tarfile.TarFile,
    members: list[tarfile.TarInfo],
) -> tuple[MemberFingerprint, ...]:
    result = []
    for member in members:
        if not member.isfile():
            continue
        stream = archive.extractfile(member)
        if stream is None:
            raise PackageArchiveError(f"unity UPM member is not readable: {member.name}")
        result.append(_fingerprint(member.name, stream, member.size))
    return tuple(result)


def content_digest(
    fingerprints: tuple[MemberFingerprint, ...],
    *,
    strip_prefix: str = "",
    exclude: frozenset[str] = frozenset(),
    include_prefix: str = "",
) -> str:
    validate_logical_paths((item.path for item in fingerprints), label="archive content")
    payload = []
    for item in fingerprints:
        if item.path in exclude or (include_prefix and not item.path.startswith(include_prefix)):
            continue
        if strip_prefix and not item.path.startswith(strip_prefix):
            raise PackageArchiveError("archive content path is outside its package root")
        path = item.path.removeprefix(strip_prefix)
        payload.append({"path": path, "sha256": item.sha256, "size_bytes": item.size})
    if not payload:
        raise PackageArchiveError("archive content selection is empty")
    return content_hash(
        {
            "domain": "unity-biome-mcp.logical-package-content.v1",
            "members": sorted(payload, key=lambda item: str(item["path"])),
        }
    )


def read_limited(stream: BinaryIO, limit: int, label: str) -> bytes:
    payload = stream.read(limit + 1)
    if len(payload) > limit:
        raise PackageArchiveError(f"{label} exceeds its safety limit")
    return payload


def validate_zip_members(members: list[zipfile.ZipInfo]) -> None:
    if not members or len(members) > _MAX_ARCHIVE_MEMBERS:
        raise PackageArchiveError("python wheel member count is unsafe")
    names = [member.filename for member in members]
    if len(names) != len(set(names)):
        raise PackageArchiveError("python wheel contains duplicate members")
    validate_member_tree(
        ((member.filename, member.is_dir()) for member in members),
        label="python wheel",
    )
    expanded = 0
    for member in members:
        _validate_member_path(member.filename, "python wheel")
        if stat.S_ISLNK(member.external_attr >> 16):
            raise PackageArchiveError("python wheel contains a symbolic link")
        if member.date_time != _CANONICAL_WHEEL_TIMESTAMP:
            raise PackageArchiveError("python wheel member timestamp is not canonical")
        if member.external_attr != _canonical_zip_external_attr(member):
            raise PackageArchiveError("python wheel member mode is not canonical")
        if member.extra or member.comment:
            raise PackageArchiveError("python wheel member metadata is not canonical")
        expanded += member.file_size
        if member.file_size and member.compress_size == 0:
            raise PackageArchiveError("python wheel member compression is invalid")
        if member.compress_size and member.file_size / member.compress_size > _MAX_COMPRESSION_RATIO:
            raise PackageArchiveError("python wheel compression ratio is unsafe")
    _validate_expanded_size(expanded, "python wheel")


def validate_tar_members(members: list[tarfile.TarInfo], compressed_size: int) -> None:
    if not members or len(members) > _MAX_ARCHIVE_MEMBERS:
        raise PackageArchiveError("unity UPM member count is unsafe")
    names = [member.name for member in members]
    if len(names) != len(set(names)):
        raise PackageArchiveError("unity UPM contains duplicate members")
    validate_member_tree(
        ((member.name, member.isdir()) for member in members),
        label="unity UPM",
    )
    expanded = 0
    for member in members:
        _validate_member_path(member.name, "unity UPM")
        is_root = member.name.rstrip("/") == "package" and member.isdir()
        if (not is_root and not member.name.startswith("package/")) or member.issym() or member.islnk():
            raise PackageArchiveError("unity UPM contains an unsafe member")
        if not (member.isfile() or member.isdir()):
            raise PackageArchiveError("unity UPM contains an unsupported member")
        expected_mode = 0o755 if member.isdir() else 0o644
        if (
            member.mode & 0o777 != expected_mode
            or member.uid != 0
            or member.gid != 0
            or member.uname
            or member.gname
            or member.mtime != _CANONICAL_TAR_MTIME
            or member.pax_headers
        ):
            raise PackageArchiveError("unity UPM member metadata is not canonical")
        expanded += member.size
    _validate_expanded_size(expanded, "unity UPM")
    if compressed_size and expanded / compressed_size > _MAX_COMPRESSION_RATIO:
        raise PackageArchiveError("unity UPM compression ratio is unsafe")


def _fingerprint(path: str, stream: BinaryIO, expected_size: int) -> MemberFingerprint:
    digest = hashlib.sha256()
    size = 0
    while chunk := stream.read(64 * 1024):
        size += len(chunk)
        digest.update(chunk)
    if size != expected_size:
        raise PackageArchiveError(f"archive member size mismatch: {path}")
    return MemberFingerprint(path, size, digest.hexdigest())


def _validate_member_path(value: str, label: str) -> None:
    if value.endswith("//"):
        raise PackageArchiveError(f"{label} contains an unsafe member path")


def _validate_expanded_size(size: int, label: str) -> None:
    if size > _MAX_EXPANDED_BYTES:
        raise PackageArchiveError(f"{label} expanded size is unsafe")


def _canonical_zip_external_attr(member: zipfile.ZipInfo) -> int:
    if member.is_dir():
        return ((stat.S_IFDIR | 0o755) << 16) | 0x10
    if member.filename.startswith("unity_mcp/"):
        return (stat.S_IFREG | 0o644) << 16
    return 0o644 << 16
