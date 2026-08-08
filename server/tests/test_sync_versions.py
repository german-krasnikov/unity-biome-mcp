"""Tests for scripts/sync_versions.py --check mode (version-skew gate)."""
import json
import importlib.util
import os
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "sync_versions.py"
REPO_ROOT = Path(__file__).resolve().parents[2]
VERSION_ARTIFACTS = (
    "pyproject.toml",
    "uv.lock",
    "package.json",
    "__version__.py",
    "_meta.json",
    "MCPServer.cs",
)

SPEC = importlib.util.spec_from_file_location("sync_versions_under_test", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
SYNC_MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SYNC_MODULE)


def _write_fixture(root: Path, versions: dict) -> None:
    """Lay out the 5 version artifacts under root, each set to versions[name]."""
    (root / "server").mkdir(parents=True, exist_ok=True)
    (root / "server" / "src" / "unity_mcp").mkdir(parents=True, exist_ok=True)
    (root / "unity-plugin" / "Editor").mkdir(parents=True, exist_ok=True)
    (root / "docs" / "assets").mkdir(parents=True, exist_ok=True)

    (root / "server" / "pyproject.toml").write_text(
        f'[project]\nname = "unity-biome-mcp"\nversion = "{versions["pyproject.toml"]}"\n', encoding="utf-8"
    )
    (root / "server" / "uv.lock").write_text(
        "[[package]]\n"
        'name = "unity-biome-mcp"\n'
        f'version = "{versions["uv.lock"]}"\n'
        'source = { editable = "." }\n',
        encoding="utf-8",
    )
    (root / "unity-plugin" / "package.json").write_text(
        json.dumps({"name": "unity-biome-mcp", "version": versions["package.json"]}), encoding="utf-8"
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


_SUBPROCESS_ENV = {**os.environ, "PYTHONIOENCODING": "utf-8"}


def _run_check(root: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--check", "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
        env=_SUBPROCESS_ENV, timeout=60,
    )


def _run_sync(root: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SCRIPT), "--sync", "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
        env=_SUBPROCESS_ENV, timeout=60,
    )


def test_check_all_agree_exits_zero(tmp_path):
    _write_fixture(tmp_path, {key: "1.2.3" for key in VERSION_ARTIFACTS})

    result = _run_check(tmp_path)

    assert result.returncode == 0
    assert "1.2.3" in result.stdout


def test_check_disagreement_exits_nonzero(tmp_path):
    versions = {key: "1.2.3" for key in VERSION_ARTIFACTS}
    versions["MCPServer.cs"] = "1.2.4"
    _write_fixture(tmp_path, versions)

    result = _run_check(tmp_path)

    assert result.returncode == 1
    assert "MCPServer.cs" in result.stderr
    assert "1.2.3" in result.stderr and "1.2.4" in result.stderr


def test_check_detects_meta_plugin_version_disagreement(tmp_path):
    _write_fixture(tmp_path, {key: "1.2.3" for key in VERSION_ARTIFACTS})
    meta_path = tmp_path / "docs" / "assets" / "_meta.json"
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    meta["plugin_version"] = "1.2.4"
    meta_path.write_text(json.dumps(meta), encoding="utf-8")

    result = _run_check(tmp_path)

    assert result.returncode == 1
    assert "_meta.json.plugin_version: 1.2.4" in result.stderr


def test_check_rejects_missing_version_patterns(tmp_path):
    _write_fixture(tmp_path, {key: "1.2.3" for key in VERSION_ARTIFACTS})
    for path in (
        tmp_path / "server" / "pyproject.toml",
        tmp_path / "server" / "uv.lock",
        tmp_path / "unity-plugin" / "package.json",
        tmp_path / "server" / "src" / "unity_mcp" / "__version__.py",
        tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs",
    ):
        path.write_text("no version here\n", encoding="utf-8")
    (tmp_path / "docs" / "assets" / "_meta.json").write_text(
        '{"server_version": "?", "plugin_version": "?"}',
        encoding="utf-8",
    )

    result = _run_check(tmp_path)

    assert result.returncode == 1
    assert "version mismatch" in result.stderr


def test_sync_uses_pyproject_as_canonical_source(tmp_path):
    versions = {key: "1.2.3" for key in VERSION_ARTIFACTS}
    for key in versions:
        if key != "pyproject.toml":
            versions[key] = "0.9.0"
    _write_fixture(tmp_path, versions)

    result = _run_sync(tmp_path)

    assert result.returncode == 0, result.stderr
    check = _run_check(tmp_path)
    assert check.returncode == 0, check.stderr
    assert "versions in sync: 1.2.3" in check.stdout


@pytest.mark.parametrize("failure_index", range(1, 7))
def test_sync_rolls_back_every_artifact_on_replace_failure(
    tmp_path, monkeypatch, failure_index
):
    _write_fixture(tmp_path, {key: "1.2.3" for key in VERSION_ARTIFACTS})
    paths = [
        tmp_path / "server" / "pyproject.toml",
        tmp_path / "server" / "uv.lock",
        tmp_path / "unity-plugin" / "package.json",
        tmp_path / "server" / "src" / "unity_mcp" / "__version__.py",
        tmp_path / "docs" / "assets" / "_meta.json",
        tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs",
    ]
    originals = {path: path.read_bytes() for path in paths}
    real_replace = SYNC_MODULE.os.replace
    calls = 0

    def fail_once(source, destination):
        nonlocal calls
        calls += 1
        if calls == failure_index:
            raise OSError("injected replace failure")
        return real_replace(source, destination)

    monkeypatch.setattr(SYNC_MODULE.os, "replace", fail_once)

    with pytest.raises(SystemExit):
        SYNC_MODULE._sync(tmp_path, "2.0.0", update_canonical=True)

    assert {path: path.read_bytes() for path in paths} == originals
    assert not list(tmp_path.rglob("*.tmp"))
