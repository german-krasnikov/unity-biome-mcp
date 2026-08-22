"""Tests: await_compile Mutation Mode annotation.

When get_status returns mutation_mode=true, await_compile returns
immediately with current errors and a [hot-reload-mode] annotation.
When mutation mode is inactive or get_status fails, normal polling path is used.
"""
from unittest.mock import AsyncMock, patch

import pytest

import unity_mcp.tools.code_intel as _ci
from unity_mcp import reload_risk


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_reload_risk():
    reload_risk.reset()
    yield
    reload_risk.reset()


@pytest.fixture(autouse=True)
def _reset_send():
    original_send = _ci._send
    original_cache = _ci._mm_cached
    _ci._mm_cached = None  # clean isolation: force fresh get_status call per test
    yield
    _ci._send = original_send
    _ci._mm_cached = original_cache


async def test_await_compile_mm_no_touches_returns_clean():
    """Mutation mode active + no script writes → immediate 'compile clean' (no polling)."""
    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=Sample\nmutation_mode=true\nplaying=false"
        raise AssertionError(f"Unexpected cmd: {cmd}")  # no polling expected

    _ci._send = _fake_send
    # No reload_risk.touch() — has_touches() returns False
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result
    assert "no script writes" in result


async def test_await_compile_mm_with_touches_falls_through_to_polling():
    """Mutation mode active + has_touches → falls through to normal polling (no early return)."""
    sync_status_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            sync_status_called.append(cmd)
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    _ci._send = _fake_send
    reload_risk.touch()  # simulate script write
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    # Must NOT have the annotation — normal polling path
    assert "hot-reload-mode" not in result


async def test_await_compile_normal_mode_unaffected():
    """Mutation mode inactive → normal polling path (no early return, sync_status called)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=false"
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


async def test_await_compile_rechecks_when_mm_cached_none_or_false():
    """_mm_cached=None/False re-checks get_status (no sticky False caching)."""
    get_status_calls = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            get_status_calls.append(cmd)
            return "mutation_mode=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "compile_status":
            return "idle|0.0"
        return ""

    _ci._send = _fake_send
    _ci._mm_cached = None  # unknown state → should call get_status
    with patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _ci.await_compile(timeout=60.0)
    assert get_status_calls, "get_status MUST be called when _mm_cached is not True"
    assert "hot-reload-mode" not in result
