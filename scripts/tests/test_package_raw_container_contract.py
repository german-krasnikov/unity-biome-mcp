"""Raw-byte release archive contracts missed by high-level readers."""

from __future__ import annotations

import gzip
import io
import json
import struct
import sys
import tarfile
import tracemalloc
import zipfile
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet.archive_containers import (  # noqa: E402
    _validate_tar_termination,
    decompress_strict_gzip_tar,
    validate_zip_framing,
)
from gauntlet.package_archives import inspect_package_archive  # noqa: E402
from gauntlet.package_contracts import PackageArchiveError  # noqa: E402
from gauntlet.package_paths import validate_member_tree  # noqa: E402
from gauntlet_test_fixtures import write_wheel  # noqa: E402

_EOCD = b"PK\x05\x06"
_CENTRAL = b"PK\x01\x02"
_TAR_BLOCK = 512
_NPM_MEMBER_MTIME = 499_162_500


def _wheel_snapshot(tmp_path: Path) -> bytes:
    path = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    write_wheel(path)
    return path.read_bytes()


def _eocd(snapshot: bytes | bytearray) -> int:
    offset = snapshot.rfind(_EOCD)
    assert offset >= 0
    return offset


def _central_offsets(snapshot: bytes | bytearray) -> tuple[int, ...]:
    eocd = _eocd(snapshot)
    count = struct.unpack_from("<H", snapshot, eocd + 10)[0]
    offset = struct.unpack_from("<L", snapshot, eocd + 16)[0]
    result = []
    for _ in range(count):
        assert snapshot[offset : offset + 4] == _CENTRAL
        result.append(offset)
        name, extra, comment = struct.unpack_from("<3H", snapshot, offset + 28)
        offset += 46 + name + extra + comment
    return tuple(result)


def _insert_zip_bytes(snapshot: bytes, offset: int, payload: bytes) -> bytes:
    old_eocd = _eocd(snapshot)
    old_directory = struct.unpack_from("<L", snapshot, old_eocd + 16)[0]
    shifted = bytearray(snapshot[:offset] + payload + snapshot[offset:])
    delta = len(payload)
    new_eocd = old_eocd + delta
    struct.pack_into("<L", shifted, new_eocd + 16, old_directory + delta)
    for central in _central_offsets(shifted):
        local = struct.unpack_from("<L", shifted, central + 42)[0]
        if local >= offset:
            struct.pack_into("<L", shifted, central + 42, local + delta)
    return bytes(shifted)


def _with_local_only_extra(snapshot: bytes) -> bytes:
    name_size, extra_size = struct.unpack_from("<2H", snapshot, 26)
    insert_at = 30 + name_size + extra_size
    extra = b"\xfe\xca\x00\x00"
    mutated = bytearray(snapshot)
    struct.pack_into("<H", mutated, 28, extra_size + len(extra))
    return _insert_zip_bytes(bytes(mutated), insert_at, extra)


def _gzip_tar(payload: bytes) -> bytes:
    output = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=output, mtime=0) as stream:
        stream.write(payload)
    result = output.getvalue()
    assert result[:10] == b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff"
    return result


def _upm_tar(*, mtime: int = _NPM_MEMBER_MTIME) -> bytes:
    data = json.dumps(
        {"name": "com.unity-biome-mcp.editor", "version": "1.27.0"}
    ).encode()
    info = tarfile.TarInfo("package/package.json")
    info.size = len(data)
    info.mode = 0o644
    info.mtime = mtime
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        archive.addfile(info, io.BytesIO(data))
    return output.getvalue()


def _patch_tar_header(payload: bytes, offset: int, value: bytes) -> bytes:
    result = bytearray(payload)
    result[offset : offset + len(value)] = value
    result[148:156] = b"        "
    checksum = sum(result[:_TAR_BLOCK])
    result[148:156] = f"{checksum:06o}\0 ".encode("ascii")
    return bytes(result)


def _assert_high_level_zip_accepts(snapshot: bytes) -> None:
    with zipfile.ZipFile(io.BytesIO(snapshot)) as archive:
        assert archive.testzip() is None


