"""PID lockfile — per-session presence file, no SIGTERM."""
import contextlib
import json
import logging
import os
import socket
import sys
import time
from pathlib import Path

from .constants import DEFAULT_PORT
from .paths import iter_port_files as _iter_port_files
from .paths import ports_dir as _ports_dir
from .paths import unity_mcp_dir

log = logging.getLogger("unity_mcp.lockfile")

_IS_WIN = sys.platform == "win32"

# Lock sentinel byte lives at offset 1024 — far outside PID data (bytes 0-31).
# On Windows, mandatory locking blocks reads on locked bytes, so the lock region
# MUST NOT overlap PID data that other processes need to read.
_LOCK_OFFSET = 1024

_OPEN_FLAGS = os.O_RDWR | os.O_CREAT | getattr(os, "O_CLOEXEC", 0)
_MAX_PORT_FILE_BYTES = 8 * 1024
_MAX_PORT_TEXT_BYTES = 64
_MAX_PROJECT_PATH_BYTES = 8192

# Maps fd → lock file path for cleanup in release_lock
_lock_paths: dict[int, str] = {}

if _IS_WIN:
    import msvcrt

    def _lock_nb(fd: int) -> None:
        """Non-blocking exclusive lock on sentinel byte at offset 1024."""
        os.lseek(fd, _LOCK_OFFSET, os.SEEK_SET)
        msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)

    def _unlock(fd: int) -> None:
        """Release the sentinel byte lock."""
        os.lseek(fd, _LOCK_OFFSET, os.SEEK_SET)
        msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)

else:
    import fcntl

    def _lock_nb(fd: int) -> None:
        """Non-blocking exclusive flock (advisory, whole-file)."""
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def _unlock(fd: int) -> None:
        fcntl.flock(fd, fcntl.LOCK_UN)


def _write_pid(fd: int) -> None:
    """Write current PID to bytes 0-31 of the lockfile."""
    os.ftruncate(fd, 0)
    os.lseek(fd, 0, os.SEEK_SET)
    os.write(fd, f"{os.getpid()}\n".encode())


def write_lock_metadata(fd: int, metadata: dict) -> None:
    """Append JSON metadata on line 2 of the lockfile (after the PID line)."""
    os.lseek(fd, 0, os.SEEK_SET)
    pid_data = os.read(fd, 64).decode(errors="ignore")
    pid_end = pid_data.find("\n")
    if pid_end < 0:
        pid_end = len(pid_data)
    meta_start = pid_end + 1
    os.lseek(fd, meta_start, os.SEEK_SET)
    payload = (json.dumps(metadata, ensure_ascii=False) + "\n").encode()
    os.write(fd, payload)
    os.ftruncate(fd, meta_start + len(payload))


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


def read_lock_metadata(fd: int) -> dict | None:
    """Read JSON metadata from line 2 of the lockfile. Returns None if absent/corrupt."""
    os.lseek(fd, 0, os.SEEK_SET)
    data = os.read(fd, 1024).decode(errors="ignore")
    lines = data.split("\n")
    if len(lines) < 2 or not lines[1].strip():
        return None
    try:
        return json.loads(lines[1])
    except json.JSONDecodeError:
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


def is_pid_alive(pid: int | None) -> bool:
    """Return True if the process with given PID exists."""
    if pid is None:
        return False
    if sys.platform == "win32":
        import ctypes
        handle = ctypes.windll.kernel32.OpenProcess(0x1000, False, pid)
        if handle:
            ctypes.windll.kernel32.CloseHandle(handle)
            return True
        return False
    try:
        os.kill(pid, 0)
        return True
    except PermissionError:
        return True  # alive but no permission (cross-user on Unix)
    except (OSError, ProcessLookupError):
        return False


