"""Tests for P1-C (validator root_key), P1-F (doctor AI configs), P2-C (python error msg)."""
import json
import sys
from pathlib import Path
from unittest.mock import patch

import pytest

# install/ lives at repo root (../.. from server/tests/) — add to path so tests can import it
_INSTALL_DIR = Path(__file__).parent.parent.parent
if str(_INSTALL_DIR) not in sys.path:
    sys.path.insert(0, str(_INSTALL_DIR))


# ─── P1-C: validator uses info.root_key ──────────────────────────────────────

def test_validator_uses_root_key(tmp_path, monkeypatch):
    """vscode config with 'servers' key → validator finds unity-biome-mcp entry."""
    from unity_mcp.config import clients as c, validator

    cfg = tmp_path / "mcp.json"
    cfg.write_text(json.dumps({"servers": {"unity-biome-mcp": {"command": "uvx"}}}), encoding="utf-8")

    monkeypatch.setattr(c.CLIENT_REGISTRY["vscode"], "config_path", cfg)
    # patch port probe so test doesn't need Unity running
    monkeypatch.setattr(validator, "_port_reachable", lambda _: False)

    result = validator.validate_config("vscode")
    assert "not configured" not in result
    assert "unity-biome-mcp entry" in result


def test_validator_hardcoded_mcpservers_regression(tmp_path, monkeypatch):
    """claude-code config with 'mcpServers' key still works (default root_key)."""
    from unity_mcp.config import clients as c, validator

    cfg = tmp_path / ".claude.json"
    cfg.write_text(json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "uvx"}}}), encoding="utf-8")

    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)
    monkeypatch.setattr(validator, "_port_reachable", lambda _: False)

    result = validator.validate_config("claude-code")
    assert "not configured" not in result
    assert "unity-biome-mcp entry" in result


def test_validator_opencode_uses_mcp_root_key(tmp_path, monkeypatch):
    """opencode config with 'mcp' key → validator finds unity-biome-mcp entry."""
    from unity_mcp.config import clients as c, validator

    cfg = tmp_path / "opencode.json"
    cfg.write_text(json.dumps({"mcp": {"unity-biome-mcp": {"command": "uvx"}}}), encoding="utf-8")

    monkeypatch.setattr(c.CLIENT_REGISTRY["opencode"], "config_path", cfg)
    monkeypatch.setattr(validator, "_port_reachable", lambda _: False)

    result = validator.validate_config("opencode")
    assert "not configured" not in result


# ─── validator BOM tolerance (encoding.md): Windows Notepad/PowerShell 5.1 ──
# Out-File/Set-Content default to a UTF-8-with-BOM encoding. merger.py's
# writers already read with utf-8-sig (_READ_ENCODING) for exactly this
# reason; validator.py's read used plain utf-8 and choked on the leading
# BOM, misreporting a perfectly valid config as "invalid JSON".

def test_validator_reads_bom_prefixed_config(tmp_path, monkeypatch):
    """A UTF-8-BOM-prefixed config must still validate as configured."""
    from unity_mcp.config import clients as c, validator

    cfg = tmp_path / ".claude.json"
    payload = json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "uvx"}}}).encode("utf-8")
    cfg.write_bytes(b"\xef\xbb\xbf" + payload)

    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)
    monkeypatch.setattr(validator, "_port_reachable", lambda _: False)

    result = validator.validate_config("claude-code")
    assert "invalid JSON" not in result
    assert "unity-biome-mcp entry" in result


# ─── P2-C fallback: build_server_entry without uvx ───────────────────────────

def test_build_server_entry_without_uvx():
    """When uvx is absent, fallback to sys.executable -m unity_mcp.server."""
    from unity_mcp.config import resolver

    with patch.object(resolver, "_which", return_value=None), \
            patch.object(resolver.pathlib.Path, "exists", return_value=False):
        entry = resolver.build_server_entry(port=9500)

    assert entry["command"] == sys.executable
    assert entry["args"] == ["-m", "unity_mcp.server"]
    assert entry["env"] == {"UNITY_MCP_PORT": "9500"}


