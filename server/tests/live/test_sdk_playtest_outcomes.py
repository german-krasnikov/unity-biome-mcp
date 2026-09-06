"""Live integration test: run_playtest outcomes agree through the real
middleware pipeline against real Unity, across sync/async routes and
text/json formats. Qualifies N0b's `_classify_outcome` and the public
ToolError gate through the production `run_playtest` wrapper -- `sdk_runtime`
binds runtime._send to a shim mirroring server._send_raw (unwrap + raise
ToolError on wire ok:false), the same composition production uses
(wrap_send(_send_raw, middleware)). Requires Unity Editor running with MCP
plugin. Run with: pytest -m live
"""
import json

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools import runtime

pytestmark = pytest.mark.live

# `# @needs editmode` opts these ASSERT-only scripts out of the Play Mode gate
# (AI/testing.md "EditMode DSL Execution") -- these tests stay in EditMode.
# ASSERT_CONSOLE_CLEAN is intentionally omitted: the DSL step checks the
# whole console buffer (ConsoleCapture.GetLogs(20, "error"),
# PlaytestRunner.Steps.cs:69), not just entries logged since this test
# started, so it is unsuitable after other live tests in the same session
# have already run and may have left unrelated errors sitting in that buffer.
_SCRIPT_PASS = "# @needs editmode\nASSERT /Main Camera|Camera|enabled == True"
_SCRIPT_FAIL = "# @needs editmode\nASSERT /Main Camera|Camera|enabled == False"


async def test_playtest_sync_pass_text_and_json_agree(sdk_runtime):
    """A passing script returns a plain string on both formats, never raises."""
    raw_text = await sdk_runtime.run_playtest(script=_SCRIPT_PASS, format="text")
    assert sdk_runtime._classify_outcome(raw_text, "text") == "pass", raw_text

    raw_json = await sdk_runtime.run_playtest(script=_SCRIPT_PASS, format="json")
    assert sdk_runtime._classify_outcome(raw_json, "json") == "pass", raw_json


async def test_playtest_sync_fail_text_and_json_agree(sdk_runtime):
    """A failing script raises ToolError (N0b) with the report/receipt as evidence."""
    with pytest.raises(ToolError) as excinfo_text:
        await sdk_runtime.run_playtest(script=_SCRIPT_FAIL, format="text")
    raw_text = str(excinfo_text.value)
    assert sdk_runtime._classify_outcome(raw_text, "text") == "fail", raw_text

    with pytest.raises(ToolError) as excinfo_json:
        await sdk_runtime.run_playtest(script=_SCRIPT_FAIL, format="json")
    raw_json = str(excinfo_json.value)
    assert sdk_runtime._classify_outcome(raw_json, "json") == "fail", raw_json
    receipt = json.loads(raw_json)
    assert receipt["failed"] > 0, receipt
    assert any(step.get("ok") is False for step in receipt["steps"]), receipt


async def test_playtest_json_receipt_structure(sdk_runtime):
    """format=json returns the canonical step-ledger receipt unchanged on pass."""
    raw_json = await sdk_runtime.run_playtest(script=_SCRIPT_PASS, format="json")
    receipt = json.loads(raw_json)
    assert receipt["outer"]["teardown_ok"] is True, receipt
    assert receipt["failed"] == 0, receipt
    steps = receipt["steps"]
    assert isinstance(steps, list) and len(steps) >= 1, receipt
    assert all("ok" in step for step in steps), receipt


async def test_playtest_async_route_agrees_with_sync(sdk_runtime, monkeypatch):
    """timeout=121 (> playtest_async._RUN_PLAYTEST_SYNC_CEILING_S == 120) forces
    run_playtest through the non-blocking start_playtest/get_playtest_run route
    (playtest_async.run_via_start_poll) instead of one blocking TCP call. A
    recording spy on runtime._send is the evidence the route was actually
    taken. Both routes must agree on outcome and on raising the same public
    ToolError for a non-pass result (N0b) -- this is the public-parity live
    proof. The spy also checks sdk_runtime.middleware._scenario_uncertain
    (N0a) right after start_playtest's ack -- the guard must be armed the
    instant a real playtest is in flight, then cleared once the terminal
    get_playtest_run response lands, on both the pass and fail leg.
    """
    recorded_cmds: list[str] = []
    underlying_send = runtime._send

    async def _spy_send(cmd, args, timeout=0):
        recorded_cmds.append(cmd)
        result = await underlying_send(cmd, args, timeout=timeout)
        if cmd == "start_playtest":
            assert sdk_runtime.middleware._scenario_uncertain, (
                "N0a guard should be armed right after start_playtest ack"
            )
        return result

    monkeypatch.setattr(runtime, "_send", _spy_send)

    for script, expected in ((_SCRIPT_PASS, "pass"), (_SCRIPT_FAIL, "fail")):
        recorded_cmds.clear()
        if expected == "pass":
            sync_raw = await sdk_runtime.run_playtest(script=script, format="text")
        else:
            with pytest.raises(ToolError) as excinfo:
                await sdk_runtime.run_playtest(script=script, format="text")
            sync_raw = str(excinfo.value)
        assert recorded_cmds == ["run_playtest"], recorded_cmds
        sync_outcome = sdk_runtime._classify_outcome(sync_raw, "text")
        assert sync_outcome == expected, sync_raw

        recorded_cmds.clear()
        if expected == "pass":
            async_raw = await sdk_runtime.run_playtest(
                script=script, format="text", timeout=121)
        else:
            with pytest.raises(ToolError) as excinfo:
                await sdk_runtime.run_playtest(script=script, format="text", timeout=121)
            async_raw = str(excinfo.value)
        assert "start_playtest" in recorded_cmds, recorded_cmds
        assert "get_playtest_run" in recorded_cmds, recorded_cmds
        async_outcome = sdk_runtime._classify_outcome(async_raw, "text")
        assert async_outcome == sync_outcome == expected, (async_raw, sync_raw)
        assert not sdk_runtime.middleware._scenario_uncertain, (
            "N0a guard must clear once the terminal get_playtest_run "
            f"response lands ({expected} leg)"
        )
