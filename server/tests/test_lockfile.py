"""TDD tests for lockfile.py — per-PID presence files, no SIGTERM."""
import os
import sys
from pathlib import Path
from unittest.mock import patch

import pytest

if sys.platform == "win32":
    pytest.skip(
        "lockfile tests use fcntl (POSIX-only); Windows lockfile uses msvcrt",
        allow_module_level=True,
    )

import fcntl  # noqa: E402 — must be after the win32 guard

from unity_mcp.lockfile import (
    acquire_lock, release_lock, read_pid_from_port_file, read_port_for_pid,
    is_pid_alive, _lock_nb, _unlock,
)


# ---------------------------------------------------------------------------
# Core: per-PID file creation and release
# ---------------------------------------------------------------------------

def test_acquire_creates_per_pid_file(tmp_path):
    """Filename must include PID: server-{port}-{pid}.lock"""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    expected = tmp_path / f"server-9500-{os.getpid()}.lock"
    assert expected.exists()
    assert int(expected.read_text(encoding="utf-8").splitlines()[0]) == os.getpid()
    release_lock(fd)


def test_release_unlinks_presence_file(tmp_path):
    """After release_lock, the per-PID file is deleted."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_file = tmp_path / f"server-9500-{os.getpid()}.lock"
    assert lock_file.exists()
    release_lock(fd)
    assert not lock_file.exists()


def test_two_sessions_same_port_both_succeed(tmp_path):
    """Two acquires with different mock PIDs don't conflict."""
    pid1, pid2 = 11111, 22222
    with patch("os.getpid", return_value=pid1):
        fd1 = acquire_lock(lock_dir=tmp_path, port=9500)
    with patch("os.getpid", return_value=pid2):
        fd2 = acquire_lock(lock_dir=tmp_path, port=9500)

    assert (tmp_path / f"server-9500-{pid1}.lock").exists()
    assert (tmp_path / f"server-9500-{pid2}.lock").exists()

    release_lock(fd1)
    release_lock(fd2)


def test_acquire_same_pid_twice_raises(tmp_path):
    """Same PID can't acquire same port twice (flock LOCK_EX | LOCK_NB)."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    try:
        with pytest.raises((RuntimeError, BlockingIOError, OSError)):
            acquire_lock(lock_dir=tmp_path, port=9500)
    finally:
        release_lock(fd)


def test_acquire_does_not_kill(tmp_path):
    """acquire_lock never calls os.kill (no SIGTERM behavior)."""
    with patch("os.kill") as mock_kill:
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        release_lock(fd)
    # os.kill(pid, 0) from is_pid_alive is OK but signal.SIGTERM (15) must not be sent
    for call_args in mock_kill.call_args_list:
        sig = call_args[0][1] if len(call_args[0]) > 1 else call_args[1].get("sig")
        assert sig != 15, f"SIGTERM was sent: {call_args}"


def test_acquire_after_release(tmp_path):
    """Can re-acquire the lock for the same PID after releasing."""
    fd1 = acquire_lock(lock_dir=tmp_path, port=9500)
    release_lock(fd1)
    fd2 = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_file = tmp_path / f"server-9500-{os.getpid()}.lock"
    assert int(lock_file.read_text(encoding="utf-8").splitlines()[0]) == os.getpid()
    release_lock(fd2)


def test_different_ports_dont_conflict(tmp_path):
    """Two locks on different ports coexist."""
    pid1, pid2 = 11111, 22222
    with patch("os.getpid", return_value=pid1):
        fd1 = acquire_lock(lock_dir=tmp_path, port=9500)
    with patch("os.getpid", return_value=pid2):
        fd2 = acquire_lock(lock_dir=tmp_path, port=9501)
    assert (tmp_path / f"server-9500-{pid1}.lock").exists()
    assert (tmp_path / f"server-9501-{pid2}.lock").exists()
    release_lock(fd1)
    release_lock(fd2)


def test_lockfile_o_cloexec(tmp_path):
    """O_CLOEXEC must be set on the lock file descriptor."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    try:
        flags = fcntl.fcntl(fd, fcntl.F_GETFD)
        assert flags & fcntl.FD_CLOEXEC, "O_CLOEXEC not set"
    finally:
        release_lock(fd)


