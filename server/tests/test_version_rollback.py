"""TDD: Task 6 — version rollback (server_git_url, version --list/--set, sync_versions patchers).

All tests are unit-level (not live, no network, no Unity required).
"""
import json
import os
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


def test_version_set_only_writes_detected_clients(install_mod):
    """`version --set` without --tool must target only clients
    detect_installed() actually found -- never every registered client -- or
    a single-tool user gets brand-new pinned configs written for AI tools
    they never installed. Double-red: reverting _target_clients to
    `list(CLIENT_REGISTRY.keys())` makes mock_merge fire for cursor too,
    failing call_count == 1."""
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

    args = Namespace(set_version="0.54.1", port=0, tool=None, list=False, online=False)

    with patch.object(install_mod, "_load_stop_server", return_value=mock_stop), \
         patch("unity_mcp.config.resolver.find_port", MagicMock(return_value=9500)), \
         patch.object(install_mod, "merge_mcp_config", mock_merge), \
         patch.object(install_mod, "CLIENT_REGISTRY", registry), \
         patch.object(install_mod, "detect_installed", return_value=["claude-code"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(args)

    assert mock_merge.call_count == 1  # only the detected client, not cursor
    assert mock_merge.call_args[0][0] == registry["claude-code"].config_path


# ── install.py — version --set / --unpin pin support (ARC-0b T3) ────────────

def test_version_set_adds_pin_to_entry(install_mod):
    """ARC-0b: `version --set` must write "_pin": true into the entry so a
    later `install.py update` skips it (_reconfigure_detected_clients)."""
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

    entry_arg = mock_merge.call_args[0][1]
    assert entry_arg["_pin"] is True


def test_version_set_pins_toml_client(install_mod):
    """Codex (TOML) client: pin_toml_entry must be called with the set version,
    after the regular merge_toml_mcp write."""
    mock_stop = MagicMock(return_value=True)
    mock_merge_toml = MagicMock()
    mock_pin_toml = MagicMock()
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = True
    mock_client.root_key = "mcpServers"
    mock_client.entry_transformer = None
    mock_client.config_path = Path("/tmp/config.toml")

    args = Namespace(set_version="0.54.1", port=0, tool="codex", list=False, online=False)

    with patch.object(install_mod, "_load_stop_server", return_value=mock_stop), \
         patch("unity_mcp.config.resolver.find_port", MagicMock(return_value=9500)), \
         patch.object(install_mod, "merge_toml_mcp", mock_merge_toml), \
         patch.object(install_mod, "pin_toml_entry", mock_pin_toml), \
         patch.object(install_mod, "CLIENT_REGISTRY", {"codex": mock_client}), \
         patch.object(install_mod, "detect_installed", return_value=["codex"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(args)

    assert mock_merge_toml.called
    assert mock_pin_toml.call_args[0] == (mock_client.config_path, "0.54.1")


def test_version_set_vscode_pin_survives_reconfigure(tmp_path, install_mod):
    """E2E, DEV-58/B2-P7: a client with a whole-replace entry_transformer (vscode's
    allowlisted format) must not lose "_pin" through the real merge_mcp_config path.
    Before the fix, _vscode_transform dropped "_pin", so a later `install.py update`
    (_reconfigure_detected_clients) would see is_entry_pinned()==False and silently
    overwrite the pinned version — a false "repinned" success."""
    from unity_mcp.config.clients import _vscode_transform

    cfg = tmp_path / "mcp.json"
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = False
    mock_client.root_key = "servers"
    mock_client.entry_transformer = _vscode_transform
    mock_client.config_path = cfg
    mock_client.name = "VS Code"

    set_args = Namespace(set_version="0.54.1", port=0, tool="vscode", list=False, online=False)

    # Phase 1: version --set writes a pinned entry through the REAL merge_mcp_config.
    with patch.object(install_mod, "_load_stop_server", return_value=MagicMock(return_value=True)), \
         patch("unity_mcp.config.resolver.find_port", MagicMock(return_value=9500)), \
         patch.object(install_mod, "CLIENT_REGISTRY", {"vscode": mock_client}), \
         patch.object(install_mod, "detect_installed", return_value=["vscode"]), \
         patch.object(install_mod, "backup", MagicMock()):
        install_mod.cmd_version(set_args)

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["servers"]["unity-biome-mcp"]["_pin"] is True
    assert install_mod.is_entry_pinned(cfg, root_key="servers") is True
    pinned_text = cfg.read_text(encoding="utf-8")

    # Phase 2: install.py update (_reconfigure_detected_clients) must skip it —
    # the file must stay byte-for-byte identical to what --set just wrote.
    with patch.object(install_mod, "CLIENT_REGISTRY", {"vscode": mock_client}), \
         patch.object(install_mod, "detect_installed", return_value=["vscode"]), \
         patch.object(install_mod, "validate_config", return_value="Status: ok"), \
         patch.object(install_mod, "build_server_entry", return_value={"command": "unpinned-uvx", "args": []}):
        install_mod._reconfigure_detected_clients()

    assert cfg.read_text(encoding="utf-8") == pinned_text


def test_unpin_only_targets_detected_clients(install_mod):
    """`version --unpin` without --tool must target only detect_installed()
    clients -- never every registered client. Double-red: reverting
    _target_clients to `list(CLIENT_REGISTRY.keys())` makes unpin_entry fire
    for cursor too, failing call_count == 1."""
    mock_unpin_entry = MagicMock(return_value=True)

    def make_client(name):
        c = MagicMock()
        c.stdout_only = False
        c.is_toml = False
        c.root_key = "mcpServers"
        c.config_path = Path(f"/tmp/{name}.json")
        return c

    registry = {"claude-code": make_client("claude-code"), "cursor": make_client("cursor")}
    args = Namespace(set_version=None, port=0, tool=None, list=False, online=False, unpin=True)

    with patch.object(install_mod, "CLIENT_REGISTRY", registry), \
         patch.object(install_mod, "detect_installed", return_value=["claude-code"]), \
         patch.object(install_mod, "unpin_entry", mock_unpin_entry):
        install_mod.cmd_version(args)

    assert mock_unpin_entry.call_count == 1  # only the detected client, not cursor
    assert mock_unpin_entry.call_args[0][0] == registry["claude-code"].config_path


def test_version_unpin_removes_pin_from_json_config(tmp_path, install_mod):
    """`version --unpin` must remove "_pin" from the real config file."""
    cfg = tmp_path / "claude.json"
    cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "old", "args": [], "_pin": True}}
    }), encoding="utf-8")
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = False
    mock_client.root_key = "mcpServers"
    mock_client.config_path = cfg

    args = Namespace(set_version=None, port=0, tool="claude-code", list=False, online=False, unpin=True)

    with patch.object(install_mod, "CLIENT_REGISTRY", {"claude-code": mock_client}):
        install_mod.cmd_version(args)

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "_pin" not in data["mcpServers"]["unity-biome-mcp"]
    assert data["mcpServers"]["unity-biome-mcp"]["command"] == "old"


