"""TDD tests for phantom_cleaner.py — dead-PID lock/port file sweeping.

Extracted from test_lockfile.py (lock acquisition/metadata/discovery-read
tests stay there; cleanup-specific tests move here with the code).
"""
import os
import sys
from pathlib import Path
from unittest.mock import patch

import pytest

if sys.platform == "win32":
    pytest.skip(
        "phantom_cleaner tests mirror lockfile's POSIX-only PID/fcntl assumptions",
        allow_module_level=True,
    )

from unity_mcp.phantom_cleaner import (
    PROBE_GRACE_S,
    cleanup_stale_locks,
    cleanup_stale_port_files,
)


# ---------------------------------------------------------------------------
# cleanup_stale_locks — zombie detection (Bug #3)
# ---------------------------------------------------------------------------

def test_zombie_detection_deletes_dead_pid_lockfile(tmp_path):
    """Dead PID lockfile is deleted by cleanup_stale_locks."""
    dead_pid = 99999999
    (tmp_path / f"server-9900-{dead_pid}.lock").write_text(str(dead_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9900, lock_dir=tmp_path)
    assert cleaned == 1
    assert not (tmp_path / f"server-9900-{dead_pid}.lock").exists()


def test_alive_lockfile_preserved(tmp_path):
    """Alive PID lockfile is NOT deleted by cleanup_stale_locks."""
    alive_pid = os.getpid()
    lock_file = tmp_path / f"server-9900-{alive_pid}.lock"
    lock_file.write_text(str(alive_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9900, lock_dir=tmp_path)
    assert cleaned == 0
    assert lock_file.exists()


def test_multiple_zombies_scenario(tmp_path):
    """3 dead + 2 alive lockfiles: cleanup removes 3, keeps 2."""
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
    dead_pid = 99999999
    (tmp_path / f"server-9900-{dead_pid}.lock").write_text(str(dead_pid), encoding="utf-8")
    cleaned = cleanup_stale_locks(9500, lock_dir=tmp_path)
    assert cleaned == 0
    assert (tmp_path / f"server-9900-{dead_pid}.lock").exists()


def test_cleanup_stale_locks_empty_dir(tmp_path):
    """cleanup_stale_locks on empty dir returns 0."""
    assert cleanup_stale_locks(9500, lock_dir=tmp_path) == 0


def test_cleanup_stale_locks_nonexistent_dir():
    """cleanup_stale_locks on missing dir returns 0 without error."""
    assert cleanup_stale_locks(9500, lock_dir=Path("/nonexistent_xyz_abc")) == 0


def test_kill_all_finds_all_lockfiles(tmp_path):
    """KillAll pattern: cleanup_stale_locks finds all server-{port}-{pid}.lock files."""
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
# cleanup_stale_port_files — dead/alive PID + pattern coverage (SD-3)
# ---------------------------------------------------------------------------

def test_cleanup_stale_port_files_removes_dead(tmp_path):
    """Dead PID .port file is deleted."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "99999.port"
    f.write_text("9500\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 1
    assert not f.exists()


def test_cleanup_stale_port_files_keeps_alive(tmp_path):
    """Alive PID .port file is preserved."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11111.port"
    f.write_text("9500\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=True):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0
    assert f.exists()


def test_cleanup_stale_port_files_all_patterns(tmp_path):
    """Cleans *.port, *.chat-port, *.reload-port patterns."""
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
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 3
    for f in files:
        assert not f.exists()


# ---------------------------------------------------------------------------
# Phase 4b: cleanup_stale_port_files — *.chat-port + TCP probe
# ---------------------------------------------------------------------------

def test_cleanup_stale_port_files_cleans_chat_port(tmp_path):
    """Dead PID .chat-port file is deleted."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "99998.chat-port"
    f.write_text("9510\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=False):
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
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11111.port"
    f.write_text("9511\n/path\n", encoding="utf-8")
    clock = [1_000.0]
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=True), \
         patch("unity_mcp.phantom_cleaner._tcp_probe", return_value=False):
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
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11115.port"
    f.write_text("9515\n/path\n", encoding="utf-8")
    clock = [2_000.0]
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=True):
        with patch("unity_mcp.phantom_cleaner._tcp_probe", return_value=False):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0
        assert f.exists()

        clock[0] += 1.0  # probe recovers well inside the grace window
        with patch("unity_mcp.phantom_cleaner._tcp_probe", return_value=True):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0
        assert f.exists()

        # PROBE_GRACE_S has now elapsed since the ORIGINAL failure, but the
        # recovery must have reset it -- this new failure needs its own grace.
        clock[0] += PROBE_GRACE_S
        with patch("unity_mcp.phantom_cleaner._tcp_probe", return_value=False):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0, "reset must not be short-circuited by the original timestamp"
        assert f.exists()


def test_cleanup_stale_port_files_tcp_probe_live_port_kept(tmp_path):
    """PID alive and port listening → file kept."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11112.port"
    f.write_text("9512\n/path\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=True), \
         patch("unity_mcp.phantom_cleaner._tcp_probe", return_value=True):
        cleaned = cleanup_stale_port_files(tcp_probe=True)
    assert cleaned == 0
    assert f.exists()


def test_cleanup_stale_port_files_no_tcp_probe_by_default(tmp_path):
    """tcp_probe=False (default) — alive PID with dead port is NOT cleaned."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "11113.port"
    f.write_text("9513\n/path\n", encoding="utf-8")
    probe_calls = []
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.phantom_cleaner.is_pid_alive", return_value=True), \
         patch("unity_mcp.phantom_cleaner._tcp_probe", side_effect=lambda p, **kw: probe_calls.append(p) or False):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0
    assert f.exists()
    assert not probe_calls  # TCP probe was NOT called


# ---------------------------------------------------------------------------
# Shared contract pin: Unity's on-disk port-file format vs Python's reader.
# Not a DSL RECORD (see Plans/task-1.2-phantom-cleaner.md "Shared Contract
# Decision") — this is the cheap pin that goes red first if either side's
# write/read format ever drifts.
# ---------------------------------------------------------------------------

def test_port_file_golden_format_matches_unity_contract(tmp_path):
    """Pin the exact literal PortFileManager.WriteRuntimePortFiles() produces
    (unity-plugin/Editor/PortFileManager.cs: `$"{port}\\n{projectPath}\\n{projectName}"`)
    and assert lockfile._read_port_file_lines parses it byte-for-byte."""
    from unity_mcp.lockfile import _read_port_file_lines

    f = tmp_path / "12345.port"
    f.write_text("9500\n/fake/Project\nProject", encoding="utf-8")

    lines = _read_port_file_lines(f, max_lines=3)

    assert lines[0] == "9500"
    assert lines[1] == "/fake/Project"
    assert lines[2] == "Project"