def test_build_server_entry_with_uvx():
    """When uvx is present and no venv, use uvx --from git+URL unity-biome-mcp."""
    from unity_mcp.config import resolver

    with patch.object(resolver, "find_python", return_value="uvx"):
        entry = resolver.build_server_entry(port=9500)

    assert entry["command"] == "uvx"
    assert "--from" in entry["args"]
    assert "unity-biome-mcp" in entry["args"]
    assert any("github.com" in a for a in entry["args"])


# ─── P1-F: doctor checks AI configs ──────────────────────────────────────────

def test_doctor_checks_ai_configs(tmp_path, monkeypatch, capsys):
    """doctor reports AI config present when file contains 'unity-biome-mcp'."""
    import install.commands as cmd
    from unity_mcp.config import clients as c

    # Create a fake claude-code config with unity-biome-mcp AND git URL present
    from unity_mcp.config.resolver import GIT_INSTALL_URL
    cfg = tmp_path / ".claude.json"
    cfg.write_text(
        '{"mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": ["--from", "' + GIT_INSTALL_URL + '", "unity-biome-mcp"]}}}',
        encoding="utf-8"
    )
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)

    # Patch away clients that don't exist in tmp
    missing = tmp_path / "nonexistent" / "file.json"
    for key in list(c.CLIENT_REGISTRY):
        if key != "claude-code":
            monkeypatch.setattr(c.CLIENT_REGISTRY[key], "config_path", missing)

    captured_ok = []
    captured_fail = []

    class FakeUI:
        def box(self, lines): pass
        def ok(self, msg): captured_ok.append(msg)
        def fail(self, msg): captured_fail.append(msg)
        def info(self, msg): pass
        def error(self, msg): pass

    server_dir = tmp_path / "server"
    server_dir.mkdir()
    venv_bin = server_dir / ".venv" / "bin"
    venv_bin.mkdir(parents=True)
    venv_py = venv_bin / "python"
    venv_py.touch()

    codex_config = tmp_path / "config.toml"
    mcp_json = tmp_path / ".mcp.json"

    # Patch subprocess so importable check doesn't actually run python
    with patch("install.commands.subprocess.run") as mock_run:
        mock_run.return_value.returncode = 0
        # Patch socket so TCP check doesn't fail loudly
        with patch("install.commands.socket.create_connection", side_effect=OSError):
            cmd.cmd_doctor(server_dir, codex_config, mcp_json, FakeUI(), None)

    # claude-code config should appear in ok messages
    assert any("Claude Code" in m and "config" in m.lower() for m in captured_ok), \
        f"Expected Claude Code config OK, got: {captured_ok}"


# ─── Drift guard: cmd_doctor's .mcp.json key check must read SERVER_NAME ────

def test_cmd_doctor_detects_configured_mcp_json(tmp_path, monkeypatch):
    """cmd_doctor's '.mcp.json configured' check must be driven by SERVER_NAME,
    not an independently-hardcoded 'unity-mcp' literal that merely happens to
    agree with it today. Monkeypatch the shared merger.SERVER_NAME constant to a
    throwaway probe value: a genuinely-wired doctor (lazy-importing SERVER_NAME
    from merger on every call) recognizes an entry keyed under the probe value;
    a hardcoded-literal doctor would report it as not configured instead."""
    import install.commands as cmd
    from unity_mcp.config import clients as c
    from unity_mcp.config import merger

    monkeypatch.setattr(merger, "SERVER_NAME", "probe-doctor")
    monkeypatch.setattr(cmd, "discover_port", lambda: 0)

    # Isolate from real user config files — only .mcp.json under test matters here.
    missing = tmp_path / "nonexistent" / "file.json"
    for key in list(c.CLIENT_REGISTRY):
        monkeypatch.setattr(c.CLIENT_REGISTRY[key], "config_path", missing)

    mcp_json = tmp_path / ".mcp.json"
    mcp_json.write_text(json.dumps({"mcpServers": {"probe-doctor": {"command": "uvx"}}}),
                         encoding="utf-8")

    server_dir = tmp_path / "server"          # deliberately absent — importable check no-ops
    codex_config = tmp_path / "config.toml"   # deliberately absent — toml check no-ops

    captured_ok = []

    class FakeUI:
        def box(self, lines): pass
        def ok(self, msg): captured_ok.append(msg)
        def fail(self, msg): pass
        def info(self, msg): pass
        def error(self, msg): pass

    with patch("install.commands.socket.create_connection", side_effect=OSError):
        cmd.cmd_doctor(server_dir, codex_config, mcp_json, FakeUI(), None)

    assert any(".mcp.json configured" in m for m in captured_ok), \
        f"Expected .mcp.json configured OK, got: {captured_ok}"


