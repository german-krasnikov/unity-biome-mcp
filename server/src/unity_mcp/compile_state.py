"""Heuristic detector for Unity C# compile / domain-reload windows.

Pure stdlib, instance-local state. Degrades gracefully: unknown project path =>
is_unity_busy() always False.
"""
import contextlib
import os
import time
from pathlib import Path

from unity_mcp.constants import SESSION_TIMEOUT as _DISCONNECT_WINDOW_S
from unity_mcp.lockfile import is_pid_alive, read_pid_from_port_file, read_project_path_from_port_file
from unity_mcp.unity_state import read_state_for_port

_DEFAULT_REMAINING_S = 5.0  # TODO(FM-26 G16w): derive from real diagnose state, not a fabricated 5.0s default
_MAX_REMAINING_S = 60.0


class CompileStateProbe:
    def __init__(self, unity_project_path: Path | None = None,
                 port: int | None = None) -> None:
        self._path = unity_project_path
        self._port: int | None = port
        self._last_disconnect_ts: float | None = None
        self._compile_elapsed: float | None = None
        self._last_known_duration: float | None = None

    @property
    def has_project(self) -> bool:
        return self._path is not None

    def mark_recompile_issued(self) -> None:
        self._last_disconnect_ts = time.monotonic()

    def is_unity_busy(self) -> bool:
        if self._recent_disconnect():
            return True
        return bool(self._lock_file_exists())

    def is_process_dead(self) -> bool:
        """True when we know the Unity process is dead (PID found but not alive)."""
        if self._port is None:
            return False
        pid = read_pid_from_port_file(self._port)
        return pid is not None and not is_pid_alive(pid)

    def is_startup_in_progress(self) -> bool:
        """True when port is set, state-file is absent, and Unity PID is alive.

        Covers the fresh-project-load window: OnQuit deleted the state-file,
        Unity is still initializing and :port is not yet accepting connections.

        When state file absent + PID alive, probes TCP: if TCP responds, state
        file just failed to write — Unity is ready → return False.
        """
        if self._port is None:
            return False
        if read_state_for_port(self._port) is not None:
            return False
        pid = read_pid_from_port_file(self._port)
        if pid is None or not is_pid_alive(pid):
            return False
        # State absent + PID alive: verify TCP not already responding.
        # If TCP responds, state file failed to write — Unity is ready.
        import socket as _socket
        s = _socket.socket()
        s.settimeout(1.0)
        try:
            s.connect(("127.0.0.1", self._port))
        except OSError:
            return True   # TCP not yet up = genuinely starting
        else:
            return False  # TCP responds = not in startup window
        finally:
            with contextlib.suppress(OSError): s.close()

    def has_strong_busy_signal(self) -> bool:
        """State file (authoritative) → startup window → lock file.

        P6: stale+busy state now consults PID instead of falling through.
        A 120s+ legit reload produces a stale state but the process is alive;
        the old code returned False (lock-file fallback only), causing
        'Unity dead/not responding' on a live compiling process.
        """
        if self._port is not None:
            state = read_state_for_port(self._port)
            if state is not None:
                # Fresh busy state → definitely busy.
                if not state.is_stale and state.is_busy:
                    return True
                # Stale busy state (>120s legit reload) → consult PID + wedge detector.
                # PID dead → not compiling. PID alive + terminal wedge → also not busy
                # (G15: detect_wedge confirms reload failed, don't keep retrying).
                if state.is_stale and state.is_busy:
                    if self.is_process_dead():
                        return False
                    try:
                        from unity_mcp.editor_log import detect_wedge
                        if detect_wedge() is not None:
                            return False  # terminal reload failure → not busy, stop retrying
                    except Exception:
                        pass
                    return True
                # State present but not busy → not busy (fresh=clean or stale=not-busy).
                return False
            if self.is_process_dead():
                return False
            if self.is_startup_in_progress():
                return True
        return self._lock_file_exists()

    def estimated_remaining_s(self) -> float:
        if self._last_known_duration and self._compile_elapsed is not None:
            remaining = self._last_known_duration - self._compile_elapsed
            return min(_MAX_REMAINING_S, max(1.0, remaining))
        return _DEFAULT_REMAINING_S

    def update_compile_info(self, status_str: str) -> None:
        """Parse 'compiling|12.3' or 'idle|8.2' from compile_status response."""
        try:
            parts = status_str.split("|")
            if len(parts) != 2:
                return
            state, val = parts[0], float(parts[1])
            if state == "compiling":
                self._compile_elapsed = val
                self._last_known_duration = max(self._last_known_duration or 0, val)
            elif state == "idle" and val > 0:
                self._last_known_duration = val
                self._compile_elapsed = None
        except (ValueError, IndexError):
            pass

    @staticmethod
    def autodetect_project_path(port: int | None = None) -> Path | None:
        """Resolve Unity project path: UNITY_MCP_PROJECT_PATH env override FIRST, then port-file autodetect."""
        p = os.environ.get("UNITY_MCP_PROJECT_PATH")
        if p:
            path = Path(p)
            if path.exists() and (path / "Library").exists():
                return path
        if port is not None:
            return read_project_path_from_port_file(port)
        return None

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _recent_disconnect(self) -> bool:
        if self._last_disconnect_ts is None:
            return False
        return (time.monotonic() - self._last_disconnect_ts) < _DISCONNECT_WINDOW_S

    def _lock_file_exists(self) -> bool:
        if self._path is None:
            return False
        try:
            return (self._path / "Library" / "BeeDriver" / "Lock").exists()
        except OSError:
            return False
