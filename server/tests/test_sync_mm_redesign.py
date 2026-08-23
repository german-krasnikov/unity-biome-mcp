"""Tests: sync_unity stamp-anchored skip + reload_type note (Tasks 1+2).

Task 1: stamp-anchored skip — only skips when _last_clean_stamp matches current stamp.
Task 2: reload_type suffix injected at clean sync exits when MM active.
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


@pytest.fixture(autouse=True)
def _reset_last_stamp():
    _sync._last_clean_stamp = ""
    yield
    _sync._last_clean_stamp = ""


# --- Task 1: stamp-anchored skip ---


async def test_sync_force_bypasses_mm_skip():
    """force=True bypasses stamp-stable skip even when MM active and stamp unchanged."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle|stamp=abc:123"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    _sync._last_clean_stamp = "abc:123"  # stamp stable

    await _sync.sync_unity(force=True)
    assert sync_called, "sync must be called when force=True even if stamp is stable"


async def test_sync_stamp_stable_skips():
    """When stamp matches last clean stamp and MM active, sync is skipped."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle|stamp=abc:123"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        return ""

    _sync._send = _fake_send
    _sync._last_clean_stamp = "abc:123"

    result = await _sync.sync_unity()
    assert not sync_called, "sync must NOT be called when stamp is stable"
    assert "mutation_mode" in result or "skipped" in result


async def test_sync_stamp_changed_proceeds():
    """When stamp differs from last clean stamp, sync proceeds (external edit detected)."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle|stamp=new:456"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    _sync._last_clean_stamp = "old:123"  # mismatch → must proceed

    await _sync.sync_unity()
    assert sync_called, "sync must proceed when stamp has changed"


async def test_sync_no_baseline_stamp_proceeds():
    """Empty _last_clean_stamp means no baseline — cannot skip, must proceed."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle|stamp=abc:123"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    # _last_clean_stamp = "" by default from fixture

    await _sync.sync_unity()
    assert sync_called, "sync must proceed when no baseline stamp exists"


async def test_sync_status_error_fails_open():
    """Connection error reading sync_status during skip check → fail-open, proceed."""
    sync_called = []
    sync_status_calls = [0]

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            sync_status_calls[0] += 1
            if sync_status_calls[0] == 1:
                raise ConnectionError("bridge down")  # skip check fails
            return "epoch=0|state=idle|stamp=abc:123"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    _sync._last_clean_stamp = "abc:123"

    await _sync.sync_unity()
    assert sync_called, "sync must proceed when sync_status raises during skip check"


# --- Task 2: reload_type suffix ---


async def test_sync_reload_type_domain():
    """When MM active + MVID changes pre→post → '(domain reload)' suffix."""
    call_count = [0]

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            call_count[0] += 1
            if call_count[0] == 1:
                return "epoch=0|state=idle|stamp=pre:111"
            return "epoch=1|state=ready|stamp=post:222"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    reload_risk.touch()  # has_touches → falls through MM guard

    result = await _sync.sync_unity()
    assert "(domain reload)" in result, f"Expected '(domain reload)' in: {result!r}"


async def test_sync_reload_type_hot():
    """When MM active + MVID unchanged (hot reload in-process) → '(hot reload)' suffix."""
    call_count = [0]

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            call_count[0] += 1
            if call_count[0] == 1:
                return "epoch=0|state=idle|stamp=abc:123"
            return "epoch=1|state=ready|stamp=abc:123"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd in ("get_compile_errors", "force_refresh", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    reload_risk.touch()

    with patch("unity_mcp.tools.sync._attempt_recovery", new=AsyncMock(return_value=None)):
        result = await _sync.sync_unity()
    assert "(hot reload)" in result, f"Expected '(hot reload)' in: {result!r}"


async def test_sync_reload_type_no_mm():
    """When MM inactive, result contains 'clean' but no reload type suffix."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=false"
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send

    result = await _sync.sync_unity()
    assert "clean" in result, f"Expected 'clean' in: {result!r}"
    assert "(domain reload)" not in result
    assert "(hot reload)" not in result


async def test_sync_force_false_default():
    """force defaults to False — stamp-stable skip applies without explicit force=False."""
    sync_called = []

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=0|state=idle|stamp=abc:123"
        if cmd == "sync":
            sync_called.append(cmd)
            return "sync_ack|epoch=1|will_compile=false"
        return ""

    _sync._send = _fake_send
    _sync._last_clean_stamp = "abc:123"

    result = await _sync.sync_unity()  # no force kwarg → defaults False
    assert not sync_called, "sync must NOT be called (force=False is default)"


async def test_sync_success_seeds_stamp_then_next_call_skips():
    """E2E: first sync succeeds → _last_clean_stamp set → second call skips."""

    async def _fake_send(cmd, args=None, **kwargs):
        if cmd == "get_status":
            return "mutation_mode=true"
        if cmd == "sync_status":
            return "epoch=1|state=idle|stamp=mvid42:999"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=false"
        if cmd in ("get_compile_errors", "warm_type_cache"):
            return ""
        return ""

    _sync._send = _fake_send
    reload_risk.touch()

    # First call: has_touches → skip guard bypassed → sync proceeds → stamp saved
    result1 = await _sync.sync_unity()
    assert "clean" in result1
    assert _sync._last_clean_stamp == "mvid42:999"

    # Reset touches — now no touches, stamp should match
    reload_risk.reset()

    # Second call: no touches + stamp stable → should skip
    sync_called = []
    original_fake = _fake_send

    async def _tracking_send(cmd, args=None, **kwargs):
        if cmd == "sync":
            sync_called.append(cmd)
        return await original_fake(cmd, args, **kwargs)

    _sync._send = _tracking_send
    result2 = await _sync.sync_unity()
    assert not sync_called, "Second call must skip (stamp stable, no touches)"
    assert "skipped" in result2.lower()


async def test_reconnect_resets_last_clean_stamp():
    """_reset_last_clean_stamp clears the module-level stamp."""
    _sync._last_clean_stamp = "some:stamp"
    _sync._reset_last_clean_stamp()
    assert _sync._last_clean_stamp == ""
