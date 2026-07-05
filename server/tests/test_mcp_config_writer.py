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
    assert "unity-mcp-config-9601.json" in path


def test_claude_config_has_unity_mcp_chat(tmp_path):
    path = write_claude_config(str(tmp_path), 9601)
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    env = data["mcpServers"]["unity-kiss"]["env"]
    assert env.get("UNITY_MCP_CHAT") == "1"


def test_claude_config_port_correct(tmp_path):
    path = write_claude_config(str(tmp_path), 9999)
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    env = data["mcpServers"]["unity-kiss"]["env"]
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
    assert args == ["unity-mcp"]


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
