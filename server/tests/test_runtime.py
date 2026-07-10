"""Tests for runtime Play Mode tools."""
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import (
    invoke_method, set_runtime_property, wait_until, query_state, move_to,
    set_active, wire_event, unwire_event, run_playtest,
)


async def test_invoke_method_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    result = await invoke_method("/Player", "PlayerController", "Jump", "5.0")
    mock_bridge.send.assert_called_once_with(
        "invoke_method",
        {"path": "/Player", "component": "PlayerController", "method": "Jump", "args": "5.0"},
        timeout=30.0,
    )
    assert result == "void"


async def test_invoke_method_no_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    await invoke_method("/Player", "PlayerController", "Jump")
    call_args = mock_bridge.send.call_args[0][1]
    assert "args" not in call_args


async def test_set_runtime_property_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "speed=10"}
    result = await set_runtime_property("/Player", "PlayerController", "speed", "10")
    mock_bridge.send.assert_called_once_with(
        "set_runtime_property",
        {"path": "/Player", "component": "PlayerController", "field": "speed", "value": "10"},
        timeout=30.0,
    )
    assert result == "speed=10"


async def test_wait_until_default_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "field=true after 1.2s"}
    result = await wait_until("/Player", "PlayerController", "isAlive", "true")
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "wait_until"
    sent = call_args[0][1]
    assert sent["timeout"] == "5.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 10.0


async def test_wait_until_with_negate(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Enemy", "EnemyAI", "isAlive", "true", negate=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["negate"] == "true"


async def test_wait_until_custom_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Player", "PlayerController", "hp", "0", timeout=15.0)
    call_args = mock_bridge.send.call_args
    sent = call_args[0][1]
    assert sent["timeout"] == "15.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 20.0


async def test_wait_until_abort_on_fail_passes_arg(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/P", "C", "f", "v", abort_on_fail=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["abort_on_fail"] == "true"


async def test_wait_until_abort_on_fail_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/P", "C", "f", "v", abort_on_fail=False)
    sent = mock_bridge.send.call_args[0][1]
    assert "abort_on_fail" not in sent


async def test_run_playtest_abort_on_fail_passes_arg(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", abort_on_fail=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["abort_on_fail"] == "true"


async def test_run_playtest_abort_on_fail_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "abort_on_fail" not in sent


async def test_query_state_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "GridPlayer.Score=5\nGridPlayer.PosX=3"}
    result = await query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
    mock_bridge.send.assert_called_once_with(
        "query_state",
        {"queries": "/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX"},
        timeout=10.0,
    )
    assert "GridPlayer.Score=5" in result


async def test_query_state_multiple_objects(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Health.hp=80\nAmmo.count=5"}
    result = await query_state("/Player|Health|hp,/Player|Ammo|count")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["queries"] == "/Player|Health|hp,/Player|Ammo|count"
    assert result == "Health.hp=80\nAmmo.count=5"


# --- P1-4: move_to bridge call shape ---

async def test_move_to_sends_correct_command(mock_bridge):
    """move_to sends cmd='move_to' with timeout as str and python timeout = timeout+5."""
    mock_bridge.send.return_value = {"ok": True, "data": "arrived"}
    await move_to("/Player", "5,0,-3")
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "move_to"
    sent = call_args[0][1]
    assert sent["path"] == "/Player"
    assert sent["position"] == "5,0,-3"
    assert sent["timeout"] == "15.0"          # str(float(default=15.0))
    assert call_args[1]["timeout"] == 20.0    # timeout + 5.0


async def test_move_to_custom_timeout_offset(mock_bridge):
    """Python-level timeout is always unity timeout + 5.0."""
    mock_bridge.send.return_value = {"ok": True, "data": "arrived"}
    await move_to("/Enemy", "0,0,0", timeout=30.0)
    call_args = mock_bridge.send.call_args
    sent = call_args[0][1]
    assert sent["timeout"] == "30.0"
    assert call_args[1]["timeout"] == 35.0    # 30 + 5


# ─── ok=False → ToolError (write tools) ──────────────────────────────────────

@pytest.mark.parametrize("active,err_msg", [
    (True,  "Object not found"),
    (False, "Scene path invalid"),
])
async def test_set_active_error_raises_tool_error_runtime(mock_bridge, active, err_msg):
    """set_active raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": err_msg})
    with pytest.raises(ToolError, match=err_msg):
        await set_active("/Missing", active)


async def test_wire_event_error_raises_tool_error_runtime(mock_bridge):
    """wire_event raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Event field not found"})
    with pytest.raises(ToolError, match="Event field not found"):
        await wire_event("/Btn", "Button", "onClick", "/Target", "SetActive")


async def test_unwire_event_error_raises_tool_error_runtime(mock_bridge):
    """unwire_event raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No listeners to remove"})
    with pytest.raises(ToolError, match="No listeners to remove"):
        await unwire_event("/Btn", "Button", "onClick")


# ─── Phase 1: Section/MOVE_PATH DSL extensions ───────────────────────────────

def test_compress_report_preserves_section_lines():
    from unity_mcp.tools.runtime import _compress_report
    report = (
        "PLAYTEST: 3/3 (1.0s)\n"
        "[1] ASSERT x == 1 — PASS (1)\n"
        "--- Movement Phase ---\n"
        "[2] ASSERT y == 2 — PASS (2)"
    )
    result = _compress_report(report)
    assert "--- Movement Phase ---" in result


async def test_move_path_sends_script(mock_bridge):
    """run_playtest passes MOVE_PATH script to bridge unchanged."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 3/3 (1.0s) OK"}
    script = "MOVE_PATH 1,0,0 > 5,0,0 > 10,0,3"
    await run_playtest(script)
    sent = mock_bridge.send.call_args[0][1]
    assert "MOVE_PATH" in sent["script"]
