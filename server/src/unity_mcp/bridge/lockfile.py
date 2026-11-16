"""PID lockfile — per-session presence file, no SIGTERM."""
import logging
import os
from pathlib import Path

from biome_mcp.lockfile import (
    _lock_nb,  # noqa: F401  # re-exported; used by tests/config/test_lockfile.py
    _unlock,  # noqa: F401  # re-exported; used by tests/config/test_lockfile.py
    is_pid_alive,
    read_lock_metadata,
)
from biome_mcp.lockfile import (
    acquire_lock as _biome_acquire_lock,
)

from ..constants import DEFAULT_PORT
from ..paths import iter_port_files as _iter_port_files
from ..paths import ports_dir as _ports_dir
from ..paths import unity_mcp_dir

log = logging.getLogger("unity_mcp.lockfile")

_MAX_PORT_FILE_BYTES = 8 * 1024
_MAX_PORT_TEXT_BYTES = 64
_MAX_PROJECT_PATH_BYTES = 8192


def _read_ppid_from_lock_path(lock_path: Path) -> int | None:
    """Read ppid from lockfile metadata. None if absent or unreadable."""
    try:
        fd = os.open(str(lock_path), os.O_RDONLY)
        try:
            meta = read_lock_metadata(fd)
            return meta.get("ppid") if meta else None
        finally:
            os.close(fd)
    except (OSError, ValueError):
        return None


def _read_pid_from_fd(fd: int) -> int | None:
    """Read PID from bytes 0-31. Always readable — outside the locked region."""
    os.lseek(fd, 0, os.SEEK_SET)
    data = os.read(fd, 32).decode(errors="ignore").strip()
    try:
        return int(data)
    except ValueError:
        return None


def _read_port_file_lines(path: Path, max_lines: int = 3) -> list[str]:
    """Read a bounded head of a port file (defense against oversized inputs).

    This parser preserves empty lines in their original positions so downstream
    `project_path` / `project` extraction remains index-stable.
    """
    try:
        file_size = path.stat().st_size
    except OSError:
        return []
    if file_size > _MAX_PORT_FILE_BYTES:
        return []

    try:
        with path.open("rb") as fd:
            data = fd.read(_MAX_PORT_FILE_BYTES)
    except OSError:
        return []
    if not data:
        return []

    # Preserve empty lines for index-stable access. Split at line boundaries only.
    raw_lines = data.split(b"\n")
    if max_lines > 0:
        raw_lines = raw_lines[:max_lines]

    out = []
    for idx, raw_line in enumerate(raw_lines):
        if idx == 0 and len(raw_line) > _MAX_PORT_TEXT_BYTES:
            return []
        if idx > 0 and len(raw_line) > _MAX_PROJECT_PATH_BYTES:
            return []
        out.append(raw_line.decode("utf-8", errors="replace").rstrip("\r"))
    return out


def acquire_lock(lock_dir=None, port: int = DEFAULT_PORT,
                 metadata: dict | None = None) -> int:
    """Create a per-PID presence file and take exclusive flock on it.

    Thin wrapper around biome_mcp.lockfile.acquire_lock that fills in the
    Unity-specific default lock_dir when none is supplied.
    """
    if lock_dir is None:
        lock_dir = unity_mcp_dir()
    return _biome_acquire_lock(lock_dir, port=port, metadata=metadata)


def _canonical_project_path(value: str | Path) -> str:
    """Return a comparison-safe project path without requiring it to exist."""
    return os.path.normcase(os.path.realpath(os.path.abspath(os.fspath(value))))


def read_pid_from_port_file(
    port: int,
    project_path: str | Path | None = None,
) -> int | None:
    """Read Unity PID from port files matching the given port.

    Checks both ~/.unity-biome-mcp/ports and legacy ~/.unity-mcp/ports.
    Skips dead PIDs to avoid false-positive process-dead signals. When
    project_path is supplied, only a discovery record for that canonical
    project may identify the process. This prevents a reused port owned by a
    different live Editor from taking over a pinned bridge.
    """
    expected_project = (
        _canonical_project_path(project_path) if project_path is not None else None
    )
    candidates: list[tuple[float, int]] = []
    for f in _iter_port_files("*.port", _ports_dir()):
        try:
            # New format: stem=port, content[0]=pid, content[1]=project_path
            if int(f.stem) != port:
                continue
            lines = _read_port_file_lines(f, max_lines=2)
            if not lines:
                continue
            pid = int(lines[0])
            if expected_project is not None:
                if len(lines) < 2:
                    continue
                project_line = lines[1].strip()
                if not project_line or _canonical_project_path(project_line) != expected_project:
                    continue
            if is_pid_alive(pid):
                candidates.append((f.stat().st_mtime, pid))
        except (ValueError, IndexError, OSError):
            continue
    if not candidates:
        return None
    candidates.sort(reverse=True)
    return candidates[0][1]


