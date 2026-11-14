"""Tests for run_tests — immediate return (no inline polling) + pre-flight recovery."""
import asyncio
from unittest.mock import AsyncMock
import unity_mcp.tools.testing as scene_mod


async def test_run_tests_connection_error_returns_unknown(monkeypatch):
    """TCP death cannot prove whether Unity dispatched the run."""
    async def fake_send(cmd, args={}, **kw):
        if cmd == "resolve_test_request":
            return "none"
        raise ConnectionError("going_away")

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode", request_id="req-connection")
    assert result == (
        "START-UNKNOWN|request_id=req-connection|reason=ConnectionError"
    )


async def test_run_tests_timeout_returns_unknown(monkeypatch):
    async def fake_send(cmd, args={}, **kw):
        if cmd == "resolve_test_request":
            return "none"
        raise asyncio.TimeoutError()

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("PlayMode", request_id="req-timeout")
    assert result == "START-UNKNOWN|request_id=req-timeout|reason=TimeoutError"


async def test_run_tests_requires_protocol_ack(monkeypatch):
    async def fake_send(cmd, args={}, **kw):
        if cmd == "resolve_test_request":
            return "none"
        return "passed: 5 failed: 0"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode", request_id="req-legacy")
    assert result == "START-UNKNOWN|request_id=req-legacy|reason=invalid-ack"


async def test_run_tests_pending_is_not_fabricated_as_started(monkeypatch):
    async def fake_send(cmd, args={}, **kw):
        if cmd == "resolve_test_request":
            return "none"
        return "pending"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode", request_id="req-pending")
    assert result == "START-UNKNOWN|request_id=req-pending|reason=invalid-ack"


# ---------------------------------------------------------------------------
# Pre-flight auto-recovery tests
# ---------------------------------------------------------------------------

async def test_preflight_recovery_retries_on_stale_dll(monkeypatch):
    """FAIL:stale-dll → force_refresh → re-diagnose → CLEAN-LIVE → proceeds."""
    import unity_mcp.tools.diagnose as diag_mod

    call_count = 0
    async def fake_diagnose(prev_mvid="", expected_compile=True):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            return "FAIL:stale-dll"
        return "CLEAN-LIVE"

    force_refresh_called = False
    original_send = scene_mod._send

    async def fake_send(cmd, args={}, **kw):
        nonlocal force_refresh_called
        if cmd == "force_refresh":
            force_refresh_called = True
            return "ok"
        if cmd == "run_tests":
            request_id = args["request_id"]
            return (
                f"tests-started|request_id={request_id}|run_id=run-recovered"
                "|utf_guid=utf-recovered|state=dispatched"
            )
        return await original_send(cmd, args, **kw)

    monkeypatch.setattr(diag_mod, "diagnose", fake_diagnose)
    monkeypatch.setattr(scene_mod, "_send", fake_send)
    monkeypatch.setattr(asyncio, "sleep", AsyncMock())

    result = await scene_mod.run_tests("EditMode")
    assert force_refresh_called, "force_refresh must be called on FAIL:stale-dll"
    assert "BLOCKED" not in result


async def test_preflight_real_compile_error_blocks_immediately(monkeypatch):
    """FAIL:CS0117 → BLOCKED immediately, no recovery attempt."""
    import unity_mcp.tools.diagnose as diag_mod

    async def fake_diagnose(prev_mvid="", expected_compile=True):
        return "FAIL:CS0117"

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
    """FAIL:unknown persists after retries → BLOCKED with exhausted message."""
    import unity_mcp.tools.diagnose as diag_mod

    async def fake_diagnose(prev_mvid="", expected_compile=True):
        return "FAIL:unknown"

    async def fake_send(cmd, args={}, **kw):
        return "ok"

    monkeypatch.setattr(diag_mod, "diagnose", fake_diagnose)
    monkeypatch.setattr(scene_mod, "_send", fake_send)
    monkeypatch.setattr(asyncio, "sleep", AsyncMock())

    result = await scene_mod.run_tests("EditMode")
    assert "BLOCKED" in result
    assert "auto-recovery exhausted" in result
