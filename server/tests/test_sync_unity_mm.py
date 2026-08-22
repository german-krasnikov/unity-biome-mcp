"""Tests: sync_unity Mutation Mode coexistence guard.

When mcp_status / get_status returns mutation_mode=true AND no script writes,
sync_unity must return a skip message without sending 'sync'.
When mutation_mode=true AND has_touches() is True, sync falls through normally.
If the status check fails (connection error), sync must proceed normally (fail-open).
"""
from unittest.mock import AsyncMock, patch

import pytest

import unity_mcp.tools.sync as _sync
from unity_mcp import reload_risk


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original_send = _sync._send
    original_cache = _sync._mm_cached
    _sync._mm_cached = None  # clean isolation: force fresh get_status call per test
    yield
    _sync._send = original_send
    _sync._mm_cached = original_cache


@pytest.fixture(autouse=True)
def _reset_reload_risk():
    reload_risk.reset()
    yield
    reload_risk.reset()


async def test_sync_unity_skips_when_hr_detected():
    """When mutation mode active, sync_unity warns and does NOT call 'sync' command."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=true\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        return ""

    _sync._send = _fake_send
    result = await _sync.sync_unity()
    assert "warn" in result.lower() or "mutation_mode" in result.lower() or "mutation mode" in result.lower()
    assert not sync_called, "sync command must NOT be called when mutation mode is detected"


async def test_sync_unity_proceeds_when_hr_not_detected():
    """When mutation mode inactive, sync_unity proceeds and sends 'sync' command."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=false\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    await _sync.sync_unity()
    assert sync_called, "sync command MUST be called when mutation mode is not detected"


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
    await _sync.sync_unity()
    assert sync_called, "sync must proceed when get_status raises"


async def test_sync_unity_skips_get_status_when_mm_cached_false():
    """When _mm_cached=False, get_status is not called — sync proceeds without the network round-trip."""
    get_status_calls = []
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            get_status_calls.append(cmd)
            return "mutation_mode=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    _sync._mm_cached = False  # simulate previously known-not-MM
    await _sync.sync_unity()
    assert not get_status_calls, "get_status must NOT be called when _mm_cached=False"
    assert sync_called, "sync must proceed when cache says mutation mode is inactive"


async def test_sync_unity_proceeds_when_mm_active_and_has_touches():
    """MM active + has_touches() True → sync falls through (script writes need compile)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=true\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    reload_risk.touch()  # simulate a script write
    await _sync.sync_unity()
    assert sync_called, "sync MUST proceed when MM active but has_touches=True"


async def test_sync_unity_skips_when_mm_active_and_no_touches():
    """MM active + no script writes → sync skipped, returned immediately."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "scene=SampleScene\nmutation_mode=true\nplaying=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        return ""

    _sync._send = _fake_send
    # No reload_risk.touch() — has_touches() returns False
    result = await _sync.sync_unity()
    assert not sync_called, "sync must NOT be called when MM active and no touches"
    assert "mutation_mode" in result or "no script writes" in result
