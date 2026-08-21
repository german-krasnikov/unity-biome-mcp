"""Tests: await_compile Hot Reload annotation.

When get_status returns hot_reload_detected=true, await_compile returns
immediately with current errors and a [hot-reload-mode] annotation.
When HR is inactive or get_status fails, normal polling path is used.
"""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock

import unity_mcp.tools.code_intel as _ci


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original_send = _ci._send
    original_cache = _ci._hr_cached
    _ci._hr_cached = None  # clean isolation: force fresh get_status call per test
    yield
    _ci._send = original_send
    _ci._hr_cached = original_cache


async def test_await_compile_hr_clean_returns_note():
    """HR active + no errors → 'compile clean' with [hot-reload-mode] annotation."""
    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=Sample\nhot_reload_detected=true\nplaying=false"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _ci._send = _fake_send
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result
    assert "hot-reload-mode" in result


async def test_await_compile_hr_with_errors_returns_errors_plus_note():
    """HR active + compile errors → both error text and [hot-reload-mode] annotation."""
    errors_text = "Assets/Foo.cs(1,1): error CS0103: name not found"

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "hot_reload_detected=true"
        if cmd == "get_compile_errors":
            return errors_text
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _ci._send = _fake_send
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value=errors_text)):
        result = await _ci.await_compile(timeout=60.0)
    assert errors_text in result
    assert "hot-reload-mode" in result


async def test_await_compile_normal_mode_unaffected():
    """HR inactive → normal polling path (no early return, sync_status called)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "hot_reload_detected=false"
        if cmd == "sync_status":
            sync_called.append(cmd)
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    _ci._send = _fake_send
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    # hot-reload-mode annotation must NOT appear
    assert "hot-reload-mode" not in result


async def test_await_compile_hr_check_fails_falls_through():
    """get_status raises → normal polling path used (fail-open)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            raise ConnectionError("bridge down")
        if cmd == "sync_status":
            sync_called.append(cmd)
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    _ci._send = _fake_send
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    assert "hot-reload-mode" not in result


async def test_await_compile_skips_get_status_when_hr_cached_false():
    """_hr_cached=False short-circuits the get_status call — normal polling proceeds."""
    get_status_calls = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            get_status_calls.append(cmd)
            return "hot_reload_detected=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    _ci._send = _fake_send
    _ci._hr_cached = False
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    assert not get_status_calls, "get_status must NOT be called when _hr_cached=False"
    assert "hot-reload-mode" not in result