def test_zip_rejects_local_extra_missing_from_central_directory(tmp_path: Path) -> None:
    snapshot = _with_local_only_extra(_wheel_snapshot(tmp_path))
    _assert_high_level_zip_accepts(snapshot)

    with pytest.raises(PackageArchiveError, match="extra|local|ZIP"):
        validate_zip_framing(snapshot)


def test_zip_rejects_unreferenced_gap_before_central_directory(tmp_path: Path) -> None:
    snapshot = _wheel_snapshot(tmp_path)
    directory = struct.unpack_from("<L", snapshot, _eocd(snapshot) + 16)[0]
    mutated = _insert_zip_bytes(snapshot, directory, b"UNREFERENCED-GAP")
    _assert_high_level_zip_accepts(mutated)

    with pytest.raises(PackageArchiveError, match="gap|span|framing|ZIP"):
        validate_zip_framing(mutated)


def test_zip_rejects_prefix_even_when_all_offsets_are_coherent(tmp_path: Path) -> None:
    mutated = _insert_zip_bytes(_wheel_snapshot(tmp_path), 0, b"COHERENT-PREFIX")
    _assert_high_level_zip_accepts(mutated)

    with pytest.raises(PackageArchiveError, match="before|prefix|framing|ZIP"):
        validate_zip_framing(mutated)


@pytest.mark.parametrize(
    ("offset", "replacement", "field"),
    ((4, b"\x01\x00\x00\x00", "MTIME"), (8, b"\x00", "XFL"), (9, b"\x03", "OS")),
)
def test_gzip_rejects_noncanonical_header_field(
    offset: int,
    replacement: bytes,
    field: str,
) -> None:
    snapshot = bytearray(_gzip_tar(_upm_tar()))
    snapshot[offset : offset + len(replacement)] = replacement

    with pytest.raises(PackageArchiveError, match="gzip.*canonical"):
        decompress_strict_gzip_tar(bytes(snapshot))


@pytest.mark.parametrize(
    ("offset", "replacement", "field"),
    (
        (157, b"hidden-target\0", "linkname"),
        (329, b"0000001\0", "devmajor"),
        (337, b"0000001\0", "devminor"),
        (500, b"X", "reserved"),
    ),
)
def test_tar_rejects_noncanonical_header_region(
    offset: int,
    replacement: bytes,
    field: str,
) -> None:
    snapshot = _gzip_tar(_patch_tar_header(_upm_tar(), offset, replacement))

    with pytest.raises(PackageArchiveError, match="tar.*canonical|metadata"):
        decompress_strict_gzip_tar(snapshot)


def test_tar_rejects_nonzero_file_data_padding() -> None:
    payload = bytearray(_upm_tar())
    size = int(payload[124:136].rstrip(b"\0 "), 8)
    payload[_TAR_BLOCK + size] = 1

    with pytest.raises(PackageArchiveError, match="padding|framing|canonical"):
        decompress_strict_gzip_tar(_gzip_tar(bytes(payload)))


def test_upm_accepts_real_npm_canonical_member_mtime() -> None:
    snapshot = _gzip_tar(_upm_tar(mtime=_NPM_MEMBER_MTIME))

    identity = inspect_package_archive(
        "unity_editor_upm",
        snapshot,
        "com.unity-biome-mcp.editor-1.27.0.tgz",
    )

    assert identity.package_version == "1.27.0"


def test_upm_rejects_synthetic_zero_member_mtime() -> None:
    snapshot = _gzip_tar(_upm_tar(mtime=0))

    with pytest.raises(PackageArchiveError, match="mtime.*canonical|metadata"):
        inspect_package_archive(
            "unity_editor_upm",
            snapshot,
            "com.unity-biome-mcp.editor-1.27.0.tgz",
        )


@pytest.mark.parametrize("name", ("COM¹", "LPT²", "CONIN$", "CONOUT$"))
def test_member_tree_rejects_portable_windows_reserved_alias(name: str) -> None:
    with pytest.raises(PackageArchiveError, match="portable"):
        validate_member_tree(((f"package/{name}.txt", False),), label="archive")


def test_tar_termination_scan_has_bounded_auxiliary_memory() -> None:
    payload = b"\0" * (8 * 1024 * 1024)
    tracemalloc.start()
    try:
        _validate_tar_termination(payload)
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()

    assert peak < 1_000_000
