"""Strict outer-container framing for release archives."""


import struct
import zlib

from gauntlet.package_contracts import PackageArchiveError

_ZIP_EOCD = b"PK\x05\x06"
_ZIP_EOCD_SIZE = 22
_ZIP_MAX_COMMENT = 65_535
_ZIP_LOCAL_SIZE = 30
_ZIP_CENTRAL_SIZE = 46
_TAR_BLOCK_SIZE = 512
_MAX_TAR_BYTES = 1024 * 1024 * 1024
_NPM_TAR_MTIME = 499_162_500
_CANONICAL_GZIP_HEADER = b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x02\xff"


def validate_zip_framing(snapshot: bytes) -> None:
    if not snapshot.startswith(b"PK\x03\x04"):
        raise PackageArchiveError("python wheel has data before its first ZIP record")
    start = max(0, len(snapshot) - _ZIP_EOCD_SIZE - _ZIP_MAX_COMMENT)
    offset = snapshot.rfind(_ZIP_EOCD, start)
    while offset >= start:
        if offset + _ZIP_EOCD_SIZE <= len(snapshot):
            comment_size = struct.unpack_from("<H", snapshot, offset + 20)[0]
            if offset + _ZIP_EOCD_SIZE + comment_size == len(snapshot):
                disk, directory_disk, entries_disk, entries, size, directory_offset = (
                    struct.unpack_from("<4H2L", snapshot, offset + 4)
                )
                if (
                    comment_size
                    or disk
                    or directory_disk
                    or entries_disk != entries
                    or 0xFFFF in {entries_disk, entries}
                    or 0xFFFFFFFF in {size, directory_offset}
                    or directory_offset + size != offset
                ):
                    raise PackageArchiveError("python wheel ZIP framing is not canonical")
                _validate_zip_records(snapshot, directory_offset, size, entries)
                return
        offset = snapshot.rfind(_ZIP_EOCD, start, offset)
    raise PackageArchiveError("python wheel has invalid or trailing container bytes")


def decompress_strict_gzip_tar(snapshot: bytes) -> bytes:
    if len(snapshot) < 10 or snapshot[:10] != _CANONICAL_GZIP_HEADER:
        raise PackageArchiveError("unity UPM gzip metadata is not canonical")
    decompressor = zlib.decompressobj(wbits=16 + zlib.MAX_WBITS)
    try:
        payload = decompressor.decompress(snapshot, _MAX_TAR_BYTES + 1)
        if decompressor.unconsumed_tail or len(payload) > _MAX_TAR_BYTES:
            raise PackageArchiveError("unity UPM expanded tar exceeds its safety limit")
        payload += decompressor.flush()
    except zlib.error as exc:
        raise PackageArchiveError("unity UPM has an invalid gzip container") from exc
    if not decompressor.eof or decompressor.unused_data:
        raise PackageArchiveError("unity UPM has trailing or concatenated gzip data")
    _validate_tar_termination(payload)
    return payload


def _validate_tar_termination(payload: bytes) -> None:
    if not payload or len(payload) % _TAR_BLOCK_SIZE:
        raise PackageArchiveError("unity UPM tar framing is invalid")
    view = memoryview(payload)
    offset = 0
    while offset < len(view):
        header = view[offset : offset + _TAR_BLOCK_SIZE]
        if not any(header):
            _validate_tar_zero_termination(view, offset)
            return
        size = _validate_tar_header(header)
        data_end = offset + _TAR_BLOCK_SIZE + size
        member_end = _tar_block_end(data_end)
        if member_end > len(view):
            raise PackageArchiveError("unity UPM tar member framing is invalid")
        if any(view[data_end:member_end]):
            raise PackageArchiveError("unity UPM tar member padding is not canonical")
        offset = member_end
    raise PackageArchiveError("unity UPM tar terminator is missing")


def _validate_zip_records(
    snapshot: bytes,
    directory_offset: int,
    directory_size: int,
    entry_count: int,
) -> None:
    view = memoryview(snapshot)
    central = directory_offset
    local_end = 0
    for _ in range(entry_count):
        record, central = _read_central_record(view, central)
        local_end = _validate_local_record(view, record, local_end)
    if central != directory_offset + directory_size or local_end != directory_offset:
        raise PackageArchiveError("python wheel ZIP records have an unreferenced gap or span")