def test_release_lock_unlocks_fd(tmp_path):
    """release_lock calls flock(LOCK_UN) and closes fd."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    with patch("fcntl.flock") as mock_flock, patch("os.close") as mock_close:
        release_lock(fd)
        mock_flock.assert_called_once_with(fd, fcntl.LOCK_UN)
        mock_close.assert_called_once_with(fd)


# ---------------------------------------------------------------------------
# Cross-platform abstraction
# ---------------------------------------------------------------------------

def test_is_pid_alive_for_own_process():
    assert is_pid_alive(os.getpid()) is True


def test_is_pid_alive_for_dead_pid():
    assert is_pid_alive(99999999) is False


def test_is_pid_alive_for_none():
    assert is_pid_alive(None) is False


def test_is_pid_alive_permission_error_returns_true():
    with patch("os.kill", side_effect=PermissionError("operation not permitted")):
        assert is_pid_alive(12345) is True


def test_is_pid_alive_win32_alive():
    """Win32 path: OpenProcess non-zero handle → alive."""
    import ctypes
    import sys
    from unittest.mock import MagicMock
    mock_k = MagicMock()
    mock_k.OpenProcess.return_value = 1  # non-zero = valid handle
    with patch.object(sys, "platform", "win32"), \
         patch.object(ctypes, "windll", MagicMock(kernel32=mock_k), create=True):
        assert is_pid_alive(1234) is True
    mock_k.CloseHandle.assert_called_once_with(1)


def test_is_pid_alive_win32_dead():
    """Win32 path: OpenProcess returns 0 → process not found."""
    import ctypes
    import sys
    from unittest.mock import MagicMock
    mock_k = MagicMock()
    mock_k.OpenProcess.return_value = 0  # zero = not found
    with patch.object(sys, "platform", "win32"), \
         patch.object(ctypes, "windll", MagicMock(kernel32=mock_k), create=True):
        assert is_pid_alive(99999) is False


# ---------------------------------------------------------------------------
# _read_pid_from_fd
# ---------------------------------------------------------------------------

def test_read_pid_from_fd_returns_none_for_corrupt_content(tmp_path):
    from unity_mcp.lockfile import _read_pid_from_fd
    f = tmp_path / "corrupt.lock"
    f.write_bytes(b"not-a-pid\n")
    fd = os.open(str(f), os.O_RDWR)
    try:
        assert _read_pid_from_fd(fd) is None
    finally:
        os.close(fd)


# ---------------------------------------------------------------------------
# read_pid_from_port_file
# ---------------------------------------------------------------------------

def test_read_pid_from_port_file(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = 12345
    (ports_dir / f"{pid}.port").write_text("9500\n/path/to/project\nMyProject", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_pid_from_port_file(9500) == pid


def test_read_pid_returns_none_for_missing():
    assert read_pid_from_port_file(9999) is None


def test_read_pid_from_port_file_corrupt_json(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "12345.port").write_text("not-a-port\n/path/to/project", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_pid_from_port_file(9500) is None


def test_read_pid_from_port_file_non_integer_stem(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "abc.port").write_text("9500\n/path/to/project", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_pid_from_port_file(9500) is None


def test_read_pid_from_port_file_cyrillic_path_does_not_crash(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "99999.port").write_bytes("9500\n/Users/Иван/МойПроект\nМойПроект\n".encode("utf-8"))
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_pid_from_port_file(9500) == 99999


def test_read_pid_from_port_file_ignores_blank_project_path(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    expected_project = tmp_path / "ExpectedProject"
    expected_project.mkdir()

    # C# format with empty ProjectPath line must never be treated as a match for project-aware lookup.
    (ports_dir / "1111.port").write_text(
        "9600\n\nExpectedProject\n",
        encoding="utf-8",
    )
    # Keep a valid alternative so caller-side port fallback still has a deterministic target.
    (ports_dir / "2222.port").write_text(
        "9601\n/some/other/project\nOtherProject\n",
        encoding="utf-8",
    )

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("os.getcwd", return_value=str(expected_project)):
        assert read_pid_from_port_file(9600, project_path=expected_project) is None


# ---------------------------------------------------------------------------
# read_port_for_pid
# ---------------------------------------------------------------------------

def test_read_port_for_pid_returns_current_port(tmp_path):
    """pid->port mirror of read_pid_from_port_file: reads {pid}.port by filename."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = 12345
    (ports_dir / f"{pid}.port").write_text(
        "9501\n/path/to/project\nProj", encoding="utf-8"
    )
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_port_for_pid(pid) == 9501


