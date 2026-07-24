"""Tests for migrate_data_dir() in paths.py."""
from pathlib import Path
from unittest.mock import patch
import pytest
from unity_mcp.paths import migrate_data_dir, unity_mcp_dir, ports_dir


def test_migrate_renames_old_dir(tmp_path):
    """Happy path: ~/.unity-mcp exists, ~/.unity-biome-mcp doesn't → rename."""
    old = tmp_path / ".unity-mcp"
    old.mkdir()
    (old / "budget.json").write_text("{}", encoding="utf-8")
    (old / "ports").mkdir()
    (old / "ports" / "1234.port").write_text("9500", encoding="utf-8")
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        migrate_data_dir()
    assert not old.exists(), "old dir must be gone"
    new = tmp_path / ".unity-biome-mcp"
    assert new.exists()
    assert (new / "budget.json").exists(), "files preserved"
    assert (new / "ports" / "1234.port").exists(), "port files preserved"


def test_migrate_skips_when_new_exists(tmp_path):
    """Both dirs exist: new wins, old stays untouched."""
    old = tmp_path / ".unity-mcp"
    old.mkdir()
    (old / "canary").write_text("old", encoding="utf-8")
    new = tmp_path / ".unity-biome-mcp"
    new.mkdir()
    (new / "canary").write_text("new", encoding="utf-8")
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        migrate_data_dir()
    assert old.exists(), "old dir must stay when new already exists"
    assert (new / "canary").read_text(encoding="utf-8") == "new", "new dir content unchanged"


def test_migrate_noop_when_old_missing(tmp_path):
    """Fresh install: no old dir → no-op, no error."""
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        migrate_data_dir()  # must not raise
    assert not (tmp_path / ".unity-biome-mcp").exists()
    assert not (tmp_path / ".unity-mcp").exists()


def test_migrate_skips_symlink(tmp_path, capsys):
    """Old dir is a symlink → skip with stderr message."""
    target = tmp_path / "real_dir"
    target.mkdir()
    old = tmp_path / ".unity-mcp"
    old.symlink_to(target)
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        migrate_data_dir()
    assert old.exists(), "symlink must remain"
    assert not (tmp_path / ".unity-biome-mcp").exists()
    captured = capsys.readouterr()
    assert "symlink" in captured.err, "must warn about symlink on stderr"


def test_unity_mcp_dir_points_to_new_name(tmp_path):
    """unity_mcp_dir() must return ~/.unity-biome-mcp, not ~/.unity-mcp."""
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        result = unity_mcp_dir()
    assert result.name == ".unity-biome-mcp"


def test_ports_dir_under_new_name(tmp_path):
    """ports_dir() must be ~/.unity-biome-mcp/ports."""
    with patch("unity_mcp.paths.Path.home", return_value=tmp_path):
        result = ports_dir()
    assert result.parts[-2] == ".unity-biome-mcp"
    assert result.name == "ports"
