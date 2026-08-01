"""Port file lifecycle tests — stale detection, PID-alive checks, CWD selection.

All tests run without Unity (marker: not live).
Tests complement test_reload_stability.py Group B — focus on:
  TC-1:  read_reload_port skips dead PID
  TC-3:  cleanup with tcp_probe=True removes PID-alive but TCP-dead files
  TC-4:  cleanup without tcp_probe keeps PID-alive files
  TC-5:  C# gap resolved — PortFileManager now cleans both *.port and *.reload-port
  TC-6:  read_reload_port multiple candidates → newest mtime among alive wins
  TC-7:  read_unity_port CWD prefix match beats newer mtime
  TC-8:  read_unity_port(skip_probe=True) returns None when all PIDs dead
  TC-10: cleanup mixed live/dead across all three file patterns atomically
  TC-11: read_reload_port CWD match beats mtime among alive candidates
"""
import os
from pathlib import Path
from unittest.mock import patch

import pytest

from unity_mcp.lockfile import (
    cleanup_stale_port_files,
    read_reload_port,
)
from unity_mcp.server_filtering import read_unity_port


# ---------------------------------------------------------------------------
# Paths (Group C style — source verification without Unity)
# ---------------------------------------------------------------------------
_PROJECT = Path(__file__).parents[2]
_PLUGIN = _PROJECT / "unity-plugin"
_SERVER = _PROJECT / "server"


# ---------------------------------------------------------------------------
# TC-5 — source verification: C# gap + Python coverage
# ---------------------------------------------------------------------------

def test_csharp_port_manager_cleans_reload_port_files():
    """PortFileManager.cs CleanStalePeerPortFiles covers *.reload-port — TC-5 gap resolved.

    C# now globs both *.port and *.reload-port from dead PIDs.
    Python cleanup_stale_port_files() is still the primary cleanup path,
    but C# no longer leaves stale *.reload-port files accumulating.
    """
    src = (_PLUGIN / "Editor/PortFileManager.cs").read_text(encoding="utf-8")
    assert "*.reload-port" in src, (
        "C# cleanup must include *.reload-port — TC-5 gap was resolved; keep it"
    )


def test_python_cleanup_stale_port_files_covers_reload_port():
    """lockfile.py cleanup_stale_port_files handles *.reload-port pattern.

    Python compensates for the C# gap documented in TC-5.
    """
    src = (_SERVER / "src/unity_mcp/lockfile.py").read_text(encoding="utf-8")
    assert '"*.reload-port"' in src, "Python cleanup must include *.reload-port pattern"


# ---------------------------------------------------------------------------
# TC-1 — read_reload_port: dead PID → returns None
# ---------------------------------------------------------------------------

def test_read_reload_port_skips_dead_pid(tmp_path):
    """Dead PID port file must never be returned by read_reload_port()."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "82875.reload-port").write_text("9600\n/proj\n", encoding="utf-8")

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        result = read_reload_port()

    assert result is None


# ---------------------------------------------------------------------------
# TC-3 — cleanup with tcp_probe=True removes PID-alive but TCP-dead files
# ---------------------------------------------------------------------------

def test_cleanup_tcp_probe_removes_pid_alive_tcp_dead(tmp_path):
    """tcp_probe=True: PID alive but port not listening → file removed."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "44444.port").write_text("9700\n/proj\n", encoding="utf-8")

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile._tcp_probe", return_value=False):
        count = cleanup_stale_port_files(tcp_probe=True)

    assert count == 1
    assert not (ports_dir / "44444.port").exists()


# ---------------------------------------------------------------------------
# TC-4 — cleanup without tcp_probe keeps PID-alive TCP-dead files
# ---------------------------------------------------------------------------

def test_cleanup_no_tcp_probe_keeps_pid_alive_tcp_dead(tmp_path):
    """tcp_probe=False (default): PID alive → file kept regardless of TCP state."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "44444.port").write_text("9700\n/proj\n", encoding="utf-8")

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        count = cleanup_stale_port_files(tcp_probe=False)

    assert count == 0
    assert (ports_dir / "44444.port").exists()


# ---------------------------------------------------------------------------
# TC-6 — read_reload_port: multiple candidates → newest mtime among alive wins
# ---------------------------------------------------------------------------

def test_read_reload_port_multiple_candidates_picks_newest_alive(tmp_path):
    """Three candidates: 1 dead, 2 alive with different mtimes → newest alive wins."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()

    dead = ports_dir / "11111.reload-port"
    dead.write_text("9601\n/other\n", encoding="utf-8")
    os.utime(dead, (100, 100))

    alive_new = ports_dir / "22222.reload-port"
    alive_new.write_text("9602\n/proj\n", encoding="utf-8")
    os.utime(alive_new, (200, 200))

    alive_old = ports_dir / "33333.reload-port"
    alive_old.write_text("9603\n/proj2\n", encoding="utf-8")
    os.utime(alive_old, (50, 50))

    def is_alive(pid):
        return pid in (22222, 33333)

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", side_effect=is_alive):
        result = read_reload_port()

    assert result == 9602, "Newest mtime among alive candidates must win"


