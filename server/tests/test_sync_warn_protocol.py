"""Tests: sync_unity must treat warn:/err: ack as hard failures (MCPAUDIT-015).

The false-green bug: Unity returns 'warn:ScriptCompilationDuringPlay blocked',
_parse_ack silently fails, caller gets 'sync clean' even though code is NOT updated.
"""
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.sync as _sync
from unity_mcp import reload_risk


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original_send = _sync._send
    yield
    _sync._send = original_send


@pytest.fixture(autouse=True)
def _reset_reload_risk():
    reload_risk.reset()
    yield
    reload_risk.reset()


async def test_sync_unity_warn_ack_raises_tool_error():
    """warn: prefix from Unity sync → ToolError, never 'sync clean'."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "warn:ScriptCompilationDuringPlay blocked"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    with pytest.raises(ToolError, match="sync blocked"):
        await _sync.sync_unity()


async def test_sync_unity_err_ack_raises_tool_error():
    """err: prefix from Unity sync → ToolError."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "err:RefreshFailed"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    with pytest.raises(ToolError, match="sync blocked"):
        await _sync.sync_unity()


async def test_sync_unity_normal_ack_returns_clean():
    """Normal sync_ack|epoch=N|will_compile=false → 'sync clean' (regression anchor)."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    result = await _sync.sync_unity()
    assert "clean" in result.lower()


async def test_sync_never_returns_clean_on_warn():
    """Negative: result must NOT contain 'clean' when warn: received."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "warn:ScriptCompilationDuringPlay blocked"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    with pytest.raises(ToolError) as exc_info:
        await _sync.sync_unity()
    assert "clean" not in str(exc_info.value).lower()


async def test_sync_warn_with_mm_and_touches_raises():
    """Exact MCPAUDIT-015 scenario: MM active + has_touches + Play Mode + warn: → ToolError."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=true\nplaying=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "warn:ScriptCompilationDuringPlay blocked"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    reload_risk.touch()  # has_touches → MM guard doesn't skip, falls through to sync
    with pytest.raises(ToolError, match="sync blocked"):
        await _sync.sync_unity()


async def test_sync_unity_none_ack_raises_tool_error():
    """ack=None (bridge timeout sentinel) → ToolError, not AttributeError."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return None
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    with pytest.raises(ToolError, match="unexpected ack type"):
        await _sync.sync_unity()
