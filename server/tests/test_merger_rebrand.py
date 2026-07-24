"""Tests for SERVER_NAME, _OLD_NAMES, and migration in merger.py."""
import json
from pathlib import Path
from unity_mcp.config import merger
from unity_mcp.config.merger import SERVER_NAME, _OLD_NAMES, merge_mcp_config


def test_server_name_is_biome():
    """The canonical server name must be unity-biome-mcp."""
    assert SERVER_NAME == "unity-biome-mcp"


def test_old_names_includes_unity_mcp():
    """_OLD_NAMES must include 'unity-mcp' to migrate prior installs."""
    assert "unity-mcp" in _OLD_NAMES


def test_merge_mcp_migrates_old_unity_mcp_key(tmp_path):
    """JSON entry under 'unity-mcp' key is replaced by 'unity-biome-mcp'."""
    old = {"mcpServers": {"unity-mcp": {"command": "uvx", "args": ["unity-mcp"]}}}
    cfg = tmp_path / "config.json"
    cfg.write_text(json.dumps(old), encoding="utf-8")
    merge_mcp_config(cfg, {"command": "uvx", "args": ["unity-biome-mcp"]})
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "unity-mcp" not in data["mcpServers"], "old key must be removed"
    assert "unity-biome-mcp" in data["mcpServers"], "new key must be present"


def test_merge_mcp_idempotent_with_new_key(tmp_path):
    """Writing 'unity-biome-mcp' twice doesn't duplicate or corrupt."""
    cfg = tmp_path / "config.json"
    entry = {"command": "uvx", "args": ["unity-biome-mcp"]}
    cfg.write_text(json.dumps({"mcpServers": {}}), encoding="utf-8")
    merge_mcp_config(cfg, entry)
    merge_mcp_config(cfg, entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    keys = list(data["mcpServers"].keys())
    assert keys.count("unity-biome-mcp") == 1, "no duplicate key"
