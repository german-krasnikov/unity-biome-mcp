"""N0b: run_playtest yields one public success/error classification for
sync/async x text/json x pass/fail. A successful poll of a failed run must
not turn into public success just because the poll itself succeeded — see
Plans/V2/Next/PARETO-STAGE3-PLAN-2026-09-06.md N0b."""
import json
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools import playtest_async, runtime


def _passthrough(**kw):
    return {k: v for k, v in kw.items() if v is not None}


@pytest.fixture(autouse=True)
def _wire_runtime(monkeypatch):
    """Bind runtime._args and silence the real 1s poll sleep for the async route."""
    monkeypatch.setattr(runtime, "_args", _passthrough)
    monkeypatch.setattr(playtest_async.asyncio, "sleep", AsyncMock())


def _ledger_json(step_ok: bool) -> str:
    return json.dumps({
        "schema_version": 1, "run_id": "r1",
        "passed": 1 if step_ok else 0, "failed": 0 if step_ok else 1,
        "duration_seconds": 0.1,
        "steps": [{
            "index": 0, "type": "Assert", "ok": step_ok, "ms": 1.0,
            "source_file": "f.playtest", "source_line": 1,
            "raw_passed": step_ok, "expected_fail": False,
        }],
        "outer": {"teardown_ok": True, "scene_clean": True},
        "text_report": "whatever",
    })


async def test_async_fail_raises_tool_error_with_receipt(monkeypatch):
    """Async route (timeout > 120): failed run polled with transport ok:true
    must surface as a public ToolError, not a normal string return."""
    async def fake_send(cmd, args, **kw):
        if cmd == "start_playtest":
            return "run_id=abc"
        if cmd == "get_playtest_run":
            return "PLAYTEST: 0/1 (0.2s)\n[1] FAIL: HP"
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    with pytest.raises(ToolError, match="FAIL: HP"):
        await runtime.run_playtest(script="ASSERT /P|H|hp == 100", timeout=121)


async def test_async_pass_returns_report(monkeypatch):
    async def fake_send(cmd, args, **kw):
        if cmd == "start_playtest":
            return "run_id=abc"
        if cmd == "get_playtest_run":
            return "PLAYTEST: 1/1 (0.2s) OK"
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    result = await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN", timeout=121)
    assert "OK" in result


async def test_async_fail_json_receipt_preserved_in_error(monkeypatch):
    """format=json: the failed run's canonical receipt is preserved verbatim
    in the ToolError message — no text-report substitution."""
    receipt = _ledger_json(step_ok=False)

    async def fake_send(cmd, args, **kw):
        if cmd == "start_playtest":
            return "run_id=abc"
        if cmd == "get_playtest_run":
            return receipt
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    with pytest.raises(ToolError) as exc_info:
        await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN", timeout=121, format="json")

    parsed = json.loads(str(exc_info.value))
    assert parsed["failed"] == 1


@pytest.mark.parametrize("poll_response", ["", "PLAYTEST: 0/0 ERROR: timeout"])
async def test_async_malformed_terminal_is_error_not_pass(monkeypatch, poll_response):
    """An empty or 0/0 terminal poll is uninterpretable, not a pass."""
    async def fake_send(cmd, args, **kw):
        if cmd == "start_playtest":
            return "run_id=abc"
        if cmd == "get_playtest_run":
            return poll_response
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    with pytest.raises(ToolError):
        await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN", timeout=121)


async def test_sync_ok_true_with_failing_assert_is_not_success(monkeypatch):
    """Sync route: transport success with a failing assertion inside the report
    (LOG OK line present, ASSERT still failed) must not read as public success."""
    async def fake_send(cmd, args, **kw):
        if cmd == "run_playtest":
            return "PLAYTEST: 1/2 (0.3s) OK\n[2] FAIL: X"
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    with pytest.raises(ToolError, match="FAIL: X"):
        await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN")


async def test_sync_pass_unchanged(monkeypatch):
    async def fake_send(cmd, args, **kw):
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)

    result = await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN")
    assert "OK" in result


async def test_fail_report_raised_whole_not_summarized(monkeypatch):
    """A failing report over the compression/summarization length threshold must
    still reach the caller whole in the ToolError message — N0b's fail path
    short-circuits before _compress_report/summarize, so no evidence is lost
    to truncation or a Haiku summary (mirrors test_playtest_summarize.py's
    pass-only summarization contract)."""
    long_fail_report = "PLAYTEST: 1/3 (0.1s)\n[2] FAIL: X\n" + "x" * 400
    assert len(long_fail_report) > 300

    async def fake_send(cmd, args, **kw):
        if cmd == "start_playtest":
            return "run_id=abc"
        if cmd == "get_playtest_run":
            return long_fail_report
        raise AssertionError(f"unexpected command: {cmd}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value="SHOULD_NOT_BE_USED"))

    with patch("unity_mcp.tools.runtime._sampling", mock_svc), pytest.raises(ToolError) as exc_info:
        await runtime.run_playtest(script="ASSERT_CONSOLE_CLEAN", timeout=121)

    assert str(exc_info.value) == long_fail_report
    mock_svc.summarize.assert_not_called()
