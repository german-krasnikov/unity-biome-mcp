"""Same-user process inventory for fail-closed residual detection."""

from __future__ import annotations

import ctypes
import os
import sys
from dataclasses import dataclass
from pathlib import Path

_MAX_ENVIRONMENT_BYTES = 1024 * 1024


class ProcessInventoryError(RuntimeError):
    """Raised when the residual-process oracle is unavailable."""


@dataclass(frozen=True, order=True, slots=True)
class ProcessIdentity:
    """PID plus an OS start token, resilient to ordinary PID reuse."""

    pid: int
    start_token: str


def capture_process_inventory() -> frozenset[ProcessIdentity]:
    """Capture all observable processes owned by the current effective user."""
    if sys.platform == "darwin":
        return _capture_darwin()
    if sys.platform.startswith("linux"):
        return _capture_linux()
    raise ProcessInventoryError("same-user process inventory is unsupported on this platform")


def processes_with_environment(
    identities: set[ProcessIdentity],
    name: str,
    value: str,
) -> frozenset[ProcessIdentity]:
    """Filter identities by one exact inherited environment entry."""
    expected = f"{name}={value}".encode()
    matched: set[ProcessIdentity] = set()
    for identity in identities:
        payload = _process_environment(identity)
        if payload is not None and expected in payload.split(b"\0"):
            matched.add(identity)
    return frozenset(matched)


class _DarwinBsdInfo(ctypes.Structure):
    _fields_ = [
        ("flags", ctypes.c_uint32),
        ("status", ctypes.c_uint32),
        ("xstatus", ctypes.c_uint32),
        ("pid", ctypes.c_uint32),
        ("ppid", ctypes.c_uint32),
        ("uid", ctypes.c_uint32),
        ("gid", ctypes.c_uint32),
        ("ruid", ctypes.c_uint32),
        ("rgid", ctypes.c_uint32),
        ("svuid", ctypes.c_uint32),
        ("svgid", ctypes.c_uint32),
        ("reserved", ctypes.c_uint32),
        ("command", ctypes.c_char * 16),
        ("name", ctypes.c_char * 32),
        ("open_files", ctypes.c_uint32),
        ("process_group", ctypes.c_uint32),
        ("job_control", ctypes.c_uint32),
        ("tty_device", ctypes.c_uint32),
        ("tty_process_group", ctypes.c_uint32),
        ("nice", ctypes.c_int32),
        ("start_seconds", ctypes.c_uint64),
        ("start_microseconds", ctypes.c_uint64),
    ]


def _capture_darwin() -> frozenset[ProcessIdentity]:
    try:
        library = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
        library.proc_listallpids.argtypes = [ctypes.c_void_p, ctypes.c_int]
        library.proc_listallpids.restype = ctypes.c_int
        library.proc_pidinfo.argtypes = [
            ctypes.c_int,
            ctypes.c_int,
            ctypes.c_uint64,
            ctypes.c_void_p,
            ctypes.c_int,
        ]
        library.proc_pidinfo.restype = ctypes.c_int
        capacity = max(library.proc_listallpids(None, 0) * 2, 256)
        pids = (ctypes.c_int * capacity)()
        count = library.proc_listallpids(pids, ctypes.sizeof(pids))
    except (AttributeError, OSError) as exc:
        raise ProcessInventoryError("Darwin process inventory is unavailable") from exc
    if count <= 0 or count > capacity:
        raise ProcessInventoryError("Darwin process inventory returned an invalid count")
    current_uid = os.geteuid()
    identities: set[ProcessIdentity] = set()
    for pid in pids[:count]:
        info = _DarwinBsdInfo()
        size = library.proc_pidinfo(pid, 3, 0, ctypes.byref(info), ctypes.sizeof(info))
        if size != ctypes.sizeof(info) or info.uid != current_uid:
            continue
        token = f"{info.start_seconds}:{info.start_microseconds}"
        identities.add(ProcessIdentity(pid, token))
    if not any(identity.pid == os.getpid() for identity in identities):
        raise ProcessInventoryError("Darwin inventory omitted the supervising process")
    return frozenset(identities)


def _process_environment(identity: ProcessIdentity) -> bytes | None:
    if sys.platform == "darwin":
        return _darwin_environment(identity)
    if sys.platform.startswith("linux"):
        return _linux_environment(identity)
    raise ProcessInventoryError("process environment inspection is unsupported")


def _darwin_environment(identity: ProcessIdentity) -> bytes | None:
    try:
        library = ctypes.CDLL(None, use_errno=True)
        mib = (ctypes.c_int * 3)(1, 49, identity.pid)
        size = ctypes.c_size_t()
        if library.sysctl(mib, 3, None, ctypes.byref(size), None, 0) != 0:
            return None
        if size.value <= 0 or size.value > _MAX_ENVIRONMENT_BYTES:
            return None
        payload = ctypes.create_string_buffer(size.value)
        if library.sysctl(mib, 3, payload, ctypes.byref(size), None, 0) != 0:
            return None
        if _darwin_identity(identity.pid) != identity:
            return None
        return payload.raw[: size.value]
    except (AttributeError, OSError):
        return None


def _darwin_identity(pid: int) -> ProcessIdentity | None:
    try:
        library = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
        info = _DarwinBsdInfo()
        size = library.proc_pidinfo(pid, 3, 0, ctypes.byref(info), ctypes.sizeof(info))
    except (AttributeError, OSError):
        return None
    if size != ctypes.sizeof(info) or info.uid != os.geteuid():
        return None
    return ProcessIdentity(pid, f"{info.start_seconds}:{info.start_microseconds}")


def _capture_linux() -> frozenset[ProcessIdentity]:
    proc = Path("/proc")
    if not proc.is_dir():
        raise ProcessInventoryError("Linux process inventory requires /proc")
    current_uid = os.geteuid()
    identities: set[ProcessIdentity] = set()
    try:
        entries = tuple(proc.iterdir())
    except OSError as exc:
        raise ProcessInventoryError("Linux process inventory cannot list /proc") from exc
    for entry in entries:
        if not entry.name.isdecimal():
            continue
        try:
            if entry.stat().st_uid != current_uid:
                continue
            fields = (entry / "stat").read_bytes().rsplit(b")", 1)[1].split()
            start_ticks = fields[19].decode("ascii")
            identities.add(ProcessIdentity(int(entry.name), start_ticks))
        except (IndexError, OSError, UnicodeError, ValueError):
            continue
    if not any(identity.pid == os.getpid() for identity in identities):
        raise ProcessInventoryError("Linux inventory omitted the supervising process")
    return frozenset(identities)


def _linux_environment(identity: ProcessIdentity) -> bytes | None:
    process = Path("/proc") / str(identity.pid)
    try:
        if _linux_identity(process) != identity:
            return None
        with (process / "environ").open("rb") as stream:
            payload = stream.read(_MAX_ENVIRONMENT_BYTES + 1)
        if len(payload) > _MAX_ENVIRONMENT_BYTES or _linux_identity(process) != identity:
            return None
        return payload
    except OSError:
        return None


def _linux_identity(process: Path) -> ProcessIdentity | None:
    try:
        if process.stat().st_uid != os.geteuid():
            return None
        fields = (process / "stat").read_bytes().rsplit(b")", 1)[1].split()
        return ProcessIdentity(int(process.name), fields[19].decode("ascii"))
    except (IndexError, OSError, UnicodeError, ValueError):
        return None
