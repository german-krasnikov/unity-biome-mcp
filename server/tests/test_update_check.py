"""Tests for update check module."""
import json
import time
from unittest.mock import patch, MagicMock
from unity_mcp._update_check import (
    _is_newer, _read_cache, _write_cache, check_for_update, format_update_banner,
    CACHE_FILE,
)


def test_is_newer_true():
    assert _is_newer("0.38.0", "0.37.0")


def test_is_newer_false_same():
    assert not _is_newer("0.37.0", "0.37.0")


def test_is_newer_false_older():
    assert not _is_newer("0.36.0", "0.37.0")


def test_is_newer_major():
    assert _is_newer("1.0.0", "0.37.0")


def test_is_newer_minor():
    assert _is_newer("0.38.1", "0.38.0")


def test_cache_write_read(tmp_path, monkeypatch):
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", tmp_path / "cache.json")
    data = {"ts": time.time(), "latest": "0.38.0"}
    _write_cache(data)
    result = _read_cache()
    assert result["latest"] == "0.38.0"


def test_cache_expired(tmp_path, monkeypatch):
    cache_file = tmp_path / "cache.json"
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", cache_file)
    old = {"ts": time.time() - 90000, "latest": "0.38.0"}
    cache_file.write_text(json.dumps(old), encoding="utf-8")
    # check_for_update should ignore expired cache — here we just verify read works
    result = _read_cache()
    assert result["ts"] < time.time() - 86400


def test_cache_missing(tmp_path, monkeypatch):
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", tmp_path / "nofile.json")
    assert _read_cache() is None


def test_cache_corrupt(tmp_path, monkeypatch):
    cache_file = tmp_path / "cache.json"
    cache_file.write_text("not json{{{", encoding="utf-8")
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", cache_file)
    assert _read_cache() is None


def test_check_for_update_network_error(tmp_path, monkeypatch):
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", tmp_path / "cache.json")
    with patch("urllib.request.urlopen", side_effect=OSError("timeout")):
        assert check_for_update() is None


def test_check_for_update_uses_cache(tmp_path, monkeypatch):
    cache_file = tmp_path / "cache.json"
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", cache_file)
    cache_file.write_text(json.dumps({"ts": time.time(), "latest": "99.99.0"}), encoding="utf-8")
    with patch("urllib.request.urlopen") as mock_url:
        result = check_for_update()
        mock_url.assert_not_called()
    assert result == "99.99.0"


def test_check_for_update_no_update(tmp_path, monkeypatch):
    cache_file = tmp_path / "cache.json"
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", cache_file)
    monkeypatch.setattr("unity_mcp._update_check.__version__", "1.0.0")
    cache_file.write_text(json.dumps({"ts": time.time(), "latest": "0.1.0"}), encoding="utf-8")
    with patch("urllib.request.urlopen") as mock_url:
        result = check_for_update()
        mock_url.assert_not_called()
    assert result is None


def test_check_for_update_fetches_github(tmp_path, monkeypatch):
    monkeypatch.setattr("unity_mcp._update_check.CACHE_FILE", tmp_path / "cache.json")
    mock_resp = MagicMock()
    mock_resp.read.return_value = json.dumps({"tag_name": "v99.99.0"}).encode()
    mock_resp.__enter__ = lambda s: s
    mock_resp.__exit__ = MagicMock(return_value=False)
    with patch("urllib.request.urlopen", return_value=mock_resp):
        result = check_for_update()
    assert result == "99.99.0"
    # Cache was written
    assert (tmp_path / "cache.json").exists()


def test_format_update_banner():
    banner = format_update_banner("0.38.0")
    assert "0.38.0" in banner
    assert "uvx" in banner
