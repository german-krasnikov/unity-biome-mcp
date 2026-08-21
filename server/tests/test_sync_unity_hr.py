"""Tests: sync_unity Hot Reload coexistence guard.

When mcp_status / get_status returns hot_reload_detected=true,
sync_unity must return a warning immediately without sending 'sync'.
If the status check fails (connection error), sync must proceed normally (fail-open).
"""
import pytest
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.sync as _sync


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _sync._send
    yield
    _sync._send = original


async def test_sync_unity_skips_when_hr_detected():
    """When HR active, sync_unity warns and does NOT call 'sync' command."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nhot_reload_detected=true\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        return ""

    _sync._send = _fake_send
    result = await _sync.sync_unity()
    assert "warn" in result.lower() or "hot reload" in result.lower() or "hot_reload" in result.lower()
    assert not sync_called, "sync command must NOT be called when HR is detected"


async def test_sync_unity_proceeds_when_hr_not_detected():
    """When HR inactive, sync_unity proceeds and sends 'sync' command."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nhot_reload_detected=false\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    result = await _sync.sync_unity()
    assert sync_called, "sync command MUST be called when HR is not detected"


async def test_sync_unity_proceeds_when_get_status_fails():
    """Connection error during get_status does NOT block sync (fail-open)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            raise ConnectionError("bridge down")
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    result = await _sync.sync_unity()
    assert sync_called, "sync must proceed when get_status raises"