def test_read_port_for_pid_matches_by_pid_not_any_port(tmp_path):
    """Keyed by the pid's own filename — a stale port on another pid's file
    must never leak into this pid's result."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid, other_pid = 12345, 67890
    (ports_dir / f"{pid}.port").write_text(
        "9501\n/path/to/project\nProj", encoding="utf-8"
    )
    (ports_dir / f"{other_pid}.port").write_text(
        "9500\n/path/to/other\nOther", encoding="utf-8"
    )
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_port_for_pid(pid) == 9501


def test_read_port_for_pid_dead_pid_returns_none(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = 12345
    (ports_dir / f"{pid}.port").write_text(
        "9501\n/path/to/project\nProj", encoding="utf-8"
    )
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert read_port_for_pid(pid) is None


def test_read_port_for_pid_no_file_returns_none(tmp_path):
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_port_for_pid(12345) is None


def test_read_port_for_pid_project_path_mismatch_returns_none(tmp_path):
    """Mirrors test_read_pid_from_port_file_ignores_blank_project_path: when
    project_path is supplied, a blank/mismatched line 2 must never match."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    expected_project = tmp_path / "ExpectedProject"
    expected_project.mkdir()
    pid = 12345
    (ports_dir / f"{pid}.port").write_text("9501\n\nProj\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_port_for_pid(pid, project_path=expected_project) is None


# ---------------------------------------------------------------------------
# read_project_path_from_port_file
# ---------------------------------------------------------------------------

def test_read_project_path_from_port_file_returns_path(tmp_path):
    from unity_mcp.lockfile import read_project_path_from_port_file
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_dir = tmp_path / "MyProject"
    project_dir.mkdir()
    (ports_dir / "12345.port").write_text(f"9500\n{project_dir}\nMyProject\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_project_path_from_port_file(9500) == project_dir


def test_read_project_path_dead_pid_skipped(tmp_path):
    from unity_mcp.lockfile import read_project_path_from_port_file
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_dir = tmp_path / "MyProject"
    project_dir.mkdir()
    (ports_dir / "99999.port").write_text(f"9500\n{project_dir}\nMyProject\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert read_project_path_from_port_file(9500) is None


def test_read_project_path_from_port_file_wrong_port(tmp_path):
    from unity_mcp.lockfile import read_project_path_from_port_file
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_dir = tmp_path / "MyProject"
    project_dir.mkdir()
    (ports_dir / "12345.port").write_text(f"9501\n{project_dir}\nMyProject\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_project_path_from_port_file(9500) is None


# ---------------------------------------------------------------------------
# read_reload_port
# ---------------------------------------------------------------------------

def test_read_reload_port_returns_none_when_no_dir():
    from unity_mcp.lockfile import read_reload_port
    with patch.object(Path, "home", return_value=Path("/nonexistent_dir_xyz")):
        assert read_reload_port() is None


def test_read_reload_port_returns_port_for_alive_pid(tmp_path):
    from unity_mcp.lockfile import read_reload_port
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = os.getpid()
    (ports_dir / f"{pid}.reload-port").write_text("9600", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_reload_port() == 9600


def test_read_reload_port_skips_dead_pid(tmp_path):
    from unity_mcp.lockfile import read_reload_port
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "99999999.reload-port").write_text("9600", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_reload_port() is None


def test_read_reload_port_skips_corrupt_file(tmp_path):
    from unity_mcp.lockfile import read_reload_port
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = os.getpid()
    (ports_dir / f"{pid}.reload-port").write_text("not-a-port", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_reload_port() is None


def test_read_reload_port_cwd_disambiguation(tmp_path):
    from unity_mcp.lockfile import read_reload_port
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = os.getpid()
    proj_a = str(tmp_path / "ProjectA")
    (ports_dir / f"{pid}.reload-port").write_text(f"9601\n{proj_a}\nProjectA", encoding="utf-8")
    with patch("os.getcwd", return_value=proj_a), \
         patch.object(Path, "home", return_value=tmp_path):
        assert read_reload_port() == 9601


def test_read_reload_port_multiline_backward_compat(tmp_path):
    from unity_mcp.lockfile import read_reload_port
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = os.getpid()
    (ports_dir / f"{pid}.reload-port").write_text(
        "9605\n/some/project/path\nMyProject", encoding="utf-8"
    )
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_reload_port() == 9605


# ---------------------------------------------------------------------------
# cleanup_stale_locks — zombie detection (Bug #3)
# ---------------------------------------------------------------------------

def test_zombie_detection_deletes_dead_pid_lockfile(tmp_path):
    """Dead PID lockfile is deleted by cleanup_stale_locks."""
    from unity_mcp.lockfile import cleanup_stale_locks
    dead_pid = 99999999
    (tmp_path / f"server-9900-{dead_pid}.lock").write_text(str(dead_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9900, lock_dir=tmp_path)
    assert cleaned == 1
    assert not (tmp_path / f"server-9900-{dead_pid}.lock").exists()


def test_alive_lockfile_preserved(tmp_path):
    """Alive PID lockfile is NOT deleted by cleanup_stale_locks."""
    from unity_mcp.lockfile import cleanup_stale_locks
    alive_pid = os.getpid()
    lock_file = tmp_path / f"server-9900-{alive_pid}.lock"
    lock_file.write_text(str(alive_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9900, lock_dir=tmp_path)
    assert cleaned == 0
    assert lock_file.exists()


def test_multiple_zombies_scenario(tmp_path):
    """3 dead + 2 alive lockfiles: cleanup removes 3, keeps 2."""
    from unity_mcp.lockfile import cleanup_stale_locks
    dead_pids = [99999991, 99999992, 99999993]
    alive_pids = [os.getpid(), os.getppid()]  # both are definitely alive
    for pid in dead_pids:
        (tmp_path / f"server-9500-{pid}.lock").write_text(str(pid), encoding="utf-8")
    for pid in alive_pids:
        (tmp_path / f"server-9500-{pid}.lock").write_text(str(pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9500, lock_dir=tmp_path)
    assert cleaned == 3
    for pid in dead_pids:
        assert not (tmp_path / f"server-9500-{pid}.lock").exists()
    for pid in alive_pids:
        assert (tmp_path / f"server-9500-{pid}.lock").exists()


def test_cleanup_stale_locks_ignores_other_ports(tmp_path):
    """cleanup_stale_locks(9500) must NOT touch server-9900-*.lock files."""
    from unity_mcp.lockfile import cleanup_stale_locks
    dead_pid = 99999999
    (tmp_path / f"server-9900-{dead_pid}.lock").write_text(str(dead_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9500, lock_dir=tmp_path)
    assert cleaned == 0
    assert (tmp_path / f"server-9900-{dead_pid}.lock").exists()


def test_cleanup_stale_locks_empty_dir(tmp_path):
    """cleanup_stale_locks on empty dir returns 0."""
    from unity_mcp.lockfile import cleanup_stale_locks
    assert cleanup_stale_locks(9500, lock_dir=tmp_path) == 0


def test_cleanup_stale_locks_nonexistent_dir():
    """cleanup_stale_locks on missing dir returns 0 without error."""
    from unity_mcp.lockfile import cleanup_stale_locks
    assert cleanup_stale_locks(9500, lock_dir=Path("/nonexistent_xyz_abc")) == 0


def test_kill_all_finds_all_lockfiles(tmp_path):
    """KillAll pattern: cleanup_stale_locks finds all server-{port}-{pid}.lock files."""
    from unity_mcp.lockfile import cleanup_stale_locks
    port = 9900
    dead_pids = [99999991, 99999992, 99999993]
    for pid in dead_pids:
        (tmp_path / f"server-{port}-{pid}.lock").write_text(str(pid), encoding="utf-8")
    # Also create a file for another port — must be ignored
    (tmp_path / f"server-9500-99999999.lock").write_text("99999999", encoding="utf-8")
    cleaned = cleanup_stale_locks(port, lock_dir=tmp_path)
    assert cleaned == 3, f"Expected 3 cleaned, got {cleaned}"
    for pid in dead_pids:
        assert not (tmp_path / f"server-{port}-{pid}.lock").exists()
    # Other port file untouched
    assert (tmp_path / "server-9500-99999999.lock").exists()


# ---------------------------------------------------------------------------
# SD-3: read_pid_from_port_file PID liveness + cleanup_stale_port_files
# ---------------------------------------------------------------------------

def test_read_pid_from_port_file_skips_dead_pid(tmp_path):
    """Dead PID port file skipped → returns None."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "99999.port").write_text("9500\n/path/to/project\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert read_pid_from_port_file(9500) is None


def test_cleanup_stale_port_files_removes_dead(tmp_path):
    """Dead PID .port file is deleted."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "99999.port"
    f.write_text("9500\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 1
    assert not f.exists()


def test_cleanup_stale_port_files_keeps_alive(tmp_path):
    """Alive PID .port file is preserved."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11111.port"
    f.write_text("9500\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0
    assert f.exists()


def test_cleanup_stale_port_files_all_patterns(tmp_path):
    """Cleans *.port, *.chat-port, *.reload-port patterns."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    files = [
        ports_dir / "99991.port",
        ports_dir / "99992.chat-port",
        ports_dir / "99993.reload-port",
    ]
    for f in files:
        f.write_text("9500\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 3
    for f in files:
        assert not f.exists()


# ---------------------------------------------------------------------------
# Phase 4b: cleanup_stale_port_files — *.chat-port + TCP probe
# ---------------------------------------------------------------------------

def test_cleanup_stale_port_files_cleans_chat_port(tmp_path):
    """Dead PID .chat-port file is deleted."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "99998.chat-port"
    f.write_text("9510\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 1
    assert not f.exists()


def test_cleanup_stale_port_files_tcp_probe_dead_port(tmp_path):
    """PID alive but port not listening → file deleted only once the probe
    failure PERSISTS across a full sweep interval (C1 #11). A single failed
    probe (first sweep) must be tolerated — Unity's own same-port bind-retry
    loop can leave the port transiently unbound — so cleanup on the first
    call must be a no-op, and only the second call (now_fn advanced by
    PROBE_GRACE_S) actually deletes the file."""
    from unity_mcp.lockfile import PROBE_GRACE_S, cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11111.port"
    f.write_text("9511\n/path\n", encoding="utf-8")
    clock = [1_000.0]
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile._tcp_probe", return_value=False):
        first = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert first == 0, "a single probe failure must not delete a live-PID file"
        assert f.exists()

        clock[0] += PROBE_GRACE_S
        second = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
    assert second == 1
    assert not f.exists()


def test_cleanup_stale_port_files_tcp_probe_recovery_resets_grace(tmp_path):
    """A probe that recovers between sweeps clears the grace-tracking entry —
    a later failure must start a fresh grace window rather than reusing the
    stale first-failure timestamp (which would otherwise delete on the very
    next failed sweep)."""
    from unity_mcp.lockfile import PROBE_GRACE_S, cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11115.port"
    f.write_text("9515\n/path\n", encoding="utf-8")
    clock = [2_000.0]
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        with patch("unity_mcp.lockfile._tcp_probe", return_value=False):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0
        assert f.exists()

        clock[0] += 1.0  # probe recovers well inside the grace window
        with patch("unity_mcp.lockfile._tcp_probe", return_value=True):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0
        assert f.exists()

        # PROBE_GRACE_S has now elapsed since the ORIGINAL failure, but the
        # recovery must have reset it -- this new failure needs its own grace.
        clock[0] += PROBE_GRACE_S
        with patch("unity_mcp.lockfile._tcp_probe", return_value=False):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0, "reset must not be short-circuited by the original timestamp"
        assert f.exists()


def test_cleanup_stale_port_files_tcp_probe_live_port_kept(tmp_path):
    """PID alive and port listening → file kept."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11112.port"
    f.write_text("9512\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile._tcp_probe", return_value=True):
        cleaned = cleanup_stale_port_files(tcp_probe=True)
    assert cleaned == 0
    assert f.exists()


def test_cleanup_stale_port_files_no_tcp_probe_by_default(tmp_path):
    """tcp_probe=False (default) — alive PID with dead port is NOT cleaned."""
    from unity_mcp.lockfile import cleanup_stale_port_files
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11113.port"
    f.write_text("9513\n/path\n", encoding="utf-8")
    probe_calls = []
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile._tcp_probe", side_effect=lambda p, **kw: probe_calls.append(p) or False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0
    assert f.exists()
    assert not probe_calls  # TCP probe was NOT called


# ---------------------------------------------------------------------------
# T5: write_lock_metadata / read_lock_metadata
# ---------------------------------------------------------------------------

def test_write_lock_metadata_appends_json_on_line2(tmp_path):
    """write_lock_metadata writes JSON on line 2 of the lockfile after the PID."""
    from unity_mcp.lockfile import write_lock_metadata, read_lock_metadata
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    try:
        meta = {"v": 2, "lockToken": "tok123", "sessionId": "sess456"}
        write_lock_metadata(fd, meta)
        result = read_lock_metadata(fd)
        assert result == meta
    finally:
        release_lock(fd)


def test_read_lock_metadata_returns_none_when_absent(tmp_path):
    """read_lock_metadata returns None when line 2 has no JSON (raw file)."""
    from unity_mcp.lockfile import read_lock_metadata
    f = tmp_path / "bare.lock"
    f.write_text("12345\n", encoding="utf-8")
    fd = os.open(str(f), os.O_RDWR)
    try:
        assert read_lock_metadata(fd) is None
    finally:
        os.close(fd)


def test_acquire_lock_with_metadata_writes_json(tmp_path):
    """acquire_lock with metadata kwarg immediately writes JSON to line 2."""
    from unity_mcp.lockfile import read_lock_metadata
    meta = {"v": 2, "role": "mcp", "cwd": "/some/path"}
    fd = acquire_lock(lock_dir=tmp_path, port=9500, metadata=meta)
    try:
        result = read_lock_metadata(fd)
        assert result == meta
    finally:
        release_lock(fd)


# ---------------------------------------------------------------------------
# PPID metadata in lockfile
# ---------------------------------------------------------------------------

def test_acquire_lock_writes_ppid(tmp_path):
    """acquire_lock always writes ppid on line 2 so eviction can read it."""
    from unity_mcp.lockfile import _read_ppid_from_lock_path
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_path = tmp_path / f"server-9500-{os.getpid()}.lock"
    try:
        assert _read_ppid_from_lock_path(lock_path) == os.getppid()
    finally:
        release_lock(fd)


def test_read_ppid_missing_returns_none(tmp_path):
    """_read_ppid_from_lock_path returns None when line 2 has no JSON."""
    from unity_mcp.lockfile import _read_ppid_from_lock_path
    f = tmp_path / "server-9500-11111.lock"
    f.write_text("11111\n", encoding="utf-8")
    assert _read_ppid_from_lock_path(f) is None


def test_read_ppid_corrupt_returns_none(tmp_path):
    """_read_ppid_from_lock_path returns None when line 2 is not valid JSON."""
    from unity_mcp.lockfile import _read_ppid_from_lock_path
    f = tmp_path / "server-9500-11111.lock"
    f.write_text("11111\nnot-valid-json\n", encoding="utf-8")
    assert _read_ppid_from_lock_path(f) is None
