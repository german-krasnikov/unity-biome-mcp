"""Tests for scripts/sync_versions.py --check mode (version-skew gate)."""
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "sync_versions.py"
REPO_ROOT = Path(__file__).resolve().parents[2]


def _write_fixture(root: Path, versions: dict) -> None:
    """Lay out the 5 version artifacts under root, each set to versions[name]."""
    (root / "server").mkdir(parents=True, exist_ok=True)
    (root / "server" / "src" / "unity_mcp").mkdir(parents=True, exist_ok=True)
    (root / "unity-plugin" / "Editor").mkdir(parents=True, exist_ok=True)
    (root / "docs" / "assets").mkdir(parents=True, exist_ok=True)

    (root / "server" / "pyproject.toml").write_text(
        f'[project]\nname = "unity-mcp"\nversion = "{versions["pyproject.toml"]}"\n', encoding="utf-8"
    )
    (root / "unity-plugin" / "package.json").write_text(
        json.dumps({"name": "unity-mcp", "version": versions["package.json"]}), encoding="utf-8"
    )
    (root / "server" / "src" / "unity_mcp" / "__version__.py").write_text(
        f'__version__ = "{versions["__version__.py"]}"\n', encoding="utf-8"
    )
    (root / "docs" / "assets" / "_meta.json").write_text(
        json.dumps({"server_version": versions["_meta.json"], "plugin_version": versions["_meta.json"]}),
        encoding="utf-8",
    )
    (root / "unity-plugin" / "Editor" / "MCPServer.cs").write_text(
        "internal static class MCPServer {\n"
        f'    internal static string PluginVersion => "{versions["MCPServer.cs"]}";\n'
        "}\n",
        encoding="utf-8",
    )


def _run_check(root: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--check", "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
    )


def test_check_all_agree_exits_zero(tmp_path):
    _write_fixture(tmp_path, {k: "1.2.3" for k in (
        "pyproject.toml", "package.json", "__version__.py", "_meta.json", "MCPServer.cs"
    )})

    result = _run_check(tmp_path)

    assert result.returncode == 0
    assert "1.2.3" in result.stdout


def test_check_disagreement_exits_nonzero(tmp_path):
    versions = {k: "1.2.3" for k in (
        "pyproject.toml", "package.json", "__version__.py", "_meta.json", "MCPServer.cs"
    )}
    versions["MCPServer.cs"] = "1.2.4"
    _write_fixture(tmp_path, versions)

    result = _run_check(tmp_path)

    assert result.returncode == 1
    assert "MCPServer.cs" in result.stderr
    assert "1.2.3" in result.stderr and "1.2.4" in result.stderr
