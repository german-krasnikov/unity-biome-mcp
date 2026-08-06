"""Tests for _read_unity_port — filesystem/env/PID mocking only, no global state leaks."""
import os
import pytest
from unittest.mock import patch
from unity_mcp.server import _read_unity_port


@pytest.fixture(autouse=True)
def _mock_tcp_probe():
    """Bypass TCP probe in all read_unity_port tests — they test PID/FS logic only."""
    with patch("unity_mcp.server_filtering._tcp_probe", return_value=True):
        yield


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_port_file(ports_dir, pid: int, port: int, project: str = "MyProject", mtime: float = 1000.0):
    """Write a <pid>.port file and set its mtime."""
    f = ports_dir / f"{pid}.port"
    f.write_text(f"{port}\n\n{project}\n", encoding="utf-8")
    os.utime(f, (mtime, mtime))
    return f


def _make_port_file_with_path(ports_dir, pid: int, port: int, project_path: str,
                               project: str = "MyProject", mtime: float = 1000.0):
    """Write a <pid>.port file with a real project path on line 1 (C# format)."""
    f = ports_dir / f"{pid}.port"
    f.write_text(f"{port}\n{project_path}\n{project}\n", encoding="utf-8")
    os.utime(f, (mtime, mtime))
    return f


# ---------------------------------------------------------------------------
# Env override
# ---------------------------------------------------------------------------

def test_env_override_returns_that_port(monkeypatch, tmp_path):
    monkeypatch.setenv("UNITY_MCP_PORT", "9999")
    # Even if ports_dir exists with valid files, env wins
    assert _read_unity_port() == 9999


def test_env_override_parses_int(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_PORT", "10500")
    assert _read_unity_port() == 10500


def test_env_override_non_numeric_falls_through(monkeypatch, tmp_path):
    """Non-numeric UNITY_MCP_PORT doesn't crash — falls through to default."""
    monkeypatch.setenv("UNITY_MCP_PORT", "notaport")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# No env, no lockfiles → default 9500
# ---------------------------------------------------------------------------

def test_fallback_default_when_no_dir(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    # Point home to tmp_path so ~/.unity-biome-mcp/ports never exists
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


def test_fallback_default_when_dir_empty(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# Single live lockfile
# ---------------------------------------------------------------------------

def test_single_live_lockfile_returns_port(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=1234, port=9501, project="Game")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # mock at the abstraction level so it works on all platforms (Windows uses ctypes, not os.kill)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    result = _read_unity_port()
    assert result == 9501


# ---------------------------------------------------------------------------
# mtime sort: newest lockfile wins
# ---------------------------------------------------------------------------

def test_newest_lockfile_wins(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=100, port=9600, mtime=500.0)   # older
    _make_port_file(ports_dir, pid=200, port=9700, mtime=1500.0)  # newer
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9700


def test_oldest_lockfile_loses(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=100, port=9600, mtime=2000.0)  # newer
    _make_port_file(ports_dir, pid=200, port=9700, mtime=100.0)   # older
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9600


# ---------------------------------------------------------------------------
# Stale PID — ProcessLookupError → file removed, skipped
# ---------------------------------------------------------------------------

def test_dead_pid_process_lookup_error_skips_and_removes(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9999, port=9501)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # is_pid_alive returns False → dead PID → file removed, port skipped
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: False)
    result = _read_unity_port()
    assert result == 9500        # fell through to default
    assert not f.exists()        # file was cleaned up


def test_permission_error_keeps_file_as_live_candidate(monkeypatch, tmp_path):
    """is_pid_alive returns True (alive, different user) — keep file."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9998, port=9502)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # is_pid_alive returns True → process alive → port is valid candidate
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    result = _read_unity_port()
    assert result == 9502  # process alive — port is valid candidate
    assert f.exists()      # file NOT deleted


def test_dead_pid_os_error_skips_and_removes(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9997, port=9503)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # is_pid_alive returns False → dead PID → file removed, port skipped
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: False)
    result = _read_unity_port()
    assert result == 9500
    assert not f.exists()


# ---------------------------------------------------------------------------
# Mixed: one dead, one live → live one wins
# ---------------------------------------------------------------------------

def test_mixed_dead_and_live_returns_live_port(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    dead_file = _make_port_file(ports_dir, pid=7777, port=9800, mtime=2000.0)
    _make_port_file(ports_dir, pid=8888, port=9801, mtime=1000.0)

    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # 7777 is dead, 8888 is alive — works on all platforms (no os.kill)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: pid != 7777)
    result = _read_unity_port()
    assert result == 9801        # only live candidate
    assert not dead_file.exists()


# ---------------------------------------------------------------------------
# Malformed file: ValueError on int parse → skipped/removed
# ---------------------------------------------------------------------------

def test_malformed_port_file_skipped(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad = ports_dir / "notanint.port"
    bad.write_text("broken\n", encoding="utf-8")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # int(lines[0]) where lines[0]="broken" → ValueError → skip
    assert _read_unity_port() == 9500


def test_malformed_port_content_skipped(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad = ports_dir / "1234.port"
    bad.write_text("notaport\n", encoding="utf-8")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # int("notaport") → ValueError → skip
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# project field: missing third line falls back to "?"
# (doesn't affect port selection, just verify no crash)
# ---------------------------------------------------------------------------

def test_lockfile_without_project_line(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "5555.port"
    f.write_text("9505\n", encoding="utf-8")  # only one line — no project
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9505


# ---------------------------------------------------------------------------
# CWD-based port discovery
# ---------------------------------------------------------------------------

def test_cwd_exact_match_project_root(monkeypatch, tmp_path):
    """CWD is exactly the project root (not a subdir) — still matches."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: project_a)
    assert _read_unity_port() == 9500


def test_cwd_match_returns_matching_project_port(monkeypatch, tmp_path):
    """CWD inside project dir → returns that port even if another has newer mtime."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    # Project B has newer mtime — would win without CWD matching
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # CWD is inside project A
    monkeypatch.setattr("os.getcwd", lambda: project_a + "/Assets/Scripts")
    assert _read_unity_port() == 9500


def test_cwd_no_match_falls_back_to_mtime(monkeypatch, tmp_path):
    """CWD doesn't match any project → falls back to newest mtime."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # CWD is somewhere unrelated
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "OtherDir"))
    assert _read_unity_port() == 9501  # newest mtime wins


