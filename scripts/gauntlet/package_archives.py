"""Fail-closed semantic inspection of staged release package archives."""


import base64
import csv
import io
import tarfile
import zipfile
from email.parser import BytesParser

from gauntlet.archive_containers import decompress_strict_gzip_tar, validate_zip_framing
from gauntlet.json_io import JsonFileError, parse_json_object
from gauntlet.package_archive_members import (
    content_digest,
    read_limited,
    tar_fingerprints,
    validate_tar_members,
    validate_zip_members,
    zip_fingerprints,
)
from gauntlet.package_contracts import (
    PACKAGE_NAMES,
    UPM_FILENAMES,
    MemberFingerprint,
    PackageArchiveError,
    PackageIdentity,
)
from gauntlet.package_versions import is_strict_semver
from gauntlet.python_package_contract import wheel_python_contract
from gauntlet.receipts import content_hash
from gauntlet.wheel_metadata import validate_wheel_metadata

_MAX_METADATA_BYTES = 4 * 1024 * 1024


def inspect_package_archive(
    artifact_type: str,
    snapshot: bytes,
    filename: str,
) -> PackageIdentity:
    if artifact_type == "python_wheel":
        return _inspect_wheel(snapshot, filename)
    if artifact_type in UPM_FILENAMES:
        return _inspect_upm(artifact_type, snapshot, filename)
    raise PackageArchiveError(f"unsupported release artifact type: {artifact_type}")


def validate_artifact_filename(artifact_type: str, filename: str, version: str) -> None:
    if artifact_type == "python_wheel":
        _validate_wheel_filename(filename, version)
        return
    if artifact_type in UPM_FILENAMES:
        if filename != UPM_FILENAMES[artifact_type].format(version=version):
            raise PackageArchiveError(f"{artifact_type} filename does not match package identity")
        return
    raise PackageArchiveError(f"unsupported release artifact type: {artifact_type}")


def _inspect_wheel(snapshot: bytes, filename: str) -> PackageIdentity:
    validate_zip_framing(snapshot)
    try:
        with zipfile.ZipFile(io.BytesIO(snapshot)) as archive:
            if archive.comment:
                raise PackageArchiveError("python wheel archive comment is not allowed")
            members = archive.infolist()
            validate_zip_members(members)
            names = [member.filename for member in members if not member.is_dir()]
            metadata_name = _one_suffix(names, ".dist-info/METADATA", "METADATA")
            if metadata_name.count("/") != 1:
                raise PackageArchiveError("python wheel dist-info must be at archive root")
            dist_info = metadata_name.removesuffix("/METADATA")
            wheel_name = f"{dist_info}/WHEEL"
            record_name = f"{dist_info}/RECORD"
            if names.count(wheel_name) != 1 or names.count(record_name) != 1:
                raise PackageArchiveError("python wheel is missing WHEEL or RECORD metadata")
            entry_points_name = f"{dist_info}/entry_points.txt"
            _validate_wheel_member_set(names, dist_info, entry_points_name)
            fingerprints = zip_fingerprints(archive, members)
            metadata = BytesParser().parsebytes(
                read_limited(archive.open(metadata_name), _MAX_METADATA_BYTES, "wheel METADATA")
            )
            wheel_data = read_limited(
                archive.open(wheel_name),
                _MAX_METADATA_BYTES,
                "wheel WHEEL",
            )
            record_data = read_limited(
                archive.open(record_name),
                _MAX_METADATA_BYTES,
                "wheel RECORD",
            )
            entry_points = (
                read_limited(
                    archive.open(entry_points_name),
                    _MAX_METADATA_BYTES,
                    "wheel entry_points.txt",
                )
                if entry_points_name in names
                else None
            )
    except PackageArchiveError:
        raise
    except (
        OSError,
        EOFError,
        ValueError,
        RuntimeError,
        NotImplementedError,
        zipfile.BadZipFile,
        zipfile.LargeZipFile,
    ) as exc:
        raise PackageArchiveError(f"artifact is not a valid python wheel archive: {filename}") from exc
    validate_wheel_metadata(wheel_data, filename)
    if not any(item.path.startswith("unity_mcp/") for item in fingerprints):
        raise PackageArchiveError("python wheel contains no unity_mcp package files")
    package_name, package_version = _embedded_identity(
        "python_wheel",
        metadata.get_all("Name", []),
        metadata.get_all("Version", []),
    )
    if dist_info != f"unity_biome_mcp-{package_version}.dist-info":
        raise PackageArchiveError("python wheel dist-info identity does not match package")
    validate_artifact_filename("python_wheel", filename, package_version)
    _validate_record(record_data, record_name, fingerprints)
    return PackageIdentity(
        package_name,
        package_version,
        content_digest(
            fingerprints,
            exclude=frozenset({record_name}),
            include_prefix="unity_mcp/",
        ),
        wheel_python_contract(metadata, entry_points),
    )