# ---------------------------------------------------------------------------
# TC-7 — read_unity_port: CWD prefix match beats newer mtime
# ---------------------------------------------------------------------------

def test_read_unity_port_cwd_prefix_wins_over_mtime(tmp_path):
    """Two Unity instances: one with newer mtime, one matching CWD → CWD wins."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()

    # Newer mtime but different project path
    other = ports_dir / "11111.port"
    other.write_text("9500\n/other/project\nOther\n", encoding="utf-8")
    os.utime(other, (200, 200))

    # Older mtime but CWD-matching path
    mine = ports_dir / "22222.port"
    mine.write_text("9501\n/my/project\nMy\n", encoding="utf-8")
    os.utime(mine, (100, 100))

    def is_alive(pid):
        return True

    with patch("unity_mcp.server_filtering._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.server_filtering._iter_port_files",
               side_effect=lambda p, d: d.glob(p)), \
         patch("unity_mcp.server_filtering._is_pid_alive", side_effect=is_alive), \
         patch.dict(os.environ, {}, clear=False), \
         patch("os.getcwd", return_value="/my/project/subdir"):
        # Remove env overrides that could bypass CWD selection
        os.environ.pop("UNITY_MCP_PORT", None)
        os.environ.pop("UNITY_MCP_PROJECT_DIR", None)
        os.environ.pop("CLAUDE_PROJECT_DIR", None)
        result = read_unity_port()

    assert result == 9501, "CWD-matching project must be selected over newer mtime"


# ---------------------------------------------------------------------------
# TC-8 — read_unity_port(skip_probe=True): no live candidates → None
# ---------------------------------------------------------------------------

def test_read_unity_port_skip_probe_returns_none_no_candidates(tmp_path):
    """skip_probe=True with all PIDs dead → None (no fallback to 9500).

    Bridge uses skip_probe=True during reconnect to avoid spurious port drift.
    """
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "99999.port").write_text("9500\n/proj\n", encoding="utf-8")

    with patch("unity_mcp.server_filtering._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.server_filtering._iter_port_files",
               side_effect=lambda p, d: d.glob(p)), \
         patch("unity_mcp.server_filtering._is_pid_alive", return_value=False), \
         patch.dict(os.environ, {}, clear=False):
        os.environ.pop("UNITY_MCP_PORT", None)
        result = read_unity_port(skip_probe=True)

    assert result is None, "skip_probe=True with no alive PIDs must return None, not 9500"


# ---------------------------------------------------------------------------
# TC-10 — cleanup: mixed live/dead across all three patterns atomically
# ---------------------------------------------------------------------------

def test_cleanup_all_patterns_mixed_live_dead(tmp_path):
    """Dead PID: all 3 file types removed. Alive PID: both files kept."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()

    # Dead PID — all three patterns
    (ports_dir / "11111.port").write_text("9500\n/proj\n", encoding="utf-8")
    (ports_dir / "11111.chat-port").write_text("9510\n/proj\n", encoding="utf-8")
    (ports_dir / "11111.reload-port").write_text("9600\n/proj\n", encoding="utf-8")

    # Alive PID — two patterns
    (ports_dir / "22222.port").write_text("9501\n/proj\n", encoding="utf-8")
    (ports_dir / "22222.reload-port").write_text("9601\n/proj\n", encoding="utf-8")

    def is_alive(pid):
        return pid == 22222

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", side_effect=is_alive):
        count = cleanup_stale_port_files()

    assert count == 3, "Three dead-PID files must be removed"
    remaining = {f.name for f in ports_dir.iterdir()}
    assert remaining == {"22222.port", "22222.reload-port"}


# ---------------------------------------------------------------------------
# TC-11 — read_reload_port: CWD match beats mtime among alive candidates
# ---------------------------------------------------------------------------

def test_read_reload_port_cwd_match_beats_mtime(tmp_path):
    """Among alive candidates, CWD-matching project_path wins over newer mtime."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()

    # Newer mtime, different project
    newer = ports_dir / "11111.reload-port"
    newer.write_text("9610\n/other\n", encoding="utf-8")
    os.utime(newer, (300, 300))

    # Older mtime, but CWD matches
    cwd_match = ports_dir / "22222.reload-port"
    cwd_match.write_text("9611\n/my/project\n", encoding="utf-8")
    os.utime(cwd_match, (100, 100))

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True), \
         patch("os.getcwd", return_value="/my/project"):
        result = read_reload_port()

    assert result == 9611, "CWD-matching project_path must win over newer mtime"
