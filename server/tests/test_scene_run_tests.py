"""Tests for run_tests — immediate return (no inline polling) + pre-flight recovery."""
import asyncio
from unittest.mock import AsyncMock
import unity_mcp.tools.scene as scene_mod


async def test_run_tests_connection_error_returns_started(monkeypatch):
    """TCP dies → returns tests-started immediately, no polling."""
    async def fake_send(cmd, args={}, **kw):
        raise ConnectionError("going_away")

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "tests-started" in result
    assert "EditMode" in result


async def test_run_tests_timeout_returns_started(monkeypatch):
    """Timeout → returns tests-started immediately."""
    async def fake_send(cmd, args={}, **kw):
        raise asyncio.TimeoutError()

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("PlayMode")
    assert "tests-started" in result
    assert "PlayMode" in result


async def test_run_tests_full_result_returned_directly(monkeypatch):
    """Unity returns full result (no domain reload) → returned as-is."""
    async def fake_send(cmd, args={}, **kw):
        return "passed: 5 failed: 0"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "passed: 5" in result


async def test_run_tests_pending_returns_started(monkeypatch):
    """Unity returns 'pending' → treated as no result."""
    async def fake_send(cmd, args={}, **kw):
        return "pending"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "tests-started" in result


# ---------------------------------------------------------------------------
# Pre-flight auto-recovery tests
# ---------------------------------------------------------------------------

async def test_preflight_recovery_retries_on_stale_dll(monkeypatch):
    """FAILED:stale-dll → force_refresh → re-diagnose → CLEAN-LIVE → proceeds."""
    import unity_mcp.tools.diagnose as diag_mod

    call_count = 0
    async def fake_diagnose(prev_mvid="", expected_compile=True):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return "FAILED:stale-dll"
        return "CLEAN-LIVE"

    force_refresh_called = False
    original_send = scene_mod._send

    async def fake_send(cmd, args={}, **kw):
        nonlocal force_refresh_called
        if cmd == "force_refresh":
            force_refresh_called = True
            return "ok"
        if cmd == "run_tests":
            return "passed: 5 failed: 0"
        return await original_send(cmd, args, **kw)

    monkeypatch.setattr(diag_mod, "diagnose", fake_diagnose)
    monkeypatch.setattr(scene_mod, "_send", fake_send)
    monkeypatch.setattr(asyncio, "sleep", AsyncMock())

    result = await scene_mod.run_tests("EditMode")
    assert force_refresh_called, "force_refresh must be called on FAILED:stale-dll"
    assert "BLOCKED" not in result


async def test_preflight_real_compile_error_blocks_immediately(monkeypatch):
    """FAILED:CS0117 → BLOCKED immediately, no recovery attempt."""
    import unity_mcp.tools.diagnose as diag_mod

    async def fake_diagnose(prev_mvid="", expected_compile=True):
        return "FAILED:CS0117"

    force_refresh_called = False

    async def fake_send(cmd, args={}, **kw):
        nonlocal force_refresh_called
        if cmd == "force_refresh":
            force_refresh_called = True
        return "ok"

    monkeypatch.setattr(diag_mod, "diagnose", fake_diagnose)
    monkeypatch.setattr(scene_mod, "_send", fake_send)

    result = await scene_mod.run_tests("EditMode")
    assert "BLOCKED" in result
    assert "fix domain state" in result
    assert not force_refresh_called, "force_refresh must NOT be called on real compile error"


async def test_preflight_recovery_exhausted(monkeypatch):
    """FAILED:unknown persists after retries → BLOCKED with exhausted message."""
    import unity_mcp.tools.diagnose as diag_mod

    async def fake_diagnose(prev_mvid="", expected_compile=True):
        return "FAILED:unknown"

    async def fake_send(cmd, args={}, **kw):
        return "ok"

    monkeypatch.setattr(diag_mod, "diagnose", fake_diagnose)
    monkeypatch.setattr(scene_mod, "_send", fake_send)
    monkeypatch.setattr(asyncio, "sleep", AsyncMock())

    result = await scene_mod.run_tests("EditMode")
    assert "BLOCKED" in result
    assert "auto-recovery exhausted" in result
