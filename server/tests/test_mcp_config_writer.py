"""Tests for mcp_config_writer.py — config file creation and server cmd resolution."""
import json
import os
import sys
from pathlib import Path
from unittest.mock import patch

import pytest

from unity_mcp.mcp_config_writer import (
    resolve_server_cmd,
    write_claude_config,
    write_kimi_mcp_config,
    write_agy_settings,
    write_opencode_config,
)


def test_write_claude_config_creates_file(tmp_path):
    path = write_claude_config(str(tmp_path), 9601)
    assert Path(path).exists()
    assert "unity-biome-mcp-config-9601.json" in path


def test_claude_config_port_correct(tmp_path):
    path = write_claude_config(str(tmp_path), 9999)
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    env = data["mcpServers"]["unity-biome-mcp"]["env"]
    assert env.get("UNITY_MCP_PORT") == "9999"


def test_resolve_server_cmd_venv(tmp_path, monkeypatch):
    """When sys.prefix != sys.base_prefix (in venv) and no .venv dir, returns sys.executable."""
    # Point module's server_dir to tmp_path so .venv/bin/python doesn't exist there
    monkeypatch.setattr("unity_mcp.mcp_config_writer.Path",
                        lambda *a, **kw: _FakePath(tmp_path, *a, **kw))
    monkeypatch.setattr(sys, "prefix", "/fake/venv")
    monkeypatch.setattr(sys, "base_prefix", "/usr")
    cmd, args = resolve_server_cmd()
    assert cmd == sys.executable
    assert args == ["-m", "unity_mcp.server"]


def test_resolve_server_cmd_uvx_fallback(tmp_path, monkeypatch):
    """When not in venv and no .venv, uvx is returned when shutil.which finds it."""
    monkeypatch.setattr("unity_mcp.mcp_config_writer.Path",
                        lambda *a, **kw: _FakePath(tmp_path, *a, **kw))
    monkeypatch.setattr(sys, "prefix",      "/usr")
    monkeypatch.setattr(sys, "base_prefix", "/usr")
    monkeypatch.setattr("unity_mcp.mcp_config_writer.shutil.which",
                        lambda b: "/opt/homebrew/bin/uvx" if b == "uvx" else None)
    cmd, args = resolve_server_cmd()
    assert cmd == "/opt/homebrew/bin/uvx"
    assert args == ["--quiet", "unity-biome-mcp"]


# ── Fake Path helper ──────────────────────────────────────────────────────────

class _FakePath:
    """Path shim: __file__ chain returns tmp_path; .exists() always False."""
    def __init__(self, base, *args, **kwargs):
        self._base = base

    def __truediv__(self, other):
        return _FakePath(self._base)

    @property
    def parent(self):
        return _FakePath(self._base)

    def exists(self) -> bool:
        return False

    def write_text(self, *a, **kw):
        pass

    def read_text(self, *a, **kw):
        return ""

    def __str__(self):
        return str(self._base)


# ─── M7: _atomic_write uses os.replace ───────────────────────────────────────

def test_atomic_write_uses_os_replace(tmp_path, monkeypatch):
    calls = []
    _real = os.replace
    monkeypatch.setattr(os, "replace", lambda s, d: calls.append((s, d)) or _real(s, d))

    from unity_mcp.mcp_config_writer import _atomic_write
    path = str(tmp_path / "out.json")
    _atomic_write(path, '{"ok":1}')

    assert len(calls) == 1
    assert calls[0][1] == path
    assert Path(path).read_text(encoding="utf-8") == '{"ok":1}'


def test_atomic_write_overwrites_existing(tmp_path):
    from unity_mcp.mcp_config_writer import _atomic_write
    path = str(tmp_path / "cfg.json")
    _atomic_write(path, "first")
    _atomic_write(path, "second")
    assert Path(path).read_text(encoding="utf-8") == "second"


# ── RC-3: port=0 must omit UNITY_MCP_PORT (use discovery instead of baked port) ──

def test_claude_config_port_zero_omits_env(tmp_path):
    """RC-3: mcp_port=0 → no UNITY_MCP_PORT env so Python uses discovery files."""
    path = write_claude_config(str(tmp_path), 0)
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert "env" not in entry, "port=0 must not bake UNITY_MCP_PORT into config"


