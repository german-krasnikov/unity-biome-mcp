"""Tests: sync_unity Mutation Mode coexistence guard.

When mcp_status / get_status returns mutation_mode=true AND no script writes,
sync_unity must return a skip message without sending 'sync'.
When mutation_mode=true AND has_touches() is True, sync falls through normally.
If the status check fails (connection error), sync must proceed normally (fail-open).
No caching: every call must re-read get_status — a later false must clear a prior true.
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
    yield
    _sync._send = original_send


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


async def test_sync_unity_rechecks_when_mm_false():
    """mutation_mode=false → always re-checks get_status (no False caching)."""
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
    await _sync.sync_unity()
    assert get_status_calls, "get_status MUST be called every time"
    assert sync_called, "sync must proceed when mutation_mode=false"


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


async def test_sync_unity_no_stale_true_cache():
    """After mutation_mode=true (skip) then false, second call must proceed — no stale cache."""
    call_count = [0]
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            call_count[0] += 1
            # First call: true (skip); second call: false (proceed)
            return "mutation_mode=true" if call_count[0] == 1 else "mutation_mode=false"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send

    # First call: mutation_mode=true, skip
    result1 = await _sync.sync_unity()
    assert not sync_called, "first call with true must skip sync"

    # Second call: mutation_mode=false, must proceed
    result2 = await _sync.sync_unity()
    assert sync_called, "second call with false must proceed — no stale True cache"
