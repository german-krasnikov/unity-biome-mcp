"""TDD: Task 6 — version rollback (server_git_url, version --list/--set, sync_versions patchers).

All tests are unit-level (not live, no network, no Unity required).
"""
import json
import re
import subprocess
import sys
import textwrap
from argparse import Namespace
from pathlib import Path
from unittest.mock import MagicMock, call, patch

import pytest

# ── resolver ──────────────────────────────────────────────────────────────────

from unity_mcp.config.resolver import GIT_INSTALL_URL, server_git_url


def test_server_git_url_no_ref_returns_head_url():
    assert server_git_url() == GIT_INSTALL_URL
    assert "@v" not in server_git_url()


def test_server_git_url_with_ref_inserts_tag():
    url = server_git_url("0.54.1")
    assert "@v0.54.1" in url
    assert "#subdirectory=server" in url
    # tag must appear BEFORE the fragment
    assert url.index("@v0.54.1") < url.index("#subdirectory")


def test_server_git_url_with_v_prefix_normalises():
    assert server_git_url("v0.54.1") == server_git_url("0.54.1")


def test_server_git_url_rejects_malformed_ref():
    with pytest.raises(ValueError):
        server_git_url("not-semver")


def test_server_git_url_rejects_two_part_version():
    with pytest.raises(ValueError):
        server_git_url("0.54")


def test_server_git_url_correct_form():
    url = server_git_url("1.2.3")
    expected = "git+https://github.com/german-krasnikov/unity-biome-mcp.git@v1.2.3#subdirectory=server"
    assert url == expected


# ── install.py — version --list (offline) ────────────────────────────────────

INSTALL_PY = Path(__file__).parents[2] / "install.py"

# We import install.py helpers by running a small loader
def _import_install():
    """Import install.py as a module (it's not a package)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("install_main", INSTALL_PY)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


@pytest.fixture(scope="module")
def install_mod():
    return _import_install()


SAMPLE_CHANGELOG = textwrap.dedent("""\
    # Changelog

    ## [Unreleased]

    ## [v0.55.2] — 2026-06-24
    - Feature A

    ## [v0.55.1] — 2026-06-22
    - Fix B

    ## [v0.54.1] — 2026-06-15
    - Fix C

    ## [v0.54.0] — 2026-06-10
    - Feature D
