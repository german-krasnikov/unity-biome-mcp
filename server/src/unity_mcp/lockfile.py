"""PID lockfile — per-session presence file, no SIGTERM."""
import logging
import os
from .constants import DEFAULT_PORT
import socket
import sys
from pathlib import Path
from typing import Optional

from .paths import ports_dir as _ports_dir, unity_mcp_dir, iter_port_files as _iter_port_files

log = logging.getLogger("unity_mcp.lockfile")

_IS_WIN = sys.platform == "win32"

# Lock sentinel byte lives at offset 1024 — far outside PID data (bytes 0-31).
# On Windows, mandatory locking blocks reads on locked bytes, so the lock region
# MUST NOT overlap PID data that other processes need to read.
_LOCK_OFFSET = 1024

_OPEN_FLAGS = os.O_RDWR | os.O_CREAT | getattr(os, "O_CLOEXEC", 0)

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


def _read_pid_from_fd(fd: int) -> Optional[int]:
    """Read PID from bytes 0-31. Always readable — outside the locked region."""
    os.lseek(fd, 0, os.SEEK_SET)
    data = os.read(fd, 32).decode(errors="ignore").strip()
    try:
        return int(data)
    except ValueError:
        return None


def is_pid_alive(pid: Optional[int]) -> bool:
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


def acquire_lock(lock_dir=None, port: int = DEFAULT_PORT) -> int:
    """Create a per-PID presence file and take exclusive flock on it.

    Each session uses server-{port}-{pid}.lock — multiple sessions coexist.
    Raises RuntimeError if this PID already holds the lock (rapid restart race).
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
        raise RuntimeError(f"Cannot acquire exclusive lock for port {port} (PID {os.getpid()} already holds it)")

    _write_pid(fd)
    _lock_paths[fd] = str(lock_file)
    return fd


def release_lock(fd: int) -> None:
    """Unlock, unlink the presence file, and close the fd."""
    try:
        _unlock(fd)
    except OSError:
        pass
    path = _lock_paths.pop(fd, None)
    if path:
        try:
            os.unlink(path)
        except OSError:
            pass
    os.close(fd)


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


def read_pid_from_port_file(port: int) -> Optional[int]:
    """Read Unity PID from port files matching the given port.

    Checks both ~/.unity-biome-mcp/ports and legacy ~/.unity-mcp/ports.
    Skips dead PIDs to avoid false-positive process-dead signals.
    """
    for f in _iter_port_files("*.port", _ports_dir()):
        try:
            lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
            if int(lines[0]) != port:
                continue
            pid = int(f.stem)
            if is_pid_alive(pid):
                return pid
        except (ValueError, IndexError, OSError):
            continue
    return None


def _tcp_probe(port: int, timeout: float = 0.2) -> bool:
    """Return True if TCP port accepts connections."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout):
            return True
    except OSError:
        return False


def cleanup_stale_port_files(tcp_probe: bool = False) -> int:
    """Delete port files for dead PIDs. Returns count cleaned.

    tcp_probe=True: also removes files where PID is alive but port is not listening
    (catches AssetImportWorker processes that hold a PID but lost the TCP server).
    """
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return 0
    cleaned = 0
    for pattern in ("*.port", "*.chat-port", "*.reload-port"):
        for f in ports_dir.glob(pattern):
            try:
                pid = int(f.stem)
                if not is_pid_alive(pid):
                    f.unlink()
                    cleaned += 1
                    continue
                if tcp_probe:
                    try:
                        content = f.read_text(encoding="utf-8", errors="replace")
                        port = int(content.split("\n")[0])
                        if not _tcp_probe(port):
                            f.unlink()
                            cleaned += 1
                    except (ValueError, OSError):
                        pass
            except (ValueError, OSError):
                continue
    return cleaned


def read_reload_port() -> Optional[int]:
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
            lines = f.read_text(encoding="utf-8").strip().split("\n")
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


def read_project_path_from_port_file(port: int) -> Optional[Path]:
    """Read Unity project path from port files matching the given port.

    Checks both ~/.unity-biome-mcp/ports and legacy ~/.unity-mcp/ports.
    """
    for f in _iter_port_files("*.port", _ports_dir()):
        try:
            pid = int(f.stem)
            lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
            if int(lines[0]) != port or len(lines) < 2:
                continue
            if not is_pid_alive(pid):
                continue
            p = Path(lines[1])
            if p.exists():
                return p
        except (ValueError, IndexError, OSError):
            continue
    return None