def acquire_lock(lock_dir=None, port: int = DEFAULT_PORT,
                 metadata: dict | None = None) -> int:
    """Create a per-PID presence file and take exclusive flock on it.

    Each session uses server-{port}-{pid}.lock — multiple sessions coexist.
    Raises RuntimeError if this PID already holds the lock (rapid restart race).
    metadata: optional dict written as JSON on line 2 via write_lock_metadata.
    """
    if lock_dir is None:
        lock_dir = unity_mcp_dir()
    lock_dir = Path(lock_dir)
    lock_dir.mkdir(parents=True, exist_ok=True)
    lock_file = lock_dir / f"server-{port}-{os.getpid()}.lock"

    fd = os.open(str(lock_file), _OPEN_FLAGS, 0o600)
    try:
        _lock_nb(fd)
    except (BlockingIOError, OSError):
        os.close(fd)
        raise RuntimeError(f"Cannot acquire exclusive lock for port {port} (PID {os.getpid()} already holds it)") from None

    _write_pid(fd)
    write_lock_metadata(fd, {"ppid": os.getppid()})
    if metadata is not None:
        write_lock_metadata(fd, metadata)
    _lock_paths[fd] = str(lock_file)
    return fd


def release_lock(fd: int) -> None:
    """Unlock, unlink the presence file, and close the fd."""
    with contextlib.suppress(OSError):
        _unlock(fd)
    path = _lock_paths.pop(fd, None)
    close_error: OSError | None = None
    try:
        os.close(fd)
    except OSError as exc:
        close_error = exc
    if path:
        with contextlib.suppress(OSError):
            os.unlink(path)
    if close_error is not None:
        raise close_error


def cleanup_stale_locks(port: int, lock_dir: Path = None) -> int:
    """Delete lockfiles for dead PIDs. Returns count cleaned."""
    if lock_dir is None:
        lock_dir = unity_mcp_dir()
    lock_dir = Path(lock_dir)
    if not lock_dir.exists():
        return 0
    cleaned = 0
    for f in lock_dir.glob(f"server-{port}-*.lock"):
        try:
            pid = int(f.stem.rsplit("-", 1)[1])
        except (ValueError, IndexError):
            continue
        if not is_pid_alive(pid):
            try:
                f.unlink()
                cleaned += 1
            except OSError:
                pass
    return cleaned


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
            lines = _read_port_file_lines(f, max_lines=2)
            if len(lines) < 1 or int(lines[0]) != port:
                continue
            if expected_project is not None:
                if len(lines) < 2:
                    continue
                project_line = lines[1].strip()
                if not project_line or _canonical_project_path(project_line) != expected_project:
                    continue
            pid = int(f.stem)
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

    pid->port mirror of read_pid_from_port_file's port->pid lookup. Reads
    {pid}.port by filename (checks both ~/.unity-biome-mcp/ports and legacy
    ~/.unity-mcp/ports via iter_port_files), so a same-process port rebind
    (Unity falling back to a new port after a bind conflict) is visible the
    instant SaveRuntimePorts rewrites the file — no failed TCP connect
    needed to notice it. Returns None if the pid is dead, no matching file
    exists, the port field is not an integer, or project_path is supplied
    but doesn't match line 2 (same semantics as read_pid_from_port_file).
    """
    if not is_pid_alive(pid):
        return None
    expected_project = (
        _canonical_project_path(project_path) if project_path is not None else None
    )
    for f in _iter_port_files(f"{pid}.port", _ports_dir()):
        try:
            lines = _read_port_file_lines(f, max_lines=2)
            if len(lines) < 1:
                return None
            port = int(lines[0])
        except (ValueError, IndexError, OSError):
            return None
        if expected_project is not None:
            if len(lines) < 2:
                return None
            project_line = lines[1].strip()
            if not project_line or _canonical_project_path(project_line) != expected_project:
                return None
        return port
    return None


# Cadence for the idle-heartbeat stale-port sweep (bridge_heartbeat throttles
# _maybe_sweep_stale_ports to at most once per this interval), and therefore
# the minimum real-world spacing between two tcp_probe=True calls from that
# caller. Defined here rather than in bridge_heartbeat.py (which imports
# cleanup_stale_port_files from this module) so PROBE_GRACE_S below can
# derive from it without an import cycle.
PORT_SWEEP_INTERVAL_S: float = 30.0

# C1 #11: a single failed tcp_probe must NOT delete a live-PID port file.
# Unity's own same-port bind-retry loop can leave the port briefly unbound
# while retrying after a bind conflict. Worst case (MCPServer.cs, Windows):
# 6 same-port attempts, linear backoff PortResolver.BackoffDelayMs(i) =
# 600ms * (i + 1) for i in 0..5:
#   sum_{i=0}^{5} 600*(i+1) = 600*(1+2+3+4+5+6) = 600*21 = 12600ms = 12.6s
# PROBE_GRACE_S must clear that window with margin. Reusing the sweep cadence
# means "failure persisted" reads as "still failing on the NEXT sweep, ~30s
# later" instead of inventing a second magic number.
PROBE_GRACE_S: float = PORT_SWEEP_INTERVAL_S  # 30.0s >> 12.6s worst case

# port-file-path (str) -> monotonic time of its first consecutive probe
# failure. Module-level because cleanup_stale_port_files is invoked
# repeatedly (once per idle heartbeat tick) against the same files, so the
# grace window must survive across calls.
_tcp_probe_fail_since: dict[str, float] = {}


def _tcp_probe(port: int, timeout: float = 0.2) -> bool:
    """Return True if TCP port accepts connections."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout):
            return True
    except OSError:
        return False