""")


def test_version_list_offline_parses_changelog(tmp_path, install_mod):
    p = tmp_path / "CHANGELOG.md"
    p.write_text(SAMPLE_CHANGELOG, encoding="utf-8")
    versions = install_mod._version_list_offline(p)
    ver_nums = [v for v, _ in versions]
    assert "0.55.2" in ver_nums
    assert "0.54.1" in ver_nums


def test_version_list_offline_has_dates(tmp_path, install_mod):
    p = tmp_path / "CHANGELOG.md"
    p.write_text(SAMPLE_CHANGELOG, encoding="utf-8")
    versions = install_mod._version_list_offline(p)
    dates = {v: d for v, d in versions}
    assert dates["0.55.2"] == "2026-06-24"
    assert dates["0.54.1"] == "2026-06-15"


def test_version_list_offline_excludes_unreleased(tmp_path, install_mod):
    p = tmp_path / "CHANGELOG.md"
    p.write_text(SAMPLE_CHANGELOG, encoding="utf-8")
    versions = install_mod._version_list_offline(p)
    ver_nums = [v for v, _ in versions]
    assert "Unreleased" not in ver_nums


def test_version_list_offline_order(tmp_path, install_mod):
    p = tmp_path / "CHANGELOG.md"
    p.write_text(SAMPLE_CHANGELOG, encoding="utf-8")
    versions = install_mod._version_list_offline(p)
    # Should be newest-first as they appear in CHANGELOG
    ver_nums = [v for v, _ in versions]
    assert ver_nums[0] == "0.55.2"


# ── install.py — version --set ────────────────────────────────────────────────

def test_version_set_calls_stop_server(install_mod):
    mock_stop = MagicMock(return_value=True)
    mock_find_port = MagicMock(return_value=9500)
    mock_merge = MagicMock()
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = False
    mock_client.root_key = "mcpServers"
    mock_client.entry_transformer = None
    mock_client.config_path = Path("/tmp/fake.json")

    args = Namespace(set_version="0.54.1", port=0, tool=None, list=False, online=False)

    with patch.object(install_mod, "_load_stop_server", return_value=mock_stop), \
         patch("unity_mcp.config.resolver.find_port", mock_find_port), \
         patch.object(install_mod, "merge_mcp_config", mock_merge), \
         patch.object(install_mod, "CLIENT_REGISTRY", {"claude-code": mock_client}), \
         patch.object(install_mod, "detect_installed", return_value=["claude-code"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(args)

    assert mock_stop.called


def test_version_set_repins_with_tagged_url(install_mod):
    mock_stop = MagicMock(return_value=True)
    mock_merge = MagicMock()
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = False
    mock_client.root_key = "mcpServers"
    mock_client.entry_transformer = None
    mock_client.config_path = Path("/tmp/fake.json")

    args = Namespace(set_version="0.54.1", port=0, tool=None, list=False, online=False)

    with patch.object(install_mod, "_load_stop_server", return_value=mock_stop), \
         patch("unity_mcp.config.resolver.find_port", MagicMock(return_value=9500)), \
         patch.object(install_mod, "merge_mcp_config", mock_merge), \
         patch.object(install_mod, "CLIENT_REGISTRY", {"claude-code": mock_client}), \
         patch.object(install_mod, "detect_installed", return_value=["claude-code"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(args)

    # merge_mcp_config was called; inspect the entry passed
    assert mock_merge.called
    entry_arg = mock_merge.call_args[0][1]  # second positional arg = entry dict
    from_url = entry_arg["args"][1]  # ["--from", URL, "unity-biome-mcp"]
    assert "@v0.54.1" in from_url
    assert "#subdirectory=server" in from_url


def test_version_set_single_tool_only(install_mod):
    mock_stop = MagicMock(return_value=True)
    mock_merge = MagicMock()

    def make_client(name):
        c = MagicMock()
        c.stdout_only = False
        c.is_toml = False
        c.root_key = "mcpServers"
        c.entry_transformer = None
        c.config_path = Path(f"/tmp/{name}.json")
        return c

    registry = {"claude-code": make_client("claude-code"), "cursor": make_client("cursor")}

    args = Namespace(set_version="0.54.1", port=0, tool="claude-code", list=False, online=False)

    with patch.object(install_mod, "_load_stop_server", return_value=mock_stop), \
         patch("unity_mcp.config.resolver.find_port", MagicMock(return_value=9500)), \
         patch.object(install_mod, "merge_mcp_config", mock_merge), \
         patch.object(install_mod, "CLIENT_REGISTRY", registry), \
         patch.object(install_mod, "detect_installed", return_value=["claude-code", "cursor"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(args)

    assert mock_merge.call_count == 1  # only claude-code, not cursor


def test_version_set_rejects_invalid_semver(install_mod, capsys):
    args = Namespace(set_version="bad-version", port=0, tool=None, list=False, online=False)
    with pytest.raises(SystemExit):
        install_mod.cmd_version(args)


def test_force_print_plugin_url_prints_and_exits(install_mod, capsys):
    """--force-print-plugin-url prints UPM URL and returns without repinning."""
    args = Namespace(
        set_version="0.55.0", port=0, tool=None, list=False, online=False,
        force_print_plugin_url=True,
    )
    install_mod.cmd_version(args)  # must NOT call sys.exit or _load_stop_server
    out = capsys.readouterr().out
    assert "german-krasnikov/unity-biome-mcp" in out
    assert "#v0.55.0" in out


def test_force_print_plugin_url_requires_set(install_mod):
    """--force-print-plugin-url without --set still fails (no version)."""
    args = Namespace(
        set_version=None, port=0, tool=None, list=False, online=False,
        force_print_plugin_url=True,
    )
    with pytest.raises(SystemExit):
        install_mod.cmd_version(args)


# ── sync_versions.py — new patchers ──────────────────────────────────────────

SYNC_SCRIPT = Path(__file__).parents[2] / "scripts" / "sync_versions.py"


def run_sync(version: str, root: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SYNC_SCRIPT), version, "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
    )


META_JSON_CONTENT = json.dumps({
    "tools": 99,
    "tests_total": 7274,
    "server_version": "0.50.0",
    "plugin_version": "0.50.0",
    "batch_savings": "80–95%"
}, indent=2, ensure_ascii=False) + "\n"

MCP_SERVER_CS_STUB = textwrap.dedent("""\
    namespace UnityMCP.Editor
    {
        internal static partial class MCPServer
        {
            // synced by sync_versions.py — do not edit manually
            internal static string PluginVersion => "0.40.1";
        }
    }