def _read_central_record(view: memoryview, offset: int) -> tuple[tuple[object, ...], int]:
    if offset + _ZIP_CENTRAL_SIZE > len(view):
        raise PackageArchiveError("python wheel ZIP central record is truncated")
    fields = struct.unpack_from("<4s6H3L5H2L", view, offset)
    if fields[0] != b"PK\x01\x02":
        raise PackageArchiveError("python wheel ZIP central records are not contiguous")
    name_size, extra_size, comment_size = fields[10:13]
    end = offset + _ZIP_CENTRAL_SIZE + name_size + extra_size + comment_size
    if end > len(view) or extra_size or comment_size or fields[13]:
        raise PackageArchiveError("python wheel ZIP central metadata is not canonical")
    name = bytes(view[offset + _ZIP_CENTRAL_SIZE : offset + _ZIP_CENTRAL_SIZE + name_size])
    return fields[2:10] + (name, fields[16]), end


def _validate_local_record(
    view: memoryview,
    central: tuple[object, ...],
    expected_offset: int,
) -> int:
    local_offset = int(central[-1])
    if local_offset != expected_offset or local_offset + _ZIP_LOCAL_SIZE > len(view):
        raise PackageArchiveError("python wheel ZIP local records have a gap or prefix")
    fields = struct.unpack_from("<4s5H3L2H", view, local_offset)
    if fields[0] != b"PK\x03\x04" or fields[1:9] != central[:8]:
        raise PackageArchiveError("python wheel ZIP local and central records disagree")
    name_size, extra_size = fields[9:11]
    name_start = local_offset + _ZIP_LOCAL_SIZE
    data_start = name_start + name_size + extra_size
    if extra_size or bytes(view[name_start : name_start + name_size]) != central[8]:
        raise PackageArchiveError("python wheel ZIP local metadata is not canonical")
    data_end = data_start + fields[7]
    if data_end > len(view):
        raise PackageArchiveError("python wheel ZIP local data is truncated")
    return data_end


def _validate_tar_zero_termination(view: memoryview, offset: int) -> None:
    second = offset + _TAR_BLOCK_SIZE
    if second + _TAR_BLOCK_SIZE > len(view) or any(view[second : second + _TAR_BLOCK_SIZE]):
        raise PackageArchiveError("unity UPM tar terminator is missing")
    if any(view[second + _TAR_BLOCK_SIZE :]):
        raise PackageArchiveError("unity UPM has data after the tar terminator")


def _validate_tar_header(header: memoryview) -> int:
    if bytes(header[257:265]) != b"ustar\x0000":
        raise PackageArchiveError("unity UPM tar header metadata is not canonical")
    if any(header[157:257]) or any(header[500:512]):
        raise PackageArchiveError("unity UPM tar header metadata is not canonical")
    if _tar_octal(header[329:337], "device") or _tar_octal(header[337:345], "device"):
        raise PackageArchiveError("unity UPM tar device metadata is not canonical")
    expected_checksum = _tar_octal(header[148:156], "checksum")
    actual_checksum = sum(header[:148]) + (8 * ord(" ")) + sum(header[156:])
    if expected_checksum != actual_checksum:
        raise PackageArchiveError("unity UPM tar header checksum is invalid")
    type_flag = bytes(header[156:157])
    if type_flag not in {b"\0", b"0", b"5"}:
        raise PackageArchiveError("unity UPM tar contains extended or unsupported metadata")
    size = _tar_octal(header[124:136], "member size")
    if _tar_octal(header[136:148], "member mtime") != _NPM_TAR_MTIME:
        raise PackageArchiveError("unity UPM tar member mtime is not canonical")
    if type_flag == b"5" and size:
        raise PackageArchiveError("unity UPM directory member has data")
    return size


def _tar_octal(field: memoryview, label: str) -> int:
    payload = bytes(field).strip(b"\0 ") or b"0"
    if any(character not in b"01234567" for character in payload):
        raise PackageArchiveError(f"unity UPM tar {label} is invalid")
    return int(payload, 8)


def _tar_block_end(offset: int) -> int:
    return (offset + _TAR_BLOCK_SIZE - 1) // _TAR_BLOCK_SIZE * _TAR_BLOCK_SIZE