def read_port_for_pid(
    pid: int,
    project_path: str | Path | None = None,
) -> int | None:
    """Read the current port for a given PID directly from its port file.

    pid->port mirror of read_pid_from_port_file's port->pid lookup. Iterates
    *.port files and matches by content[0]==pid (new format: stem=port,
    content[0]=pid). Returns None if the pid is dead, no matching file exists,
    or project_path is supplied but doesn't match line 2.
    """
    if not is_pid_alive(pid):
        return None
    expected_project = (
        _canonical_project_path(project_path) if project_path is not None else None
    )
    for f in _iter_port_files("*.port", _ports_dir()):
        try:
            lines = _read_port_file_lines(f, max_lines=2)
            if not lines or int(lines[0]) != pid:
                continue
            port = int(f.stem)
        except (ValueError, IndexError, OSError):
            continue
        if expected_project is not None:
            if len(lines) < 2:
                continue
            project_line = lines[1].strip()
            if not project_line or _canonical_project_path(project_line) != expected_project:
                continue
        return port
    return None


def read_reload_port() -> int | None:
    """Discover reload mini-server port from ~/.unity-biome-mcp/ports/{port}.reload-port."""
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return None

    candidates = []
    for f in ports_dir.glob("*.reload-port"):
        try:
            port = int(f.stem)
            lines = _read_port_file_lines(f, max_lines=2)
            if not lines:
                continue
            pid = int(lines[0])
            if not is_pid_alive(pid):
                continue
            project_path = lines[1].strip() if len(lines) > 1 else ""
            candidates.append((f.stat().st_mtime, port, project_path))
        except (ValueError, OSError):
            continue

    if not candidates:
        return None

    if len(candidates) == 1:
        return candidates[0][1]

    cwd = os.getcwd()
    cwd_matches = [
        (len(pp), mtime, port)
        for mtime, port, pp in candidates
        if pp and (cwd == pp or cwd.startswith(pp + os.sep))
    ]
    if cwd_matches:
        cwd_matches.sort(reverse=True)
        return cwd_matches[0][2]

    candidates.sort(reverse=True)
    return candidates[0][1]


def read_project_path_from_port_file(port: int) -> Path | None:
    """Read Unity project path from port files matching the given port.

    Checks both ~/.unity-biome-mcp/ports and legacy ~/.unity-mcp/ports.
    """
    for f in _iter_port_files("*.port", _ports_dir()):
        try:
            # New format: stem=port, content[0]=pid, content[1]=project_path
            if int(f.stem) != port:
                continue
            lines = _read_port_file_lines(f, max_lines=2)
            if len(lines) < 2:
                continue
            pid = int(lines[0])
            if not is_pid_alive(pid):
                continue
            project_path = lines[1].strip()
            if not project_path:
                continue
            p = Path(project_path)
            if p.exists():
                return p
        except (ValueError, IndexError, OSError):
            continue
    return None


# Back-compat re-export: phantom_cleaner.py now owns stale-file cleanup (this
# module keeps lock acquisition + discovery reads). Existing callers
# (server.py, bridge_heartbeat.py) import these names from here unchanged.
# Lazy (module __getattr__, PEP 562) rather than an eager `from .phantom_cleaner
# import ...`: phantom_cleaner.py itself imports is_pid_alive/_read_port_file_lines
# from this module, so an eager shim here is a genuine bidirectional circular
# import that fails depending on which module loads first. Resolving on first
# attribute access defers past both modules' init, in either order. Returns
# the real function object (getattr), so identity checks against
# phantom_cleaner's own names still hold. Thin shim, not a long-lived
# abstraction — flag for removal once callers migrate.
_PHANTOM_CLEANER_REEXPORTS = frozenset((
    "cleanup_stale_locks", "cleanup_stale_port_files",
    "PORT_SWEEP_INTERVAL_S", "PROBE_GRACE_S",
))


def __getattr__(name: str):
    if name in _PHANTOM_CLEANER_REEXPORTS:
        from . import phantom_cleaner
        return getattr(phantom_cleaner, name)
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")