def _inspect_upm(artifact_type: str, snapshot: bytes, filename: str) -> PackageIdentity:
    tar_snapshot = decompress_strict_gzip_tar(snapshot)
    try:
        with tarfile.open(fileobj=io.BytesIO(tar_snapshot), mode="r:") as archive:
            members = archive.getmembers()
            if archive.pax_headers:
                raise PackageArchiveError("unity UPM global PAX metadata is not allowed")
            validate_tar_members(members, len(snapshot))
            package_members = [member for member in members if member.name == "package/package.json"]
            if len(package_members) != 1 or not package_members[0].isfile():
                raise PackageArchiveError("unity UPM must contain one package/package.json")
            package_stream = archive.extractfile(package_members[0])
            if package_stream is None:
                raise PackageArchiveError("unity UPM package/package.json is not readable")
            package_data = parse_json_object(
                read_limited(package_stream, _MAX_METADATA_BYTES, "unity UPM package.json"),
                source="unity UPM package/package.json",
            )
            fingerprints = tar_fingerprints(archive, members)
    except PackageArchiveError:
        raise
    except JsonFileError as exc:
        raise PackageArchiveError(str(exc)) from exc
    except (OSError, EOFError, ValueError, RuntimeError, tarfile.TarError) as exc:
        raise PackageArchiveError(f"artifact is not a valid unity UPM archive: {filename}") from exc
    package_name, package_version = _embedded_identity(
        artifact_type,
        [package_data.get("name")],
        [package_data.get("version")],
    )
    validate_artifact_filename(artifact_type, filename, package_version)
    return PackageIdentity(
        package_name,
        package_version,
        content_digest(fingerprints, strip_prefix="package/"),
        content_hash(package_data),
    )


def _validate_wheel_member_set(names: list[str], dist_info: str, entry_points: str) -> None:
    allowed_metadata = {
        f"{dist_info}/METADATA",
        f"{dist_info}/WHEEL",
        f"{dist_info}/RECORD",
        entry_points,
    }
    unexpected = [
        name
        for name in names
        if not name.startswith("unity_mcp/") and name not in allowed_metadata
    ]
    if unexpected:
        raise PackageArchiveError("python wheel contains an unreviewed install member")


def _validate_record(
    payload: bytes,
    record_name: str,
    fingerprints: tuple[MemberFingerprint, ...],
) -> None:
    try:
        rows = list(csv.reader(io.StringIO(payload.decode("utf-8"), newline=""), strict=True))
    except (UnicodeDecodeError, csv.Error) as exc:
        raise PackageArchiveError("python wheel RECORD is malformed") from exc
    expected = {item.path: item for item in fingerprints}
    observed: set[str] = set()
    for row in rows:
        if len(row) != 3 or row[0] in observed or row[0] not in expected:
            raise PackageArchiveError("python wheel RECORD contains invalid or duplicate paths")
        observed.add(row[0])
        item = expected[row[0]]
        if row[0] == record_name:
            if row[1:] != ["", ""]:
                raise PackageArchiveError("python wheel RECORD self-entry must omit hash and size")
            continue
        if row[1] != _record_digest(item.sha256) or row[2] != str(item.size):
            raise PackageArchiveError("python wheel RECORD hash or size mismatch")
    if observed != set(expected):
        raise PackageArchiveError("python wheel RECORD does not cover every archive file")


def _record_digest(hex_digest: str) -> str:
    encoded = base64.urlsafe_b64encode(bytes.fromhex(hex_digest)).rstrip(b"=")
    return f"sha256={encoded.decode('ascii')}"


def _embedded_identity(
    artifact_type: str,
    names: list[object],
    versions: list[object],
) -> tuple[str, str]:
    expected_name = PACKAGE_NAMES[artifact_type]
    if names != [expected_name]:
        raise PackageArchiveError(f"{artifact_type} embedded package name must be {expected_name}")
    if (
        len(versions) != 1
        or not is_strict_semver(versions[0])
    ):
        raise PackageArchiveError(f"{artifact_type} embedded package version is invalid")
    return expected_name, versions[0]


def _validate_wheel_filename(filename: str, version: str) -> None:
    import re

    if "-" in version or "+" in version:
        raise PackageArchiveError("python_wheel version is not a canonical PEP 440 wheel version")
    component = r"[0-9A-Za-z_.]+"
    build = r"(?:-[0-9][0-9A-Za-z_]*)?"
    pattern = rf"unity_biome_mcp-{re.escape(version)}{build}-{component}-{component}-{component}\.whl"
    if re.fullmatch(pattern, filename) is None:
        raise PackageArchiveError("python_wheel filename does not match package identity")


def _one_suffix(names: list[str], suffix: str, label: str) -> str:
    matches = [name for name in names if name.endswith(suffix)]
    if len(matches) != 1:
        raise PackageArchiveError(f"python wheel must contain one dist-info/{label}")
    return matches[0]
