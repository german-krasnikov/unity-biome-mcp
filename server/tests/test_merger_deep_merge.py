"""Tests for _deep_merge and its wiring into merge_mcp_config (ARC-12 T1).

Regression coverage for the P7 bug: merge_mcp_config used to whole-value
replace data[root_key][SERVER_NAME], deleting any user-added key (top-level
or nested inside env) that wasn't part of the new entry. See
Plans/consumer-reports/ARC-12-python-deep-merge.md.
"""
import json

from unity_mcp.config.merger import merge_mcp_config


def test_merge_preserves_custom_env_key_on_env_less_reentry(tmp_path):
    """The literal P7 reproduction: install.py update passes an env-less entry
    (port=0 shape) and must not wipe a pre-existing custom env var."""
    cfg = tmp_path / "config.json"
    existing = {
        "mcpServers": {
            "unity-biome-mcp": {
                "command": "old",
                "args": [],
                "env": {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"},
            }
        }
    }
    cfg.write_text(json.dumps(existing), encoding="utf-8")

    merge_mcp_config(cfg, {"command": "new", "args": ["-m", "x"]})

    data = json.loads(cfg.read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["env"] == {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"}
    assert entry["command"] == "new"


def test_merge_updates_only_specified_env_subkey(tmp_path):
    """A merge that supplies a new env value for an existing sub-key must
    update that sub-key while still preserving unrelated sub-keys."""
    cfg = tmp_path / "config.json"
    existing = {
        "mcpServers": {
            "unity-biome-mcp": {
                "command": "old",
                "args": [],
                "env": {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"},
            }
        }
    }
    cfg.write_text(json.dumps(existing), encoding="utf-8")

    merge_mcp_config(cfg, {"command": "old", "args": [], "env": {"UNITY_MCP_PORT": "9999"}})

    data = json.loads(cfg.read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["env"] == {"UNITY_MCP_PORT": "9999", "CUSTOM_VAR": "keepme"}


def test_merge_preserves_unknown_top_level_key(tmp_path):
    """A hand-added top-level key (e.g. 'disabled') must survive a re-merge
    that doesn't mention it, while our own keys still get updated."""
    cfg = tmp_path / "config.json"
    existing = {
        "mcpServers": {
            "unity-biome-mcp": {"command": "old", "args": [], "disabled": True}
        }
    }
    cfg.write_text(json.dumps(existing), encoding="utf-8")

    merge_mcp_config(cfg, {"command": "new", "args": ["x"]})

    data = json.loads(cfg.read_text(encoding="utf-8"))
    entry = data["mcpServers"]["unity-biome-mcp"]
    assert entry["disabled"] is True
    assert entry["command"] == "new"


def test_merge_malformed_existing_entry_falls_back_to_overlay(tmp_path):
    """A malformed on-disk entry (not a dict) must not crash the merge —
    degrade to the new entry wholesale, same 'degrade, don't crash' spirit
    as this file's corrupt-JSON handling."""
    cfg = tmp_path / "config.json"
    existing = {"mcpServers": {"unity-biome-mcp": "not-a-dict"}}
    cfg.write_text(json.dumps(existing), encoding="utf-8")

    merge_mcp_config(cfg, {"command": "new", "args": []})

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"] == {"command": "new", "args": []}