# ─── P2-C: Python version error message ──────────────────────────────────────

def test_python_version_error_has_url(monkeypatch):
    """setup_env exits with helpful message including python.org URL."""
    import install.commands as cmd

    # Force check_python() to return False
    monkeypatch.setattr(cmd, "check_python", lambda: False)

    with pytest.raises(SystemExit) as exc_info:
        cmd.setup_env(Path("/tmp"), Path("/tmp"), Path("/tmp"), None)

    msg = str(exc_info.value)
    assert "python.org" in msg


def test_python_version_error_has_windows_tip(monkeypatch):
    """setup_env error message includes Windows PATH tip."""
    import install.commands as cmd

    monkeypatch.setattr(cmd, "check_python", lambda: False)

    with pytest.raises(SystemExit) as exc_info:
        cmd.setup_env(Path("/tmp"), Path("/tmp"), Path("/tmp"), None)

    msg = str(exc_info.value)
    assert "PATH" in msg or "Windows" in msg


# ─── B2: TOML command uses posix path (no backslash) ─────────────────────────

def test_toml_command_uses_posix_path(tmp_path):
    """write_codex_config must use as_posix() so Windows paths don't inject TOML unicode escapes."""
    import install.commands as cmd

    server_dir = tmp_path / "server"
    venv_bin = server_dir / ".venv" / "Scripts"
    venv_bin.mkdir(parents=True)
    py = venv_bin / "python.exe"
    py.touch()

    codex_dir = tmp_path / ".codex"
    codex_config = tmp_path / ".codex" / "config.toml"

    class FakeUI:
        def ok(self, msg): pass

    cmd.write_codex_config(server_dir, codex_dir, codex_config, FakeUI())

    content = codex_config.read_text(encoding="utf-8")
    assert "\\" not in content, f"Backslash found in TOML (unicode escape risk): {content!r}"


# ─── B3: file: URI uses posix path (forward slashes) ─────────────────────────

def test_file_uri_uses_posix_path(tmp_path, monkeypatch):
    """cmd_connect must use as_posix() so Windows paths have forward slashes in manifest."""
    import install.commands as cmd

    project_dir = tmp_path / "MyProject"
    (project_dir / "Packages").mkdir(parents=True)
    manifest = project_dir / "Packages" / "manifest.json"
    manifest.write_text('{"dependencies": {}}', encoding="utf-8")

    # Simulate a Windows-style path by monkeypatching _REPO_ROOT
    fake_root = tmp_path / "C:" / "Work" / "unity-biome-mcp"
    fake_root.mkdir(parents=True)
    monkeypatch.setattr(cmd, "_REPO_ROOT", fake_root)

    class FakeUI:
        def ok(self, msg): pass
        def error(self, msg): pass

    import argparse
    args = argparse.Namespace(unity_project=str(project_dir))
    cmd.cmd_connect(args, FakeUI())

    content = manifest.read_text(encoding="utf-8")
    import json
    data = json.loads(content)
    for pkg, ref in data.get("dependencies", {}).items():
        if ref.startswith("file:"):
            assert "\\" not in ref, f"Backslash in file: URI for {pkg}: {ref!r}"


# ─── Cross-language: git URL consistency ──────────────────────────────────────