def test_version_unpin_no_pin_present_does_not_report_unpinned(tmp_path, install_mod, capsys):
    """`version --unpin` on a config with no "_pin" key must not claim success --
    unpin_entry/unpin_toml_entry both return False on a no-op (item 6)."""
    cfg = tmp_path / "claude.json"
    cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "old", "args": []}}
    }), encoding="utf-8")
    mock_client = MagicMock()
    mock_client.stdout_only = False
    mock_client.is_toml = False
    mock_client.root_key = "mcpServers"
    mock_client.config_path = cfg

    args = Namespace(set_version=None, port=0, tool="claude-code", list=False, online=False, unpin=True)

    with patch.object(install_mod, "CLIENT_REGISTRY", {"claude-code": mock_client}):
        install_mod.cmd_version(args)

    out = capsys.readouterr().out
    assert "unpinned" not in out


def test_version_unpin_does_not_require_set_version(install_mod):
    """--unpin alone (no --set) must not hit the 'Specify --list or --set' failure."""
    args = Namespace(set_version=None, port=0, tool=None, list=False, online=False, unpin=True)
    with patch.object(install_mod, "CLIENT_REGISTRY", {}):
        install_mod.cmd_version(args)  # must not raise SystemExit


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
    # PYTHONIOENCODING=utf-8: sync_versions.py prints → (U+2192) which fails
    # on Windows pipes using the default cp1252 encoding.
    return subprocess.run(
        [sys.executable, str(SYNC_SCRIPT), version, "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
        env={**os.environ, "PYTHONIOENCODING": "utf-8"}, timeout=60,
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

BIOME_VERSION_CS_STUB = textwrap.dedent("""\
    namespace UnityMCP.Editor
    {
        internal static class BiomeVersion
        {
            public const string Plugin = "0.50.0";
            public const int Protocol = 3;
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
    (tmp_path / "server" / "uv.lock").write_text(textwrap.dedent("""\
        [[package]]
        name = "unity-biome-mcp"
        version = "0.50.0"
        source = { editable = "." }
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

    (tmp_path / "unity-plugin" / "Editor" / "BiomeVersion.cs").write_text(
        BIOME_VERSION_CS_STUB, encoding="utf-8"
    )

    (tmp_path / "scripts" / "gauntlet").mkdir(parents=True)
    (tmp_path / "scripts" / "gauntlet" / "release-policy.json").write_text(
        '{\n  "activation_product_version": "0.50.0"\n}\n', encoding="utf-8"
    )

    return tmp_path


def test_plugin_version_cs_pattern_not_found(tmp_path):
    """Fails fast if MCPServer.cs has no PluginVersion pattern."""
    (tmp_path / "server" / "src" / "unity_mcp").mkdir(parents=True)
    (tmp_path / "unity-plugin" / "Editor").mkdir(parents=True)
    (tmp_path / "docs" / "assets").mkdir(parents=True)

    (tmp_path / "server" / "pyproject.toml").write_text('[project]\nname="x"\nversion="0.1.0"\n', encoding="utf-8")
    (tmp_path / "server" / "uv.lock").write_text(
        '[[package]]\nname = "unity-biome-mcp"\nversion = "0.1.0"\n',
        encoding="utf-8",
    )
    (tmp_path / "unity-plugin" / "package.json").write_text('{"name":"x","version":"0.1.0"}', encoding="utf-8")
    (tmp_path / "server" / "src" / "unity_mcp" / "__version__.py").write_text('__version__="0.1.0"\n', encoding="utf-8")
    (tmp_path / "docs" / "assets" / "_meta.json").write_text('{"server_version":"0.1.0","plugin_version":"0.1.0"}', encoding="utf-8")
    # MCPServer.cs WITHOUT the PluginVersion pattern
    (tmp_path / "unity-plugin" / "Editor" / "MCPServer.cs").write_text("// no version here\n", encoding="utf-8")
    (tmp_path / "scripts" / "gauntlet").mkdir(parents=True)
    (tmp_path / "scripts" / "gauntlet" / "release-policy.json").write_text(
        '{"activation_product_version":"0.1.0"}', encoding="utf-8"
    )

    result = run_sync("0.2.0", tmp_path)
    assert result.returncode != 0
    assert "PluginVersion" in result.stderr or "pattern" in result.stderr.lower() or "not found" in result.stderr.lower()


def test_all_six_sources_synced(full_project_root):
    """After sync, all 6 version sources agree (BiomeVersion.cs joined the set in DEV-61)."""
    result = run_sync("1.0.0", full_project_root)
    assert result.returncode == 0

    pyproject = (full_project_root / "server" / "pyproject.toml").read_text(encoding="utf-8")
    pkg = (full_project_root / "unity-plugin" / "package.json").read_text(encoding="utf-8")
    ver_py = (full_project_root / "server" / "src" / "unity_mcp" / "__version__.py").read_text(encoding="utf-8")
    meta = json.loads((full_project_root / "docs" / "assets" / "_meta.json").read_text(encoding="utf-8"))
    cs = (full_project_root / "unity-plugin" / "Editor" / "MCPServer.cs").read_text(encoding="utf-8")
    biome = (full_project_root / "unity-plugin" / "Editor" / "BiomeVersion.cs").read_text(encoding="utf-8")

    assert 'version = "1.0.0"' in pyproject
    assert '"version": "1.0.0"' in pkg
    assert '__version__ = "1.0.0"' in ver_py
    assert meta["server_version"] == "1.0.0"
    assert meta["plugin_version"] == "1.0.0"
    assert 'PluginVersion => "1.0.0"' in cs
    assert 'Plugin = "1.0.0"' in biome
    assert "public const int Protocol = 3;" in biome