def test_cwd_nested_projects_prefers_longest_match(monkeypatch, tmp_path):
    """Two overlapping project paths → prefer the longer (more specific) one."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_root = str(tmp_path / "Workspace")
    project_sub  = str(tmp_path / "Workspace" / "SubProject")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_root, mtime=2000.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_sub,  mtime=500.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # CWD is inside the sub-project (both match, longest wins)
    monkeypatch.setattr("os.getcwd", lambda: project_sub + "/Assets")
    assert _read_unity_port() == 9501


# ---------------------------------------------------------------------------
# RC-4: skip_probe bypasses TCP check
# ---------------------------------------------------------------------------

def test_skip_probe_true_returns_port_without_tcp_check(monkeypatch, tmp_path):
    """skip_probe: PID-alive candidate always included — _tcp_probe removed to stop Unity console spam.
    Previously skip_probe=False caused the candidate to be TCP-probed and skipped on failure.
    Now the probe is gone; PID liveness is the only gate in both modes.
    skip_probe=False still controls the no-candidates fallback (None vs 9500).
    """
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=1111, port=9510)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    probe_calls = []

    def fail_probe(port, timeout=0.2):
        probe_calls.append(port)
        return False

    monkeypatch.setattr("unity_mcp.server_filtering._tcp_probe", fail_probe)
    # Probe is never called — candidate included in both modes
    assert _read_unity_port(skip_probe=False) == 9510
    assert _read_unity_port(skip_probe=True) == 9510
    assert not probe_calls  # probe was never called


# ---------------------------------------------------------------------------
# Phase 3: UNITY_MCP_PROJECT_DIR / CLAUDE_PROJECT_DIR env vars
# ---------------------------------------------------------------------------

def test_read_unity_port_uses_unity_mcp_project_dir(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR set → uses it for port-file matching instead of CWD."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    # ProjectA has older mtime — would lose without project dir matching
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # CWD is unrelated, but UNITY_MCP_PROJECT_DIR points to ProjectA
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "SomeOtherDir"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", project_a)
    assert _read_unity_port() == 9500


def test_read_unity_port_falls_back_to_claude_project_dir(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR not set, CLAUDE_PROJECT_DIR set → uses it."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("UNITY_MCP_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "SomeOtherDir"))
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", project_a)
    assert _read_unity_port() == 9500


def test_read_unity_port_falls_back_to_cwd(monkeypatch, tmp_path):
    """Neither env var set → uses getcwd() as before (backward compat)."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("UNITY_MCP_PROJECT_DIR", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    # CWD inside ProjectA → should match ProjectA despite older mtime
    monkeypatch.setattr("os.getcwd", lambda: project_a + "/Assets")
    assert _read_unity_port() == 9500


def test_read_unity_port_project_dir_matches_correct_of_three(monkeypatch, tmp_path):
    """3 port files for 3 projects; project_dir matches the middle one."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    proj_a = str(tmp_path / "ProjA")
    proj_b = str(tmp_path / "ProjB")
    proj_c = str(tmp_path / "ProjC")
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a, mtime=3000.0)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b, mtime=2000.0)
    _make_port_file_with_path(ports_dir, pid=103, port=9602, project_path=proj_c, mtime=1000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", proj_b)
    # ProjectB is middle — newest is A (9600), but env var selects B (9601)
    assert _read_unity_port() == 9601


def test_read_unity_port_empty_unity_mcp_project_dir_falls_through(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR="" (empty string) → falls through to CLAUDE_PROJECT_DIR."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    proj_a = str(tmp_path / "ProjA")
    proj_b = str(tmp_path / "ProjB")
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a, mtime=2000.0)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b, mtime=1000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", "")   # empty → falls through
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", proj_b)  # next level picks this up
    assert _read_unity_port() == 9601


def test_read_unity_port_unity_mcp_project_dir_priority_over_claude(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR takes priority over CLAUDE_PROJECT_DIR."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    proj_a = str(tmp_path / "ProjA")
    proj_b = str(tmp_path / "ProjB")
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a, mtime=2000.0)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b, mtime=1000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("unity_mcp.server_filtering._is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", proj_b)  # B: older, lower port
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", proj_a)      # A: newer, higher mtime
    # UNITY_MCP_PROJECT_DIR wins → 9601 (ProjB)
    assert _read_unity_port() == 9601
