"""Tests for result envelope consistency fixes (MCP091-005/007/008/015/016/019).

These tests verify Python propagates ok:false from C# as ToolError.
All tests mock bridge.send — no Unity required.
"""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools.batch import batch
from unity_mcp.tools.runtime import run_playtest, wait_until
from unity_mcp.tools.rendering import render_analyze


# ── MCP091-016: batch envelope ──────────────────────────────────────────────

async def test_batch_errors_raise_tool_error(mock_bridge):
    """C# returns ok:false when batch summary has err:N — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "[0] err: cmd failed\nok:0 err:1"})
    with pytest.raises(ToolError):
        await batch(commands="set_property path=/ component=X prop=y value=z")


async def test_batch_all_ok_returns_string(mock_bridge):
    """C# returns ok:true when batch has no errors — Python must return the data string."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:2"})
    result = await batch(commands="get_hierarchy\nget_console")
    assert result == "ok:2"


# ── MCP091-007/008/019: run_playtest isSuccess ───────────────────────────────

async def test_run_playtest_busy_raises_tool_error(mock_bridge):
    """C# returns ok:false for 'ERROR: Playtest already running.' — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "ERROR: Playtest already running."})
    with pytest.raises(ToolError):
        await run_playtest(script="WAIT 1")


async def test_run_playtest_parse_error_raises_tool_error(mock_bridge):
    """C# returns ok:false for 'PARSE ERROR:...' — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "PARSE ERROR: unexpected token at line 1"})
    with pytest.raises(ToolError):
        await run_playtest(script="INVALID DSL TOKEN")


async def test_run_playtest_step_error_raises_tool_error(mock_bridge):
    """C# returns ok:false for step-level '— ERROR' lines — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": False,
        "err": "PLAYTEST: 0/1 (0.5s)\n[1] ASSERT /P|H|hp == 10 — ERROR method not found",
    })
    with pytest.raises(ToolError):
        await run_playtest(script="ASSERT /P|H|hp == 10")


async def test_run_playtest_pass_returns_string(mock_bridge):
    """C# returns ok:true on clean pass — Python must return the report string."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "PLAYTEST: 3/3 (1.2s) OK"})
    result = await run_playtest(script="WAIT 1")
    assert "3/3" in result


# ── MCP091-005: render_analyze bad action ───────────────────────────────────

async def test_render_analyze_bad_action_raises_tool_error(mock_bridge):
    """C# throws on unknown action → ok:false — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": False,
        "err": "Unknown action 'badmode'. Valid: stats|materials|...",
    })
    with pytest.raises(ToolError):
        await render_analyze(action="badmode")


# ── MCP091-015: wait_until error ────────────────────────────────────────────

async def test_wait_until_bad_field_raises_tool_error(mock_bridge):
    """C# returns ok:false for 'wait_until error:...' — Python must raise ToolError."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": False,
        "err": "wait_until error: Field 'hp' not found on Health",
    })
    with pytest.raises(ToolError):
        await wait_until(path="/Player", component="Health", field="hp", value="100")
