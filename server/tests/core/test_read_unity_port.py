"""Tests for _read_unity_port — filesystem/env/PID mocking only, no global state leaks."""
import os
from unittest.mock import patch

import pytest

from unity_mcp.server import _read_unity_port


@pytest.fixture(autouse=True)
def _mock_tcp_probe():
    """Bypass TCP probe in all read_unity_port tests — they test PID/FS logic only."""
    with patch("unity_mcp.server_filtering._tcp_probe", return_value=True):
        yield


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_port_file(ports_dir, pid: int, port: int, project: str = "MyProject"):
    """Write a <port>.port file (stem=port, content[0]=pid)."""
    f = ports_dir / f"{port}.port"
    f.write_text(f"{pid}\n\n{project}\n", encoding="utf-8")
    return f


def _make_port_file_with_path(ports_dir, pid: int, port: int, project_path: str,
                               project: str = "MyProject"):
    """Write a <port>.port file with project path on line 2."""
    f = ports_dir / f"{port}.port"
    f.write_text(f"{pid}\n{project_path}\n{project}\n", encoding="utf-8")
    return f


# ---------------------------------------------------------------------------
# Env override
# ---------------------------------------------------------------------------

def test_env_override_returns_that_port(monkeypatch, tmp_path):
    monkeypatch.setenv("UNITY_MCP_PORT", "9999")
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
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9501


# ---------------------------------------------------------------------------
# Stale PID — skipped (file not removed by shim; phantom_cleaner handles that)
# ---------------------------------------------------------------------------

def test_dead_pid_skipped_returns_default(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=9999, port=9501)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: False)
    assert _read_unity_port() == 9500


def test_permission_error_keeps_file_as_live_candidate(monkeypatch, tmp_path):
    """is_pid_alive returns True (alive, different user) — port is valid candidate."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=9998, port=9502)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9502


def test_dead_pid_all_gone_returns_default(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=9997, port=9503)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: False)
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# Mixed: one dead, one live → live one wins
# ---------------------------------------------------------------------------

def test_mixed_dead_and_live_returns_live_port(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=7777, port=9800)
    _make_port_file(ports_dir, pid=8888, port=9801)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: pid != 7777)
    assert _read_unity_port() == 9801


# ---------------------------------------------------------------------------
# Malformed file: non-integer stem → skipped
# ---------------------------------------------------------------------------

def test_malformed_port_file_skipped(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad = ports_dir / "notanint.port"
    bad.write_text("broken\n", encoding="utf-8")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# project field: missing third line doesn't crash
# ---------------------------------------------------------------------------

def test_lockfile_without_project_line(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "9505.port"
    f.write_text("5555\n", encoding="utf-8")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    assert _read_unity_port() == 9505


# ---------------------------------------------------------------------------
# CWD-based port discovery
# ---------------------------------------------------------------------------

def test_cwd_exact_match_project_root(monkeypatch, tmp_path):
    """CWD is exactly the project root — still matches."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: project_a)
    assert _read_unity_port() == 9500


def test_cwd_match_returns_matching_project_port(monkeypatch, tmp_path):
    """CWD inside project dir → returns that port even if another port file exists."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: project_a + "/Assets/Scripts")
    assert _read_unity_port() == 9500


def test_cwd_no_match_returns_first_alphabetically(monkeypatch, tmp_path):
    """CWD doesn't match any project → first alphabetically alive port file wins."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "OtherDir"))
    assert _read_unity_port() == 9500  # first alphabetically


def test_cwd_nested_projects_prefers_longest_match(monkeypatch, tmp_path):
    """Two overlapping project paths → prefer the longer (more specific) one."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_root = str(tmp_path / "Workspace")
    project_sub = str(tmp_path / "Workspace" / "SubProject")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_root)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_sub)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: project_sub + "/Assets")
    assert _read_unity_port() == 9501


# ---------------------------------------------------------------------------
# skip_probe: does not call TCP probe in either mode
# ---------------------------------------------------------------------------

def test_skip_probe_does_not_call_tcp_probe(monkeypatch, tmp_path):
    """TCP probe is never called — PID liveness is the only gate."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=1111, port=9510)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    probe_calls = []

    def fail_probe(port, timeout=0.2):
        probe_calls.append(port)
        return False

    monkeypatch.setattr("unity_mcp.server_filtering._tcp_probe", fail_probe)
    assert _read_unity_port(skip_probe=False) == 9510
    assert _read_unity_port(skip_probe=True) == 9510
    assert not probe_calls


def test_skip_probe_true_returns_none_when_no_candidates(monkeypatch, tmp_path):
    """skip_probe=True returns None when no port files found (no silent 9500 drift)."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port(skip_probe=True) is None


# ---------------------------------------------------------------------------
# UNITY_MCP_PROJECT_DIR / CLAUDE_PROJECT_DIR env vars
# ---------------------------------------------------------------------------

def test_read_unity_port_uses_unity_mcp_project_dir(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR set → uses it for port-file matching instead of CWD."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
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
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "SomeOtherDir"))
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", project_a)
    assert _read_unity_port() == 9500


def test_read_unity_port_falls_back_to_cwd(monkeypatch, tmp_path):
    """Neither env var set → uses getcwd() (backward compat)."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("UNITY_MCP_PROJECT_DIR", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
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
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b)
    _make_port_file_with_path(ports_dir, pid=103, port=9602, project_path=proj_c)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", proj_b)
    assert _read_unity_port() == 9601


def test_read_unity_port_empty_unity_mcp_project_dir_falls_through(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR="" (empty string) → falls through to CLAUDE_PROJECT_DIR."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    proj_a = str(tmp_path / "ProjA")
    proj_b = str(tmp_path / "ProjB")
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", "")
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", proj_b)
    assert _read_unity_port() == 9601


def test_read_unity_port_unity_mcp_project_dir_priority_over_claude(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR takes priority over CLAUDE_PROJECT_DIR."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    proj_a = str(tmp_path / "ProjA")
    proj_b = str(tmp_path / "ProjB")
    _make_port_file_with_path(ports_dir, pid=101, port=9600, project_path=proj_a)
    _make_port_file_with_path(ports_dir, pid=102, port=9601, project_path=proj_b)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", proj_b)
    monkeypatch.setenv("CLAUDE_PROJECT_DIR", proj_a)
    assert _read_unity_port() == 9601


def test_blank_second_line_project_dir_selects_valid_port(monkeypatch, tmp_path):
    """UNITY_MCP_PROJECT_DIR selects port whose project_path matches; blank line loses."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    monkeypatch.delenv("CLAUDE_PROJECT_DIR", raising=False)
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    good_project = str(tmp_path / "FreshProject")
    bad = ports_dir / "9600.port"
    good = ports_dir / "9601.port"
    bad.write_text("1111\n\nBadProject\n", encoding="utf-8")   # blank line 2 → no project_path
    good.write_text(f"2222\n{good_project}\nGoodProject\n", encoding="utf-8")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("biome_mcp.cli.resolver.is_pid_alive", lambda pid: True)
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "Unrelated"))
    monkeypatch.setenv("UNITY_MCP_PROJECT_DIR", good_project)
    assert _read_unity_port() == 9601