def test_kimi_config_port_zero_omits_env(tmp_path):
    write_kimi_mcp_config(str(tmp_path), 0)
    data = json.loads((tmp_path / "mcp.json").read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert "env" not in entry


def test_agy_config_port_zero_omits_env(tmp_path):
    write_agy_settings(str(tmp_path), 0)
    data = json.loads((tmp_path / "settings.json").read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert "env" not in entry


def test_opencode_config_port_zero_omits_env(tmp_path):
    write_opencode_config(str(tmp_path), 0)
    path = tmp_path / "opencode-unity-biome-mcp-0.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    entry = data["mcp"]["unity-biome-mcp"]
    assert "environment" not in entry


def test_kimi_config_nonzero_port_includes_env(tmp_path):
    """Existing callers that pass an explicit port still get the env key."""
    write_kimi_mcp_config(str(tmp_path), 9601)
    data = json.loads((tmp_path / "mcp.json").read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["env"]["UNITY_MCP_PORT"] == "9601"


# ── ARC-12 T2: writers must deep-merge, not wholesale-replace the entry ──────

def test_kimi_config_preserves_custom_env_on_rewrite(tmp_path):
    """A pre-existing custom env var (or any key next to UNITY_MCP_PORT) must
    survive a re-write that omits it (port=0 shape), while our own keys still
    get updated. Reproduces RC2 (mcp_config_writer.py hand-rolled replace)."""
    path = tmp_path / "mcp.json"
    existing = {
        "mcpServers": {
            "unity-biome-mcp": {
                "command": "old",
                "args": [],
                "env": {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"},
            }
        }
    }
    path.write_text(json.dumps(existing), encoding="utf-8")

    with patch("unity_mcp.mcp_config_writer.resolve_server_cmd", return_value=("new", ["-m", "x"])):
        write_kimi_mcp_config(str(tmp_path), 0)

    data = json.loads(path.read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["env"] == {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"}
    assert entry["command"] == "new"


def test_agy_settings_preserves_custom_top_level_key_on_rewrite(tmp_path):
    """A hand-added top-level key (e.g. 'customFlag') must survive a re-write,
    while our own keys (command, trust) still get updated."""
    path = tmp_path / "settings.json"
    existing = {
        "mcpServers": {
            "unity-biome-mcp": {
                "command": "old",
                "args": [],
                "trust": True,
                "customFlag": "value",
            }
        }
    }
    path.write_text(json.dumps(existing), encoding="utf-8")

    with patch("unity_mcp.mcp_config_writer.resolve_server_cmd", return_value=("new", ["-m", "x"])):
        write_agy_settings(str(tmp_path), 0)

    data = json.loads(path.read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["customFlag"] == "value"
    assert entry["command"] == "new"
    assert entry["trust"] is True


# ── DEV-56: corrupt JSON must not wipe existing config (fail loud, don't overwrite) ──

def test_write_kimi_mcp_config_refuses_to_wipe_on_corrupt_json(tmp_path):
    """Corrupt mcp.json (unparseable) must be left untouched, not replaced with
    an entry containing only our server — other-server must survive on disk."""
    path = tmp_path / "mcp.json"
    corrupt = '{"mcpServers": {"other-server": {"command": "y"}}} trailing garbage'
    path.write_text(corrupt, encoding="utf-8")

    write_kimi_mcp_config(str(tmp_path), 9601)

    raw = path.read_text(encoding="utf-8")
    assert "other-server" in raw
    assert raw == corrupt


def test_write_agy_settings_refuses_to_wipe_on_corrupt_json(tmp_path):
    """Corrupt settings.json (unparseable) must be left untouched, not replaced
    with an entry containing only our server — other-server must survive on disk."""
    path = tmp_path / "settings.json"
    corrupt = '{"mcpServers": {"other-server": {"command": "y"}}} trailing garbage'
    path.write_text(corrupt, encoding="utf-8")

    write_agy_settings(str(tmp_path), 9601)

    raw = path.read_text(encoding="utf-8")
    assert "other-server" in raw
    assert raw == corrupt


def test_write_kimi_mcp_config_returns_false_on_corrupt_json(tmp_path):
    path = tmp_path / "mcp.json"
    path.write_text("{ not json", encoding="utf-8")
    assert write_kimi_mcp_config(str(tmp_path), 9601) is False


def test_write_agy_settings_returns_false_on_corrupt_json(tmp_path):
    path = tmp_path / "settings.json"
    path.write_text("{ not json", encoding="utf-8")
    assert write_agy_settings(str(tmp_path), 9601) is False


def test_write_kimi_mcp_config_returns_true_on_success(tmp_path):
    assert write_kimi_mcp_config(str(tmp_path), 9601) is True


def test_write_agy_settings_returns_true_on_success(tmp_path):
    assert write_agy_settings(str(tmp_path), 9601) is True
