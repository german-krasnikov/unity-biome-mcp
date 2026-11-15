"""Tests for the shared atomic-write helpers in config/merger.py.

Consolidates the write-tmp-then-os.replace pattern that used to be repeated
seven times as `config_path.with_suffix(".tmp")` across merge_mcp_config,
unpin_entry, remove_mcp_entry (JSON) and merge_toml_mcp, pin_toml_entry,
unpin_toml_entry, remove_toml_mcp_entry (TOML text). Sonar flagged the JSON
trio as S2083 (path constructed from tainted config_path); the shared
".tmp" name also collided across writers and left a stale artifact behind
on a failed os.replace.
"""
import inspect
import json
import os
import stat

import pytest

from unity_mcp.config import merger
from unity_mcp.config.merger import _replace_json_atomic, _replace_text_atomic


def test_replace_json_atomic_leaves_no_temp_file_on_success(tmp_path):
    cfg = tmp_path / "config.json"

    _replace_json_atomic(cfg, {"a": 1})

    assert cfg.read_text(encoding="utf-8") == json.dumps({"a": 1}, indent=2, ensure_ascii=False)
    assert list(tmp_path.glob("*.tmp")) == []


def test_replace_json_atomic_removes_temp_file_on_failure(tmp_path, monkeypatch):
    cfg = tmp_path / "config.json"
    cfg.write_text("original", encoding="utf-8")

    def boom(_src, _dst):
        raise OSError("replace failed")

    monkeypatch.setattr(os, "replace", boom)

    with pytest.raises(OSError):
        _replace_json_atomic(cfg, {"a": 1})

    assert cfg.read_text(encoding="utf-8") == "original"
    assert list(tmp_path.glob("*.tmp")) == []


@pytest.mark.skipif(os.name == "nt", reason="POSIX chmod semantics")
def test_replace_text_atomic_preserves_existing_file_mode(tmp_path):
    """mkstemp creates the temp file at 0o600; without copying the existing
    file's mode onto it first, os.replace would silently downgrade a
    0o644 client config (e.g. ~/.claude.json, .mcp.json) to 0o600."""
    cfg = tmp_path / "config.json"
    cfg.write_text("original", encoding="utf-8")
    cfg.chmod(0o644)

    _replace_text_atomic(cfg, "updated")

    assert stat.S_IMODE(cfg.stat().st_mode) == 0o644


def test_merger_module_has_no_with_suffix_tmp_construction():
    """Guards against reintroducing the tainted-path pattern Sonar flagged
    (S2083): `tmp = config_path.with_suffix(".tmp")`. Every writer must
    funnel through _replace_text_atomic / _replace_json_atomic instead."""
    source = inspect.getsource(merger)
    assert 'with_suffix(".tmp")' not in source
