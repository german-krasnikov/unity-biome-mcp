"""Tests for the import-time preflight guard (Domain-A crash -> one stderr line)."""
import importlib.util
import sys

import pytest


def test_run_preflight_python_too_old_exits_2_with_fatal_line(monkeypatch, capsys):
    from unity_mcp import _preflight

    monkeypatch.setattr(sys, "version_info", (3, 9, 0))
    with pytest.raises(SystemExit) as exc_info:
        _preflight.run_preflight()

    assert exc_info.value.code == 2
    err = capsys.readouterr().err
    assert err.strip().startswith("UNITY-BIOME-MCP-FATAL:")
    assert "| fix:" in err


def test_run_preflight_missing_mcp_exits_2(monkeypatch, capsys):
    from unity_mcp import _preflight

    real_find_spec = importlib.util.find_spec

    def fake_find_spec(name, *a, **kw):
        if name == "mcp":
            return None
        return real_find_spec(name, *a, **kw)

    monkeypatch.setattr(importlib.util, "find_spec", fake_find_spec)
    with pytest.raises(SystemExit) as exc_info:
        _preflight.run_preflight()

    assert exc_info.value.code == 2
    err = capsys.readouterr().err
    assert err.strip().startswith("UNITY-BIOME-MCP-FATAL:")
    assert "| fix:" in err


def test_run_preflight_happy_path_returns_none_prints_nothing(capsys):
    from unity_mcp import _preflight

    result = _preflight.run_preflight()

    assert result is None
    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == ""
