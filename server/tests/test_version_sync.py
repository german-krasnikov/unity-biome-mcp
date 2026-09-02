"""TDD: sync_versions.py — updates pyproject.toml, package.json, __version__.py."""
import os
import subprocess
import sys
import textwrap
from pathlib import Path

import pytest

SYNC_SCRIPT = Path(__file__).parents[2] / "scripts" / "sync_versions.py"


def run_sync(version: str, root: Path) -> subprocess.CompletedProcess:
    # PYTHONIOENCODING=utf-8: sync_versions.py prints → (U+2192) which fails
    # on Windows pipes using the default cp1252 encoding.
    return subprocess.run(
        [sys.executable, str(SYNC_SCRIPT), version, "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
        env={**os.environ, "PYTHONIOENCODING": "utf-8"}, timeout=60,
    )


@pytest.fixture()
def project_root(tmp_path: Path) -> Path:
    """Minimal project tree mirroring real layout."""
    (tmp_path / "server" / "src" / "unity_mcp").mkdir(parents=True)
    (tmp_path / "unity-plugin" / "Editor").mkdir(parents=True)
    (tmp_path / "docs" / "assets").mkdir(parents=True)

    (tmp_path / "server" / "pyproject.toml").write_text(textwrap.dedent("""\
        [project]
        name = "unity-biome-mcp"
        version = "0.8.2"
        description = "MCP server"
    """), encoding="utf-8")
    (tmp_path / "server" / "uv.lock").write_text(textwrap.dedent("""\
        [[package]]
        name = "unity-biome-mcp"
        version = "0.8.2"
        source = { editable = "." }
    """), encoding="utf-8")

    (tmp_path / "unity-plugin" / "package.json").write_text(
        '{\n  "name": "com.unity-biome-mcp.editor",\n  "version": "0.8.2"\n}\n',
        encoding="utf-8"
    )

    (tmp_path / "server" / "src" / "unity_mcp" / "__version__.py").write_text(
        '__version__ = "0.8.2"\n', encoding="utf-8"
    )

    (tmp_path / "docs" / "assets" / "_meta.json").write_text(
        '{\n  "server_version": "0.8.2",\n  "plugin_version": "0.8.2"\n}\n',
        encoding="utf-8"
    )

    (tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs").write_text(
        'internal static string PluginVersion => "0.8.2";\n',
        encoding="utf-8"
    )

    (tmp_path / "unity-plugin" / "Editor" / "BiomeVersion.cs").write_text(
        'namespace UnityMCP.Editor\n'
        '{\n'
        '    internal static class BiomeVersion\n'
        '    {\n'
        '        public const string Plugin = "0.8.2";\n'
        '        public const int Protocol = 3;\n'
        '    }\n'
        '}\n',
        encoding="utf-8"
    )

    (tmp_path / "scripts" / "gauntlet").mkdir(parents=True)
    (tmp_path / "scripts" / "gauntlet" / "release-policy.json").write_text(
        '{\n  "activation_product_version": "0.8.2"\n}\n', encoding="utf-8"
    )

    return tmp_path


def test_sync_versions_preserves_other_content(project_root: Path):
    result = run_sync("2.0.0", project_root)
    assert result.returncode == 0, result.stderr

    pyproject = (project_root / "server" / "pyproject.toml").read_text(encoding="utf-8")
    assert 'name = "unity-biome-mcp"' in pyproject
    assert 'description = "MCP server"' in pyproject

    biome_version_cs = (project_root / "unity-plugin" / "Editor" / "BiomeVersion.cs").read_text(encoding="utf-8")
    assert 'Plugin = "2.0.0"' in biome_version_cs
    assert "public const int Protocol = 3;" in biome_version_cs


def test_sync_versions_invalid_version(project_root: Path):
    result = run_sync("not-semver", project_root)
    assert result.returncode != 0
    assert "semver" in result.stderr.lower() or "invalid" in result.stderr.lower()


def test_sync_versions_invalid_version_empty(project_root: Path):
    result = run_sync("", project_root)
    assert result.returncode != 0
