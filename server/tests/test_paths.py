"""Regression guard: every ~/.unity-biome-mcp base-dir reference must go through
paths.unity_mcp_dir(), not a hand-rolled Path.home() / ".unity-biome-mcp" literal."""
import re
from pathlib import Path
from unittest.mock import patch

from unity_mcp.paths import iter_port_files

_REPO_ROOT = Path(__file__).resolve().parent.parent.parent
_SRC_ROOT = _REPO_ROOT / "server" / "src" / "unity_mcp"

_FILES_TO_CHECK = [
    _SRC_ROOT / "lockfile.py",
    _SRC_ROOT / "server_lifespan.py",
    _SRC_ROOT / "server_control.py",
    _SRC_ROOT / "unity_state.py",
    _SRC_ROOT / "_update_check.py",
    _SRC_ROOT / "crash_log.py",
    _SRC_ROOT / "doctor.py",
    _SRC_ROOT / "budget" / "cost_tracker.py",
    _REPO_ROOT / "install" / "commands.py",
]

_BYPASS_RE = re.compile(r'Path\.home\(\)\s*/\s*[\'"]\.unity-(?:mcp|biome-mcp)[\'"]')


def test_all_unity_mcp_dir_consumers_use_canonical_helper():
    offenders = []
    for f in _FILES_TO_CHECK:
        text = f.read_text(encoding="utf-8")
        if _BYPASS_RE.search(text):
            offenders.append(str(f.relative_to(_REPO_ROOT)))
    assert not offenders, f"Bypasses canonical unity_mcp_dir(): {offenders}"


# --- iter_port_files ---

def test_iter_port_files_finds_primary_dir(tmp_path):
    primary = tmp_path / "ports"
    primary.mkdir()
    (primary / "111.port").write_text("9500\n", encoding="utf-8")
    found = list(iter_port_files("*.port", primary_dir=primary))
    assert [f.name for f in found] == ["111.port"]


def test_iter_port_files_finds_legacy_dir(tmp_path):
    legacy = tmp_path / ".unity-mcp" / "ports"
    legacy.mkdir(parents=True)
    (legacy / "222.port").write_text("9501\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path):
        found = list(iter_port_files("*.port", primary_dir=tmp_path / "nonexistent"))
    assert [f.name for f in found] == ["222.port"]


def test_iter_port_files_deduplicates(tmp_path):
    primary = tmp_path / "new"
    primary.mkdir()
    (primary / "333.port").write_text("9500\n", encoding="utf-8")
    legacy = tmp_path / "old" / ".unity-mcp" / "ports"
    legacy.mkdir(parents=True)
    (legacy / "333.port").write_text("9999\n", encoding="utf-8")
    with patch.object(Path, "home", return_value=tmp_path / "old"):
        found = list(iter_port_files("*.port", primary_dir=primary))
    assert len(found) == 1
    assert found[0].read_text(encoding="utf-8") == "9500\n"  # primary (new dir) wins