""")


@pytest.fixture()
def full_project_root(tmp_path: Path) -> Path:
    """Full project tree including _meta.json and MCPServer.cs stub."""
    (tmp_path / "server" / "src" / "unity_mcp").mkdir(parents=True)
    (tmp_path / "unity-plugin" / "Editor").mkdir(parents=True)
    (tmp_path / "docs" / "assets").mkdir(parents=True)

    (tmp_path / "server" / "pyproject.toml").write_text(textwrap.dedent("""\
        [project]
        name = "unity-biome-mcp"
        version = "0.50.0"
    """), encoding="utf-8")

    (tmp_path / "unity-plugin" / "package.json").write_text(
        '{\n  "name": "com.unity-biome-mcp.editor",\n  "version": "0.50.0"\n}\n',
        encoding="utf-8"
    )

    (tmp_path / "server" / "src" / "unity_mcp" / "__version__.py").write_text(
        '__version__ = "0.50.0"\n', encoding="utf-8"
    )

    (tmp_path / "docs" / "assets" / "_meta.json").write_text(
        META_JSON_CONTENT, encoding="utf-8"
    )

    (tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs").write_text(
        MCP_SERVER_CS_STUB, encoding="utf-8"
    )

    return tmp_path


def test_plugin_version_cs_pattern_not_found(tmp_path):
    """Fails fast if MCPServer.cs has no PluginVersion pattern."""
    (tmp_path / "server" / "src" / "unity_mcp").mkdir(parents=True)
    (tmp_path / "unity-plugin" / "Editor").mkdir(parents=True)
    (tmp_path / "docs" / "assets").mkdir(parents=True)

    (tmp_path / "server" / "pyproject.toml").write_text('[project]\nname="x"\nversion="0.1.0"\n', encoding="utf-8")
    (tmp_path / "unity-plugin" / "package.json").write_text('{"name":"x","version":"0.1.0"}', encoding="utf-8")
    (tmp_path / "server" / "src" / "unity_mcp" / "__version__.py").write_text('__version__="0.1.0"\n', encoding="utf-8")
    (tmp_path / "docs" / "assets" / "_meta.json").write_text('{"server_version":"0.1.0","plugin_version":"0.1.0"}', encoding="utf-8")
    # MCPServer.cs WITHOUT the PluginVersion pattern
    (tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs").write_text("// no version here\n", encoding="utf-8")

    result = run_sync("0.2.0", tmp_path)
    assert result.returncode != 0
    assert "PluginVersion" in result.stderr or "pattern" in result.stderr.lower() or "not found" in result.stderr.lower()


def test_all_five_sources_synced(full_project_root):
    """After sync, all 5 version sources agree."""
    result = run_sync("1.0.0", full_project_root)
    assert result.returncode == 0

    pyproject = (full_project_root / "server" / "pyproject.toml").read_text(encoding="utf-8")
    pkg = (full_project_root / "unity-plugin" / "package.json").read_text(encoding="utf-8")
    ver_py = (full_project_root / "server" / "src" / "unity_mcp" / "__version__.py").read_text(encoding="utf-8")
    meta = json.loads((full_project_root / "docs" / "assets" / "_meta.json").read_text(encoding="utf-8"))
    cs = (full_project_root / "unity-plugin" / "Editor" / "MCPServer.cs").read_text(encoding="utf-8")

    assert 'version = "1.0.0"' in pyproject
    assert '"version": "1.0.0"' in pkg
    assert '__version__ = "1.0.0"' in ver_py
    assert meta["server_version"] == "1.0.0"
    assert meta["plugin_version"] == "1.0.0"
    assert 'PluginVersion => "1.0.0"' in cs
