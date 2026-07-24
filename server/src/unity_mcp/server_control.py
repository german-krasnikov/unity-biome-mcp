"""List and stop running Unity Biome MCP server processes via lockfile inspection.

Pure stdlib. No network calls. No wildcard kills.
"""
import logging
import os
import sys
import time
from pathlib import Path
from typing import Optional

from .lockfile import is_pid_alive
from .paths import unity_mcp_dir

log = logging.getLogger("unity_mcp.server_control")

_IS_WIN = sys.platform == "win32"
_DEFAULT_LOCK_DIR = unity_mcp_dir()


def _lock_dir_resolved(lock_dir: Optional[Path]) -> Path:
    return Path(lock_dir) if lock_dir else _DEFAULT_LOCK_DIR


def list_servers(lock_dir: Optional[Path] = None) -> list[dict]:
    """Return info for all live server processes found in lock_dir.

    Each entry: {"port": int, "pid": int, "lock_path": Path}
    Dead PIDs are filtered out. Stale lockfiles left for cleanup_stale_locks().
    """
    d = _lock_dir_resolved(lock_dir)
    if not d.exists():
        return []
    results = []
    for f in d.glob("server-*-*.lock"):
        parts = f.stem.split("-")  # ["server", port, pid]
        if len(parts) != 3:
            continue
        try:
            port_val = int(parts[1])
            pid_val = int(parts[2])
        except ValueError:
            continue
        if is_pid_alive(pid_val):
            results.append({"port": port_val, "pid": pid_val, "lock_path": f})
    return results


def stop_server(
    port: int,
    lock_dir: Optional[Path] = None,
    timeout: float = 10.0,
    _signal_override: Optional[int] = None,
    _kill_fn=None,
) -> bool:
    """Stop the server listening on port. Returns True if stopped cleanly.

    POSIX: SIGTERM -> poll lockfile release -> SIGKILL fallback.
    Windows: taskkill /PID -> poll -> taskkill /F fallback.

    Never signals self (os.getpid()) or parent (os.getppid()).
    Returns False if no server found or port == 0.
    Raises nothing — all errors are logged and treated as "could not stop".
    """
    if not port:
        return False

    d = _lock_dir_resolved(lock_dir)
    self_pid = os.getpid()
    parent_pid = os.getppid()
    safe_pids = {self_pid, parent_pid}

    servers = [s for s in list_servers(lock_dir=d) if s["port"] == port]
    if not servers:
        log.debug("stop_server: no live server found for port %d", port)
        return False

    signaled = False
    for s in servers:
        pid = s["pid"]
        if pid in safe_pids:
            log.warning("stop_server: refusing to signal self/parent PID %d", pid)
            continue
        signaled = True
        _send_stop(pid, s["lock_path"], timeout, _signal_override, _kill_fn)

    if not signaled:
        return False

    # Poll until all non-safe lockfiles for this port are gone
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        still_alive = [
            s for s in list_servers(lock_dir=d)
            if s["port"] == port and s["pid"] not in safe_pids
        ]
        if not still_alive:
            return True
        time.sleep(0.1)
    return False


def _send_stop(pid, lock_path, timeout, signal_override, kill_fn):
    if _IS_WIN:
        _windows_stop(pid, lock_path, timeout, kill_fn)
    else:
        _posix_stop(pid, lock_path, timeout, signal_override, kill_fn)


def _posix_stop(pid, lock_path, timeout, signal_override, kill_fn):
    import signal as _signal
    sig = signal_override if signal_override is not None else _signal.SIGTERM
    _do_kill = kill_fn or os.kill
    try:
        _do_kill(pid, sig)
    except (ProcessLookupError, OSError):
        return  # already gone
    # Poll until lockfile disappears or process dies
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if not lock_path.exists() or not is_pid_alive(pid):
            return
        time.sleep(0.1)
    # SIGKILL fallback
    log.warning("stop_server: SIGTERM timeout for PID %d, sending SIGKILL", pid)
    try:
        _do_kill(pid, _signal.SIGKILL)
    except (ProcessLookupError, OSError):
        pass


def _windows_stop(pid, lock_path, timeout, kill_fn):
    import subprocess
    _run = kill_fn or subprocess.run
    _run(["taskkill", "/PID", str(pid)], capture_output=True)
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if not is_pid_alive(pid):
            return
        time.sleep(0.1)
    log.warning("stop_server: taskkill timeout for PID %d, sending /F", pid)
    _run(["taskkill", "/F", "/PID", str(pid)], capture_output=True)