def cleanup_stale_port_files(tcp_probe: bool = False, now_fn=time.monotonic) -> int:
    """Delete port files for dead PIDs. Returns count cleaned.

    tcp_probe=True: also removes files where PID is alive but port is not
    listening (catches AssetImportWorker processes that hold a PID but lost
    the TCP server). A live-PID file is only deleted once its probe failure
    has PERSISTED for at least PROBE_GRACE_S: a single failed probe can be a
    sibling project's Unity mid-domain-reload or mid-bind-retry rather than a
    truly dead server (see PROBE_GRACE_S for the bind-retry-window
    derivation). A dead PID is still removed immediately, no grace applied.
    now_fn: injectable monotonic clock (default time.monotonic) so grace-period
    tests can advance time deterministically instead of monkeypatching the
    global time module.
    """
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return 0
    if tcp_probe:
        # Entries for files removed by another path (or a previous sweep)
        # must not accumulate forever.
        for key in [k for k in _tcp_probe_fail_since if not Path(k).exists()]:
            _tcp_probe_fail_since.pop(key, None)
    now = now_fn()
    cleaned = 0
    for pattern in ("*.port", "*.chat-port", "*.reload-port"):
        for f in ports_dir.glob(pattern):
            key = str(f)
            try:
                pid = int(f.stem)
                if not is_pid_alive(pid):
                    f.unlink()
                    cleaned += 1
                    _tcp_probe_fail_since.pop(key, None)
                    continue
                if tcp_probe:
                    lines = _read_port_file_lines(f, max_lines=1)
                    if not lines:
                        continue
                    port = int(lines[0])
                    if _tcp_probe(port):
                        _tcp_probe_fail_since.pop(key, None)
                        continue
                    first_fail = _tcp_probe_fail_since.setdefault(key, now)
                    if now - first_fail >= PROBE_GRACE_S:
                        f.unlink()
                        cleaned += 1
                        _tcp_probe_fail_since.pop(key, None)
            except (ValueError, OSError):
                pass
    return cleaned


def read_reload_port() -> int | None:
    """Discover reload mini-server port from ~/.unity-biome-mcp/ports/{pid}.reload-port."""
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return None

    candidates = []
    for f in ports_dir.glob("*.reload-port"):
        try:
            pid = int(f.stem)
            if not is_pid_alive(pid):
                continue
            lines = _read_port_file_lines(f, max_lines=2)
            if not lines:
                continue
            port = int(lines[0])
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
            pid = int(f.stem)
            lines = _read_port_file_lines(f, max_lines=2)
            if len(lines) < 2 or int(lines[0]) != port:
                continue
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
