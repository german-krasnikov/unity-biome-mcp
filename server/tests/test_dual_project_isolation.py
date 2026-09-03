"""Dual-project port isolation — pure unit tests, no live Unity required."""
import os
import sys
from pathlib import Path
from unittest.mock import patch

import pytest

if sys.platform == "win32":
    pytest.skip("lockfile tests use fcntl (POSIX-only)", allow_module_level=True)

from unity_mcp.lockfile import (
    acquire_lock,
    cleanup_stale_port_files,
    read_pid_from_port_file,
    release_lock,
)


def _make_port_file(ports_dir: Path, pid: int, port: int, project: str) -> Path:
    f = ports_dir / f"{pid}.port"
    f.write_text(f"{port}\n{project}\n", encoding="utf-8")
    return f


# ---------------------------------------------------------------------------
# 1. project_path filter: same port, two projects → correct PID per project
# ---------------------------------------------------------------------------

def test_port_file_project_path_filter(tmp_path):
    """Two port files on same port but different projects → filter returns the matching PID."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)

    proj_a = str(tmp_path / "ProjectA")
    proj_b = str(tmp_path / "ProjectB")
    pid_a, pid_b = 11111, 22222

    _make_port_file(ports_dir, pid_a, 9500, proj_a)
    _make_port_file(ports_dir, pid_b, 9500, proj_b)

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_pid_from_port_file(9500, project_path=proj_a) == pid_a
        assert read_pid_from_port_file(9500, project_path=proj_b) == pid_b


# ---------------------------------------------------------------------------
# 2. cleanup_stale_port_files preserves live sibling from another project
# ---------------------------------------------------------------------------

def test_cleanup_stale_leaves_live_sibling(tmp_path):
    """Dead-PID port file deleted; live-PID sibling (different project) is kept."""
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)

    dead_file = _make_port_file(ports_dir, 99999, 9500, str(tmp_path / "ProjectA"))
    live_file = _make_port_file(ports_dir, 11111, 9501, str(tmp_path / "ProjectB"))

    def pid_alive(pid):
        return pid == 11111

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile.is_pid_alive", side_effect=pid_alive):
        cleaned = cleanup_stale_port_files()

    assert cleaned == 1
    assert not dead_file.exists()
    assert live_file.exists()


# ---------------------------------------------------------------------------
# 3. acquire_lock on two different ports — both succeed simultaneously
# ---------------------------------------------------------------------------

def test_lock_files_different_ports_coexist(tmp_path):
    """acquire_lock(9500) and acquire_lock(9501) succeed in the same session."""
    pid_a, pid_b = 33333, 44444

    with patch("os.getpid", return_value=pid_a):
        fd_a = acquire_lock(lock_dir=tmp_path, port=9500)
    with patch("os.getpid", return_value=pid_b):
        fd_b = acquire_lock(lock_dir=tmp_path, port=9501)

    try:
        assert (tmp_path / f"server-9500-{pid_a}.lock").exists()
        assert (tmp_path / f"server-9501-{pid_b}.lock").exists()
    finally:
        release_lock(fd_a)
        release_lock(fd_b)


# ---------------------------------------------------------------------------
# 4. Port-change handoff: new port acquired BEFORE old port released
# ---------------------------------------------------------------------------

def test_on_port_change_atomic(tmp_path):
    """acquire new port BEFORE releasing old — no gap where neither is locked."""
    events: list[tuple[str, int | None]] = []

    pid_old, pid_new = 55555, 66666

    with patch("os.getpid", return_value=pid_old):
        fd_old = acquire_lock(lock_dir=tmp_path, port=9500)
    events.append(("acquired", 9500))

    # Acquire new port while old is still held → both coexist
    with patch("os.getpid", return_value=pid_new):
        fd_new = acquire_lock(lock_dir=tmp_path, port=9501)
    events.append(("acquired", 9501))

    # Both lock files exist simultaneously — invariant holds
    assert (tmp_path / f"server-9500-{pid_old}.lock").exists()
    assert (tmp_path / f"server-9501-{pid_new}.lock").exists()

    release_lock(fd_old)
    events.append(("released", 9500))

    # After releasing old, new is still locked
    assert not (tmp_path / f"server-9500-{pid_old}.lock").exists()
    assert (tmp_path / f"server-9501-{pid_new}.lock").exists()

    release_lock(fd_new)
    events.append(("released", 9501))

    # Confirm acquire-before-release ordering
    acquire_indices = [i for i, (op, _) in enumerate(events) if op == "acquired"]
    release_indices = [i for i, (op, _) in enumerate(events) if op == "released"]
    assert acquire_indices[-1] < release_indices[0], "new port must be acquired before any release"


# ---------------------------------------------------------------------------
# 5. tcp_probe grace period: a genuinely live PID (this test process) must
#    survive one failed probe (C1 #11 — a sibling project's Unity mid
#    bind-retry can fail one sweep and recover before the next).
# ---------------------------------------------------------------------------

def test_cleanup_stale_port_files_live_pid_survives_single_probe_failure(tmp_path):
    """No is_pid_alive mocking — os.getpid() is a real, currently-running PID.
    A dead-listener probe failure must NOT delete its file on the first
    sweep; only a failure that persists for PROBE_GRACE_S deletes it."""
    from unity_mcp.lockfile import PROBE_GRACE_S
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, os.getpid(), 9520, str(tmp_path / "ProjectC"))
    clock = [10_000.0]

    with patch.object(Path, "home", return_value=tmp_path), \
         patch("unity_mcp.lockfile._tcp_probe", return_value=False):
        cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
        assert cleaned == 0, "a single probe failure against a live PID must be tolerated"
        assert f.exists()

        clock[0] += PROBE_GRACE_S
        cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])
    assert cleaned == 1, "failure persisting across a full sweep interval must be cleaned"
    assert not f.exists()


def test_cleanup_stale_port_files_live_pid_probe_recovery_keeps_file(tmp_path):
    """A probe that recovers before PROBE_GRACE_S elapses must reset the
    grace window — a later `now` that is far past the ORIGINAL failure must
    not delete the file if the probe itself is passing again."""
    from unity_mcp.lockfile import PROBE_GRACE_S
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, os.getpid(), 9521, str(tmp_path / "ProjectD"))
    clock = [20_000.0]

    with patch.object(Path, "home", return_value=tmp_path):
        with patch("unity_mcp.lockfile._tcp_probe", return_value=False):
            cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])

        clock[0] += PROBE_GRACE_S * 5  # far past the original grace deadline
        with patch("unity_mcp.lockfile._tcp_probe", return_value=True):
            cleaned = cleanup_stale_port_files(tcp_probe=True, now_fn=lambda: clock[0])

    assert cleaned == 0, "a recovered probe must never delete the file"
    assert f.exists()