def test_git_url_consistency():
    """Python GIT_INSTALL_URL and C# GitInstallUrl must be identical."""
    import subprocess
    from unity_mcp.config.resolver import GIT_INSTALL_URL

    repo_root = Path(__file__).parent.parent.parent
    cs_files = [
        repo_root / "unity-plugin" / "Editor" / "Wizard" / "WizardConfigWriter.cs",
    ]

    py_url = GIT_INSTALL_URL
    for cs_file in cs_files:
        content = cs_file.read_text(encoding="utf-8")
        assert py_url in content, (
            f"GIT_INSTALL_URL mismatch in {cs_file.name}.\n"
            f"Python: {py_url}\n"
            f"Not found in C# file."
        )


# ─── Bug fixes: TOML validator + generic cross-platform ──────────────────────

def test_validator_toml_client_skips_json_parse(tmp_path, monkeypatch):
    """Codex (is_toml=True) validator must not raise JSONDecodeError on TOML content."""
    from unity_mcp.config import clients as c, validator

    toml_file = tmp_path / "config.toml"
    toml_file.write_text('[mcp]\n[mcp."unity-biome-mcp"]\ncommand = "uvx"\n', encoding="utf-8")

    monkeypatch.setattr(c.CLIENT_REGISTRY["codex"], "config_path", toml_file)

    result = validator.validate_config("codex")
    assert "configured" in result
    assert "JSONDecodeError" not in result
    assert "invalid JSON" not in result


def test_generic_config_path_cross_platform():
    """generic client config_path must not be the literal '/dev/null' string (Windows compat)."""
    import os
    from unity_mcp.config.clients import CLIENT_REGISTRY

    generic = CLIENT_REGISTRY["generic"]
    # os.devnull resolves to NUL on Windows, /dev/null on POSIX — never literal '/dev/null'
    assert str(generic.config_path) == os.devnull


# ─── B1: update check imports correct version ─────────────────────────────────

def test_update_check_imports_correct_version():
    """_update_check must import __version__ from package, not stale __version__.py."""
    from unity_mcp import _update_check, __version__

    assert _update_check.__version__ == __version__
    assert _update_check.__version__ != "0.45.0", "importing stale __version__.py — fix import"


def test_update_check_uses_github_not_pypi():
    """_update_check must fetch from GitHub releases, not PyPI."""
    from unity_mcp import _update_check

    assert "github" in _update_check.GITHUB_URL
    assert "pypi" not in _update_check.GITHUB_URL


# ─── B3: doctor detects stale PyPI config ─────────────────────────────────────

def test_doctor_detects_stale_pypi_config(tmp_path, monkeypatch, capsys):
    """doctor must warn when config has unity-biome-mcp but no git+URL (stale PyPI)."""
    import install.commands as cmd
    from unity_mcp.config import clients as c
    from unity_mcp.config.resolver import GIT_INSTALL_URL

    # Config has unity-biome-mcp but NOT the git URL — simulates old PyPI install
    cfg = tmp_path / ".claude.json"
    cfg.write_text('{"mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": ["unity-biome-mcp"]}}}',
                   encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-code"], "config_path", cfg)

    missing = tmp_path / "nonexistent" / "file.json"
    for key in list(c.CLIENT_REGISTRY):
        if key != "claude-code":
            monkeypatch.setattr(c.CLIENT_REGISTRY[key], "config_path", missing)

    captured_fail = []

    class FakeUI:
        def box(self, lines): pass
        def ok(self, msg): pass
        def fail(self, msg): captured_fail.append(msg)
        def info(self, msg): pass
        def error(self, msg): pass

    server_dir = tmp_path / "server"
    server_dir.mkdir()
    venv_bin = server_dir / ".venv" / "bin"
    venv_bin.mkdir(parents=True)
    (venv_bin / "python").touch()

    codex_config = tmp_path / "config.toml"
    mcp_json = tmp_path / ".mcp.json"

    from unittest.mock import patch
    with patch("install.commands.subprocess.run") as mock_run:
        mock_run.return_value.returncode = 0
        with patch("install.commands.socket.create_connection", side_effect=OSError):
            cmd.cmd_doctor(server_dir, codex_config, mcp_json, FakeUI(), None)

    assert any("stale" in m.lower() or "pypi" in m.lower() for m in captured_fail), \
        f"Expected stale PyPI warning in fail messages, got: {captured_fail}"
