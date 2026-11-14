"""Tests for MCP sampling tools: auto_fix, smart_build, SamplingService.generate."""
import os
from unittest.mock import AsyncMock, MagicMock, patch
from unity_mcp.sampling import SamplingService
from unity_mcp.tools.codegen import auto_fix, smart_build


def _make_ctx(sampling_result=None, sampling_error=None):
    """Build a mock Context with create_message behavior."""
    ctx = MagicMock()
    if sampling_error:
        ctx.session.create_message = AsyncMock(side_effect=sampling_error)
    else:
        msg = MagicMock()
        msg.content = [MagicMock(text=sampling_result or "Fix the code")]
        ctx.session.create_message = AsyncMock(return_value=msg)
    return ctx


async def test_generate_returns_text_on_success(monkeypatch):
    """P1-10: SamplingService.generate happy path — returns text from _run."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    svc = SamplingService()
    with patch.object(svc, "_run", new=AsyncMock(return_value="generated text")):
        result = await svc.generate("write a hello world script")
    assert result == "generated text"


async def test_auto_fix_no_errors(mock_bridge):
    mock_bridge.send.side_effect = [
        {"ok": True, "data": ""},                         # get_console
        {"ok": True, "data": "No compilation errors"},    # get_compile_errors
    ]
    ctx = _make_ctx()
    result = await auto_fix(ctx)
    assert "No errors" in result


async def test_auto_fix_with_errors_no_sampling(mock_bridge):
    mock_bridge.send.side_effect = [
        {"ok": True, "data": "[Error] NullRef in Player.cs:42"},  # get_console
        {"ok": True, "data": "Assets/Player.cs(5,3): error CS0001"},  # get_compile_errors
    ]
    ctx = _make_ctx(sampling_error=NotImplementedError("sampling not supported"))
    result = await auto_fix(ctx)
    assert "ERRORS" in result
    assert "Auto-fix unavailable" in result


async def test_smart_build_no_sampling(mock_bridge):
    ctx = _make_ctx(sampling_error=NotImplementedError("no sampling"))
    result = await smart_build("create a red cube", ctx)
    assert "Sampling unavailable" in result
    assert "execute_code" in result


# ── P2: verify_visual_diff both-files guard ───────────────────────────────────

async def test_verify_visual_diff_returns_none_when_before_missing(tmp_path, monkeypatch):
    """before file does not exist → returns None immediately, no subprocess."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    after = tmp_path / "after.png"
    after.write_bytes(b"img")

    svc = SamplingService()
    result = await svc.verify_visual_diff(
        str(tmp_path / "before.png"),  # missing
        str(after),
        "Did anything change?",
    )
    assert result is None


async def test_verify_visual_diff_returns_none_when_after_missing(tmp_path, monkeypatch):
    """after file does not exist → returns None immediately, no subprocess."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    before = tmp_path / "before.png"
    before.write_bytes(b"img")

    svc = SamplingService()
    result = await svc.verify_visual_diff(
        str(before),
        str(tmp_path / "after.png"),  # missing
        "Did anything change?",
    )
    assert result is None


async def test_verify_visual_diff_both_present_calls_run(tmp_path, monkeypatch):
    """both files exist → _run is called (gated by enabled+_gate)."""
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    before = tmp_path / "before.png"
    after = tmp_path / "after.png"
    before.write_bytes(b"img")
    after.write_bytes(b"img")

    svc = SamplingService()
    with patch.object(svc, "_run", new=AsyncMock(return_value="PASS: nothing changed")):
        result = await svc.verify_visual_diff(str(before), str(after), "Compare")

    assert result == "PASS: nothing changed"
