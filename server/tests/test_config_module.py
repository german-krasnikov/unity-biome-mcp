"""Tests for config auto-generation module (clients, merger, backup, validator, resolver)."""
import json
import pathlib
import sys
from unittest.mock import patch

import pytest


# ─── clients.py ─────────────────────────────────────────────────────────────

def test_detect_finds_claude_desktop_when_dir_exists(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c
    cfg = tmp_path / "claude_desktop_config.json"
    cfg.touch()
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    found = c.detect_installed()
    assert "claude-desktop" in found


def test_detect_returns_empty_when_nothing_installed(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c
    # Point to a nested path whose parent also doesn't exist
    missing = tmp_path / "no_such_dir" / "nonexistent.json"
    for info in c.CLIENT_REGISTRY.values():
        monkeypatch.setattr(info, "config_path", missing)
    result = c.detect_installed()
    assert result == []


def test_client_paths_are_platform_specific():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["claude-desktop"]
    if sys.platform == "darwin":
        assert "Library" in str(info.config_path)
    elif sys.platform == "win32":
        assert "AppData" in str(info.config_path) or "APPDATA" in str(info.config_path)
    else:
        assert ".config" in str(info.config_path)


def test_all_expected_clients_registered():
    from unity_mcp.config import clients as c
    for key in ("claude-desktop", "claude-code", "cursor", "windsurf", "kimi", "vscode", "opencode", "junie"):
        assert key in c.CLIENT_REGISTRY


def test_kimi_in_client_registry():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["kimi"]
    assert "kimi-code" in str(info.config_path).lower()
    assert info.root_key == "mcpServers"


def test_junie_in_client_registry():
    """Junie is referenced by ProjectConfigTargets.cs (.junie/mcp/mcp.json) but was
    missing from the Python registry — install.py configure --tool junie failed silently."""
    from unity_mcp.config import clients as c
    assert "junie" in c.CLIENT_REGISTRY
    info = c.CLIENT_REGISTRY["junie"]
    assert str(info.config_path).replace("\\", "/").endswith(".junie/mcp/mcp.json")
    assert info.root_key == "mcpServers"
    assert info.is_toml is False


def test_vscode_in_client_registry():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["vscode"]
    assert "Code" in str(info.config_path) or "code" in str(info.config_path).lower()
    assert info.root_key == "servers"


def test_opencode_in_client_registry():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["opencode"]
    assert "opencode" in str(info.config_path).lower()
    assert info.root_key == "mcp"


def test_vscode_config_path_platform_specific():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["vscode"]
    if sys.platform == "darwin":
        assert "Library" in str(info.config_path)
    elif sys.platform == "win32":
        assert "Code" in str(info.config_path)
    else:
        assert ".config" in str(info.config_path)


def test_opencode_config_path_platform_specific():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["opencode"]
    if sys.platform == "win32":
        assert "opencode" in str(info.config_path).lower()
    else:
        assert ".config" in str(info.config_path)


# ─── merger.py ──────────────────────────────────────────────────────────────

def test_merge_creates_new_config_file(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    entry = {"command": "uvx", "args": ["unity-biome-mcp"]}
    merger.merge_mcp_config(cfg, entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"] == entry


def test_merge_preserves_other_servers(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    existing = {"mcpServers": {"filesystem": {"command": "fs", "args": []}}}
    cfg.write_text(json.dumps(existing), encoding="utf-8")
    entry = {"command": "uvx", "args": ["unity-biome-mcp"]}
    merger.merge_mcp_config(cfg, entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "filesystem" in data["mcpServers"]
    assert data["mcpServers"]["unity-biome-mcp"] == entry


def test_merge_updates_existing_unity_mcp(tmp_path):
    """A prior-install entry under the OLD name 'unity-mcp' must be migrated to
    'unity-biome-mcp' on merge — no leftover duplicate under the old key."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    old = {"mcpServers": {"unity-mcp": {"command": "python", "args": ["-m", "old"]}}}
    cfg.write_text(json.dumps(old), encoding="utf-8")
    new_entry = {"command": "uvx", "args": ["unity-biome-mcp"]}
    merger.merge_mcp_config(cfg, new_entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"] == new_entry
    assert "unity-mcp" not in data["mcpServers"]  # old name migrated away
    assert len(data["mcpServers"]) == 1  # not duplicated


def test_merger_custom_root_key(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "mcp.json"
    entry = {"type": "stdio", "command": "uvx", "args": ["unity-biome-mcp"]}
    merger.merge_mcp_config(cfg, entry, root_key="servers")
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "servers" in data
    assert data["servers"]["unity-biome-mcp"] == entry
    assert "mcpServers" not in data


def test_merger_entry_transformer(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    base_entry = {"command": "uvx", "args": ["unity-biome-mcp"]}

    def transform(e: dict) -> dict:
        return {"type": "local", "command": [e["command"]] + e["args"], "enabled": True}

    merger.merge_mcp_config(cfg, base_entry, entry_transformer=transform)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    stored = data["mcpServers"]["unity-biome-mcp"]
    assert stored["type"] == "local"
    assert stored["command"] == ["uvx", "unity-biome-mcp"]
    assert stored["enabled"] is True


def test_merger_default_root_key_unchanged(tmp_path):
    """Backward compat: no extra params = old behavior."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    existing = {"mcpServers": {"other": {}}}
    cfg.write_text(json.dumps(existing), encoding="utf-8")
    merger.merge_mcp_config(cfg, {"command": "uvx", "args": ["unity-biome-mcp"]})
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "mcpServers" in data
    assert "other" in data["mcpServers"]


def test_merge_toml_strips_stale_unity_entry(tmp_path):
    """Stale [mcp_servers.unity] (bare) must be removed when writing unity-biome-mcp."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text('[mcp_servers.unity]\ncommand = "/old/python"\nargs = []\n', encoding="utf-8")
    merger.merge_toml_mcp(cfg, {"command": "/new/python", "args": ["-m", "unity_mcp.server"]})
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity]\n" not in text
    assert "[mcp_servers.unity-biome-mcp]" in text
    assert "/new/python" in text


def test_merge_toml_preserves_other_tables(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text('model = "gpt-4"\n\n[projects.foo]\ntrust_level = "trusted"\n\n[mcp_servers.unity]\ncommand = "/old"\nargs = []\n', encoding="utf-8")
    merger.merge_toml_mcp(cfg, {"command": "/new", "args": []})
    text = cfg.read_text(encoding="utf-8")
    assert 'model = "gpt-4"' in text
    assert "[projects.foo]" in text
    assert "[mcp_servers.unity]\n" not in text


def test_merge_toml_includes_env(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": ["unity-biome-mcp"], "env": {"PYTHONUTF8": "1"}})
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity-biome-mcp.env]" in text
    assert "PYTHONUTF8 = '1'" in text


def test_merge_toml_windows_path_no_regex_escape(tmp_path):
    """Windows paths with backslashes must not cause re.error on sub()."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text('[mcp_servers.unity-mcp]\ncommand = "/old"\nargs = []\n', encoding="utf-8")
    merger.merge_toml_mcp(cfg, {
        "command": r"C:\Users\TestUser\Python\python.exe",
        "args": ["-m", "unity_mcp.server"],
    })
    text = cfg.read_text(encoding="utf-8")
    assert r"C:\Users\TestUser" in text
    assert text.count("[mcp_servers.unity-biome-mcp]") == 1


def test_merge_toml_creates_backup(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text("original content", encoding="utf-8")
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": []})
    bak = tmp_path / "config.bak"
    assert bak.exists()
    assert bak.read_text(encoding="utf-8") == "original content"


def test_merge_toml_idempotent(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    entry = {"command": "/py", "args": ["-m", "unity_mcp.server"]}
    merger.merge_toml_mcp(cfg, entry)
    merger.merge_toml_mcp(cfg, entry)
    text = cfg.read_text(encoding="utf-8")
    assert text.count("[mcp_servers.unity-biome-mcp]") == 1


def test_merge_toml_does_not_strip_unity_mcp_entry(tmp_path):
    """Legacy [mcp_servers.unity-mcp] is migrated to unity-biome-mcp;
    the foreign bare [mcp_servers.unity] (CoplayDev) is still stripped."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        '[mcp_servers.unity-mcp]\ncommand = "/existing"\nargs = []\n'
        '\n[mcp_servers.unity]\ncommand = "/foreign"\nargs = []\n',
        encoding="utf-8",
    )
    merger.merge_toml_mcp(cfg, {"command": "/new", "args": []})
    text = cfg.read_text(encoding="utf-8")
    assert text.count("[mcp_servers.unity-biome-mcp]") == 1
    assert "[mcp_servers.unity-mcp]" not in text
    assert "[mcp_servers.unity]\n" not in text


def test_merge_toml_strips_stale_unity_env_subsection(tmp_path):
    """CRIT-1: stale [mcp_servers.unity.env] sub-section must be stripped too."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        '[mcp_servers.unity]\ncommand = "/old"\nargs = []\n'
        '\n[mcp_servers.unity.env]\nFOO = "bar"\n',
        encoding="utf-8",
    )
    merger.merge_toml_mcp(cfg, {"command": "/new", "args": []})
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity]" not in text
    assert "[mcp_servers.unity.env]" not in text
    assert 'FOO = "bar"' not in text


def test_merge_toml_strips_stale_no_trailing_newline(tmp_path):
    """CRIT-2: stale_re works even when file has no trailing newline."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_bytes(b'[mcp_servers.unity]\ncommand = "/old"\nargs = []')  # no trailing \n
    merger.merge_toml_mcp(cfg, {"command": "/new", "args": []})
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity]\n" not in text
    assert "[mcp_servers.unity-biome-mcp]" in text


def test_merge_toml_backup_not_overwritten_on_second_call(tmp_path):
    """MAJOR-2: first-write-wins — backup keeps original content across calls."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text("original", encoding="utf-8")
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": []})
    merger.merge_toml_mcp(cfg, {"command": "uvx2", "args": []})
    assert (tmp_path / "config.bak").read_text(encoding="utf-8") == "original"


def test_merger_handles_crlf_toml(tmp_path):
    """CRLF line endings (Windows) must not cause duplicate sections.

    Also covers migration: an old-name section survives CRLF parsing and is
    replaced by the new unity-biome-mcp name (not left behind as a duplicate)."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    # Write TOML with Windows CRLF line endings
    cfg.write_bytes(
        b'[mcp_servers.unity-mcp]\r\ncommand = \'/old\'\r\nargs = []\r\n'
    )
    merger.merge_toml_mcp(cfg, {"command": "/new", "args": ["-m", "unity_mcp.server"]})
    text = cfg.read_text(encoding="utf-8")
    assert text.count("[mcp_servers.unity-biome-mcp]") == 1, "CRLF caused duplicate section"
    assert "[mcp_servers.unity-mcp]" not in text  # old name migrated away
    assert "/new" in text
    assert "/old" not in text


# ─── merger.py: remove_mcp_entry / remove_toml_mcp_entry ────────────────────

def test_remove_mcp_entry_deletes_existing_key(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "x"}, "other": {"command": "y"}}}), encoding="utf-8")

    result = merger.remove_mcp_entry(cfg)

    assert result is True
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "unity-biome-mcp" not in data["mcpServers"]
    assert "other" in data["mcpServers"]


def test_remove_mcp_entry_missing_file_returns_false(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"

    result = merger.remove_mcp_entry(cfg)

    assert result is False
    assert not cfg.exists()


def test_remove_mcp_entry_missing_key_returns_false(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    original = json.dumps({"mcpServers": {"other": {"command": "y"}}})
    cfg.write_text(original, encoding="utf-8")

    result = merger.remove_mcp_entry(cfg)

    assert result is False
    assert cfg.read_text(encoding="utf-8") == original


def test_remove_mcp_entry_corrupt_json_raises_valueerror(tmp_path):
    from unity_mcp.config import merger
    bad = tmp_path / "config.json"
    bad.write_text("{ this is not json }", encoding="utf-8")
    with pytest.raises(ValueError, match="Corrupt JSON"):
        merger.remove_mcp_entry(bad)


def test_remove_toml_mcp_entry_strips_section(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text("[mcp_servers.unity-mcp]\ncommand = 'x'\nargs = []\n", encoding="utf-8")

    result = merger.remove_toml_mcp_entry(cfg)

    assert result is True
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity-mcp]" not in text


def test_remove_toml_mcp_entry_strips_env_subsection(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        "[mcp_servers.unity-mcp]\ncommand = 'x'\nargs = []\n"
        "\n[mcp_servers.unity-mcp.env]\nFOO = 'bar'\n",
        encoding="utf-8",
    )

    result = merger.remove_toml_mcp_entry(cfg)

    assert result is True
    text = cfg.read_text(encoding="utf-8")
    assert "[mcp_servers.unity-mcp]" not in text
    assert "[mcp_servers.unity-mcp.env]" not in text
    assert "FOO = 'bar'" not in text


def test_remove_toml_mcp_entry_missing_section_returns_false(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text('model = "gpt-4"\n', encoding="utf-8")

    result = merger.remove_toml_mcp_entry(cfg)

    assert result is False


def test_remove_toml_mcp_entry_preserves_other_tables(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        'model = "gpt-4"\n\n[projects.foo]\ntrust_level = "trusted"\n\n'
        "[mcp_servers.unity-mcp]\ncommand = '/old'\nargs = []\n",
        encoding="utf-8",
    )

    result = merger.remove_toml_mcp_entry(cfg)

    assert result is True
    text = cfg.read_text(encoding="utf-8")
    assert 'model = "gpt-4"' in text
    assert "[projects.foo]" in text
    assert "[mcp_servers.unity-mcp]" not in text


def test_remove_toml_mcp_entry_missing_file_returns_false(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"

    result = merger.remove_toml_mcp_entry(cfg)

    assert result is False


# ─── backup.py ──────────────────────────────────────────────────────────────

def test_backup_creates_timestamped_copy(tmp_path):
    from unity_mcp.config import backup
    orig = tmp_path / "config.json"
    orig.write_text('{"key": "value"}', encoding="utf-8")
    bak = backup.backup(orig)
    assert bak is not None
    assert bak.exists()
    assert bak.suffix == ".bak"
    assert orig.exists()  # original preserved
    assert bak.read_text(encoding="utf-8") == orig.read_text(encoding="utf-8")


def test_backup_nonexistent_file_returns_none(tmp_path):
    from unity_mcp.config import backup
    result = backup.backup(tmp_path / "missing.json")
    assert result is None


def test_backup_filename_contains_timestamp(tmp_path):
    from unity_mcp.config import backup
    orig = tmp_path / "config.json"
    orig.write_text("{}", encoding="utf-8")
    bak = backup.backup(orig)
    # should contain a date-like substring e.g. 2026-06-19
    name = bak.name
    assert "config.json" in name
    assert ".bak" in name


# ─── validator.py ────────────────────────────────────────────────────────────

def test_validate_missing_config(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    missing = tmp_path / "nofile.json"
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", missing)
    report = validator.validate_config("claude-desktop")
    assert "not found" in report.lower()


def test_validate_invalid_json(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    bad = tmp_path / "bad.json"
    bad.write_text("not json {{", encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", bad)
    report = validator.validate_config("claude-desktop")
    assert "invalid json" in report.lower()


def test_validate_missing_unity_mcp_entry(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    report = validator.validate_config("claude-desktop")
    assert "not configured" in report.lower()


def test_validate_ok_report(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    from unity_mcp.config.merger import SERVER_NAME
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {SERVER_NAME: {"command": "uvx", "args": ["unity-biome-mcp"]}}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    # patch port check to skip network
    with patch("unity_mcp.config.validator._port_reachable", return_value=False):
        report = validator.validate_config("claude-desktop")
    assert SERVER_NAME in report.lower()


def test_validate_config_uses_read_unity_port_not_naive_find_port(tmp_path, monkeypatch):
    """M13: validate_config must resolve the port via server_filtering.read_unity_port
    (env var -> CWD-scoped -> PID-liveness-pruned -> newest mtime), NOT the naive
    config.resolver.find_port() (first-glob-match, no liveness check, no CWD scoping).
    Prove it by making the two disagree and asserting the report reflects
    read_unity_port's value."""
    from unity_mcp.config import clients as c, validator
    from unity_mcp.config.merger import SERVER_NAME
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {SERVER_NAME: {"command": "uvx", "args": ["unity-biome-mcp"]}}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    monkeypatch.setattr("unity_mcp.config.resolver.find_port", lambda: 1111)
    monkeypatch.setattr("unity_mcp.config.validator.read_unity_port", lambda skip_probe=False: 2222)
    with patch("unity_mcp.config.validator._port_reachable", return_value=False):
        report = validator.validate_config("claude-desktop")
    assert "2222" in report
    assert "1111" not in report


def test_validate_config_falls_back_to_default_port_when_read_unity_port_returns_none(tmp_path, monkeypatch):
    """read_unity_port(skip_probe=True) returns None when no live candidates are found
    (unlike find_port(), which always returns a port). validate_config must fall back
    to DEFAULT_PORT rather than crash or print 'None'."""
    from unity_mcp.config import clients as c, validator
    from unity_mcp.config.merger import SERVER_NAME
    from unity_mcp.constants import DEFAULT_PORT
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {SERVER_NAME: {"command": "uvx", "args": ["unity-biome-mcp"]}}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    monkeypatch.setattr("unity_mcp.config.validator.read_unity_port", lambda skip_probe=False: None)
    with patch("unity_mcp.config.validator._port_reachable", return_value=False):
        report = validator.validate_config("claude-desktop")
    assert str(DEFAULT_PORT) in report
    assert "None" not in report


def test_validate_config_key_matches_server_name(tmp_path, monkeypatch):
    """Drift guard: validate_config's server-key literal must be READ from
    SERVER_NAME, not an independently-hardcoded 'unity-mcp' that merely happens
    to agree with it today. Monkeypatch SERVER_NAME to a throwaway probe value:
    a genuinely-wired validator recognizes an entry keyed under the probe value;
    a hardcoded-literal validator would report it as missing instead."""
    from unity_mcp.config import clients as c, validator
    monkeypatch.setattr(validator, "SERVER_NAME", "probe-doctor")
    cfg = tmp_path / "cfg.json"
    cfg.write_text(
        json.dumps({"mcpServers": {"probe-doctor": {"command": "uvx", "args": ["unity-biome-mcp"]}}}),
        encoding="utf-8",
    )
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    with patch("unity_mcp.config.validator._port_reachable", return_value=False):
        report = validator.validate_config("claude-desktop")
    assert "probe-doctor" in report
    assert "not configured" not in report.lower()


# ─── resolver.py ─────────────────────────────────────────────────────────────

def test_find_server_command_prefers_venv(monkeypatch, tmp_path):
    from unity_mcp.config import resolver
    venv_py = tmp_path / ".venv" / "bin" / "python"
    venv_py.parent.mkdir(parents=True)
    venv_py.touch()
    monkeypatch.setattr(resolver.pathlib.Path, "__fspath__", lambda self: str(self))
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/local/bin/uvx" if name == "uvx" else None)
    # Point server_dir to tmp_path so venv is found
    monkeypatch.setattr(resolver.pathlib.Path, "parent", property(lambda self: type(self)(str(self).rsplit("/", 1)[0])))
    # Simpler: just patch find_python directly
    monkeypatch.setattr(resolver, "find_python", lambda: str(venv_py))
    cmd = resolver.find_server_command()
    assert cmd[0] == str(venv_py)
    assert "-m" in cmd


def test_find_server_command_falls_back_to_uvx(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "find_python", lambda: "uvx")
    cmd = resolver.find_server_command()
    assert cmd[0] == "uvx"
    assert "unity-biome-mcp" in cmd


def test_find_server_command_falls_back_to_python(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: None)
    with patch("pathlib.Path.exists", return_value=False):
        cmd = resolver.find_server_command()
    assert cmd[0] == sys.executable
    assert "-m" in cmd


def test_find_port_from_port_file(tmp_path, monkeypatch):
    from unity_mcp.config import resolver
    ports_dir = tmp_path / ".unity-biome-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "test.port").write_text("9501\n", encoding="utf-8")
    monkeypatch.setattr(resolver, "_ports_dir", lambda: ports_dir)
    assert resolver.find_port() == 9501


def test_find_port_default_when_no_files(tmp_path, monkeypatch):
    from unity_mcp.config import resolver
    empty_dir = tmp_path / "ports"
    empty_dir.mkdir()
    monkeypatch.setattr(resolver, "_ports_dir", lambda: empty_dir)
    assert resolver.find_port() == 9500


# ─── build_server_entry ──────────────────────────────────────────────────────

def test_build_server_entry_no_port(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/bin/uvx" if name == "uvx" else None)
    entry = resolver.build_server_entry()
    assert "command" in entry
    assert "args" in entry
    assert "env" not in entry


def test_build_server_entry_with_port(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/bin/uvx" if name == "uvx" else None)
    entry = resolver.build_server_entry(port=9501)
    assert entry["env"]["UNITY_MCP_PORT"] == "9501"


# ─── transform env passthrough ───────────────────────────────────────────────

def test_vscode_transform_passes_env():
    from unity_mcp.config.clients import _vscode_transform
    entry = {"command": "uvx", "args": ["unity-biome-mcp"], "env": {"PORT": "9500"}}
    result = _vscode_transform(entry)
    assert result["env"] == {"PORT": "9500"}


def test_opencode_transform_passes_env():
    # OpenCode uses the key "environment" (not "env") per opencode.ai/docs/mcp-servers.
    # Emitting "env" silently drops UNITY_MCP_PORT for OpenCode users.
    from unity_mcp.config.clients import _opencode_transform
    entry = {"command": "uvx", "args": ["unity-biome-mcp"], "env": {"PORT": "9500"}}
    result = _opencode_transform(entry)
    assert result["environment"] == {"PORT": "9500"}
    assert "env" not in result


# ─── merger.py: invalid JSON raises ─────────────────────────────────────────

def test_merge_invalid_json_raises_valueerror(tmp_path):
    from unity_mcp.config import merger
    bad = tmp_path / "config.json"
    bad.write_text("{ this is not json }", encoding="utf-8")
    with pytest.raises(ValueError, match="Corrupt JSON"):
        merger.merge_mcp_config(bad, {"command": "uvx", "args": []})


# ─── --project-dir: path resolution ─────────────────────────────────────────
# Tests exercise the config-layer logic (merger + clients) that --project-dir
# delegates to; we don't import install.py from here (wrong sys.path context).

def _project_config_path(project: pathlib.Path, tool_key: str) -> pathlib.Path:
    """Mirror of install._project_config_path — kept in sync by test contract."""
    paths = {
        "claude-code": project / ".mcp.json",
        "cursor": project / ".cursor" / "mcp.json",
        "vscode": project / ".vscode" / "mcp.json",
    }
    return paths.get(tool_key, project / ".mcp.json")


def _write_project_config(project: pathlib.Path, tool_key: str, entry: dict) -> pathlib.Path:
    from unity_mcp.config import merger, clients as c
    target = _project_config_path(project, tool_key)
    target.parent.mkdir(parents=True, exist_ok=True)
    client = c.CLIENT_REGISTRY.get(tool_key, c.CLIENT_REGISTRY["claude-code"])
    merger.merge_mcp_config(target, entry, root_key=client.root_key, entry_transformer=client.entry_transformer)
    return target


_ENTRY = {"command": "uvx", "args": ["unity-biome-mcp"]}


def test_configure_project_dir_claude_code(tmp_path):
    target = _write_project_config(tmp_path, "claude-code", _ENTRY)
    assert target == tmp_path / ".mcp.json"
    data = json.loads(target.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"]["command"] == "uvx"


def test_configure_project_dir_cursor(tmp_path):
    target = _write_project_config(tmp_path, "cursor", _ENTRY)
    assert target == tmp_path / ".cursor" / "mcp.json"
    data = json.loads(target.read_text(encoding="utf-8"))
    assert "unity-biome-mcp" in data["mcpServers"]


def test_configure_project_dir_vscode(tmp_path):
    target = _write_project_config(tmp_path, "vscode", _ENTRY)
    assert target == tmp_path / ".vscode" / "mcp.json"
    data = json.loads(target.read_text(encoding="utf-8"))
    assert "unity-biome-mcp" in data["servers"]


def test_project_merge_preserves_existing_servers(tmp_path):
    """Existing servers in .mcp.json must survive project-dir write."""
    cfg = tmp_path / ".mcp.json"
    cfg.write_text(json.dumps({"mcpServers": {"filesystem": {"command": "fs", "args": []}}}), encoding="utf-8")
    _write_project_config(tmp_path, "claude-code", _ENTRY)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "filesystem" in data["mcpServers"]
    assert "unity-biome-mcp" in data["mcpServers"]


# ─── merger.py: pin support (ARC-0b T3) ──────────────────────────────────────

def test_is_entry_pinned_false_when_file_missing(tmp_path):
    from unity_mcp.config import merger
    assert merger.is_entry_pinned(tmp_path / "nope.json") is False


def test_is_entry_pinned_false_when_absent(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": []}}}), encoding="utf-8")
    assert merger.is_entry_pinned(cfg) is False


def test_is_entry_pinned_true_when_pin_true(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": [], "_pin": True}}
    }), encoding="utf-8")
    assert merger.is_entry_pinned(cfg) is True


def test_is_entry_pinned_ignores_sibling_entry_pin(tmp_path):
    """A sibling MCP server's own "_pin" must never leak into our classification."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps({
        "mcpServers": {
            "other-mcp": {"command": "x", "_pin": True},
            "unity-biome-mcp": {"command": "uvx", "args": []},
        }
    }), encoding="utf-8")
    assert merger.is_entry_pinned(cfg) is False


def test_unpin_entry_removes_pin_key(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": [], "_pin": True}}
    }), encoding="utf-8")

    removed = merger.unpin_entry(cfg)

    assert removed is True
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "_pin" not in data["mcpServers"]["unity-biome-mcp"]
    assert data["mcpServers"]["unity-biome-mcp"]["command"] == "uvx"


def test_unpin_entry_returns_false_when_not_pinned(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    original = json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "uvx", "args": []}}})
    cfg.write_text(original, encoding="utf-8")

    assert merger.unpin_entry(cfg) is False
    assert cfg.read_text(encoding="utf-8") == original  # untouched, no rewrite


def test_is_toml_pinned_false_without_marker(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text("[mcp_servers.unity-biome-mcp]\ncommand = 'uvx'\nargs = []\n", encoding="utf-8")
    assert merger.is_toml_pinned(cfg) is False


def test_is_toml_pinned_false_when_file_missing(tmp_path):
    from unity_mcp.config import merger
    assert merger.is_toml_pinned(tmp_path / "nope.toml") is False


def test_is_toml_pinned_true_with_marker(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        "# unity-biome-mcp generated v0.54.1 pinned\n[mcp_servers.unity-biome-mcp]\ncommand = 'uvx'\nargs = []\n",
        encoding="utf-8",
    )
    assert merger.is_toml_pinned(cfg) is True


def test_pin_toml_entry_writes_marker(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": []})

    merger.pin_toml_entry(cfg, "0.54.1")

    text = cfg.read_text(encoding="utf-8")
    assert "# unity-biome-mcp generated v0.54.1 pinned\n[mcp_servers.unity-biome-mcp]" in text
    assert merger.is_toml_pinned(cfg) is True


def test_unpin_toml_entry_removes_marker(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": []})
    merger.pin_toml_entry(cfg, "0.54.1")

    removed = merger.unpin_toml_entry(cfg)

    assert removed is True
    text = cfg.read_text(encoding="utf-8")
    assert merger.is_toml_pinned(cfg) is False
    assert "[mcp_servers.unity-biome-mcp]" in text
    assert "command = 'uvx'" in text


def test_unpin_toml_entry_returns_false_when_not_pinned(tmp_path):
    """Symmetric with unpin_entry's bool contract (test_unpin_entry_returns_false_when_not_pinned)."""
    from unity_mcp.config import merger
    cfg = tmp_path / "config.toml"
    merger.merge_toml_mcp(cfg, {"command": "uvx", "args": []})
    original = cfg.read_text(encoding="utf-8")

    assert merger.unpin_toml_entry(cfg) is False
    assert cfg.read_text(encoding="utf-8") == original  # untouched, no rewrite
