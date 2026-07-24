"""Tests for install connect/disconnect commands."""
import argparse
import json
from pathlib import Path
from unittest.mock import MagicMock

import pytest

import install.commands as cmds


# ── helpers ───────────────────────────────────────────────────────────────────

def _make_manifest(tmp_path: Path, extra_deps: dict | None = None,
                   testables: list | None = None) -> Path:
    pkg = tmp_path / "Packages"
    pkg.mkdir()
    manifest = pkg / "manifest.json"
    data: dict = {"dependencies": {"com.unity.mathematics": "1.2.0"}}
    if extra_deps:
        data["dependencies"].update(extra_deps)
    if testables is not None:
        data["testables"] = testables
    manifest.write_text(json.dumps(data, indent=2) + "\n", "utf-8")
    return manifest


def _args(unity_project: Path) -> argparse.Namespace:
    return argparse.Namespace(unity_project=str(unity_project))


def _ui():
    m = MagicMock()
    m.ok = MagicMock()
    m.error = MagicMock()
    return m


# ── connect ───────────────────────────────────────────────────────────────────

def test_connect_adds_file_entries(tmp_path):
    _make_manifest(tmp_path)
    ui = _ui()
    rc = cmds.cmd_connect(_args(tmp_path), ui)
    assert rc == 0
    data = json.loads((tmp_path / "Packages" / "manifest.json").read_text("utf-8"))
    assert "com.unity-biome-mcp.editor" in data["dependencies"]
    assert "com.unity-biome-mcp.reload" in data["dependencies"]
    assert data["dependencies"]["com.unity-biome-mcp.editor"].startswith("file:")
    assert data["dependencies"]["com.unity-biome-mcp.reload"].startswith("file:")


def test_connect_idempotent(tmp_path):
    _make_manifest(tmp_path)
    ui = _ui()
    cmds.cmd_connect(_args(tmp_path), ui)
    # Second call — should detect already connected
    rc = cmds.cmd_connect(_args(tmp_path), ui)
    assert rc == 0
    data = json.loads((tmp_path / "Packages" / "manifest.json").read_text("utf-8"))
    # Entry appears exactly once
    assert list(data["dependencies"]).count("com.unity-biome-mcp.editor") == 1
    ui.ok.assert_called()


def test_connect_preserves_other_deps(tmp_path):
    _make_manifest(tmp_path)
    cmds.cmd_connect(_args(tmp_path), _ui())
    data = json.loads((tmp_path / "Packages" / "manifest.json").read_text("utf-8"))
    assert "com.unity.mathematics" in data["dependencies"]


def test_connect_creates_backup(tmp_path):
    _make_manifest(tmp_path)
    cmds.cmd_connect(_args(tmp_path), _ui())
    assert (tmp_path / "Packages" / "manifest.json.bak").exists()


def test_connect_adds_reload_to_existing_editor_project(tmp_path):
    """Item 26: editor present, reload missing → both added, returns 0."""
    editor_path = (Path(__file__).parent.parent.parent / "unity-plugin").resolve()
    _make_manifest(tmp_path, extra_deps={
        "com.unity-biome-mcp.editor": f"file:{editor_path.as_posix()}"
    })
    ui = _ui()
    rc = cmds.cmd_connect(_args(tmp_path), ui)
    assert rc == 0
    data = json.loads((tmp_path / "Packages" / "manifest.json").read_text("utf-8"))
    assert "com.unity-biome-mcp.reload" in data["dependencies"]
    assert data["dependencies"]["com.unity-biome-mcp.reload"].startswith("file:")


def test_connect_skips_when_both_present(tmp_path):
    """Item 26: both packages present → return 0 with ok message, no backup needed."""
    editor_path = (Path(__file__).parent.parent.parent / "unity-plugin").resolve()
    reload_path = (Path(__file__).parent.parent.parent / "unity-plugin-reload").resolve()
    _make_manifest(tmp_path, extra_deps={
        "com.unity-biome-mcp.editor": f"file:{editor_path.as_posix()}",
        "com.unity-biome-mcp.reload": f"file:{reload_path.as_posix()}",
    })
    ui = _ui()
    rc = cmds.cmd_connect(_args(tmp_path), ui)
    assert rc == 0
    ui.ok.assert_called()


def test_connect_invalid_path(tmp_path):
    ui = _ui()
    rc = cmds.cmd_connect(_args(tmp_path), ui)  # No Packages/ dir
    assert rc == 1
    ui.error.assert_called()


# ── disconnect ────────────────────────────────────────────────────────────────

def test_disconnect_removes_entries(tmp_path):
    _make_manifest(tmp_path)
    cmds.cmd_connect(_args(tmp_path), _ui())
    rc = cmds.cmd_disconnect(_args(tmp_path), _ui())
    assert rc == 0
    data = json.loads((tmp_path / "Packages" / "manifest.json").read_text("utf-8"))
    assert "com.unity-biome-mcp.editor" not in data["dependencies"]
    assert "com.unity-biome-mcp.reload" not in data["dependencies"]


def test_disconnect_removes_testables(tmp_path):
    _make_manifest(tmp_path, testables=["com.unity-biome-mcp.editor", "com.unity-biome-mcp.reload"])
    # Manually inject deps so disconnect sees something to remove
    manifest = tmp_path / "Packages" / "manifest.json"
    data = json.loads(manifest.read_text("utf-8"))
    data["dependencies"]["com.unity-biome-mcp.editor"] = "file:/foo"
    data["dependencies"]["com.unity-biome-mcp.reload"] = "file:/bar"
    manifest.write_text(json.dumps(data, indent=2) + "\n", "utf-8")

    cmds.cmd_disconnect(_args(tmp_path), _ui())
    data = json.loads(manifest.read_text("utf-8"))
    assert "com.unity-biome-mcp.editor" not in data.get("testables", [])
    assert "com.unity-biome-mcp.reload" not in data.get("testables", [])


def test_disconnect_noop_when_not_connected(tmp_path):
    _make_manifest(tmp_path)
    ui = _ui()
    rc = cmds.cmd_disconnect(_args(tmp_path), ui)
    assert rc == 0
    ui.ok.assert_called()


def test_disconnect_creates_backup(tmp_path):
    _make_manifest(tmp_path)
    cmds.cmd_connect(_args(tmp_path), _ui())
    # Remove backup from connect to verify disconnect makes its own
    (tmp_path / "Packages" / "manifest.json.bak").unlink()
    cmds.cmd_disconnect(_args(tmp_path), _ui())
    assert (tmp_path / "Packages" / "manifest.json.bak").exists()


def test_disconnect_invalid_path(tmp_path):
    ui = _ui()
    rc = cmds.cmd_disconnect(_args(tmp_path), ui)
    assert rc == 1
    ui.error.assert_called()
