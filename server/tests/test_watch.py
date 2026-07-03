"""Tests for Watch System tools (B4c: collapsed into single watch(action=...) dispatcher)."""
import pytest
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import watch, get_watches


async def test_watch_add_dispatches_watch_add_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    result = await watch("add", path="/Player", component="Health", field="hp")
    mock_bridge.send.assert_called_once_with(
        "watch_add",
        {"path": "/Player", "component": "Health", "field": "hp"},
        timeout=30.0,
    )
    assert result == "w1"


async def test_watch_add_with_all_params(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w2"}
    await watch("add", path="/Player", component="Health", field="hp",
                condition="< 10", trigger_action="pause", interval_ms=250)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["condition"] == "< 10"
    assert sent["action"] == "pause"
    assert sent["interval_ms"] == "250"


async def test_watch_add_omits_optional_defaults(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch("add", path="/Player", component="Health", field="hp")
    sent = mock_bridge.send.call_args[0][1]
    assert "condition" not in sent
    assert "action" not in sent
    assert "interval_ms" not in sent


async def test_watch_add_json_key_is_action_not_trigger_action(mock_bridge):
    """Regression guard: the Python param is renamed trigger_action (dispatcher already
    owns `action` for add|remove|clear|reset), but the wire JSON key sent to Unity's
    WatchCommandHandler must stay 'action'."""
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch("add", path="/Player", component="Health", field="hp", trigger_action="pause")
    sent = mock_bridge.send.call_args[0][1]
    assert "trigger_action" not in sent
    assert sent["action"] == "pause"


async def test_get_watches_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "watches: 0"}
    result = await get_watches()
    mock_bridge.send.assert_called_once_with("get_watches", {}, timeout=30.0)
    assert result == "watches: 0"


async def test_watch_remove_dispatches_watch_remove_command(mock_bridge):
    """JSON key stays 'id' — WatchCommandHandler.cs reads JsonHelper.ExtractString(args, 'id')."""
    mock_bridge.send.return_value = {"ok": True, "data": "removed"}
    result = await watch("remove", watch_id="w1")
    mock_bridge.send.assert_called_once_with(
        "watch_remove", {"id": "w1"}, timeout=30.0
    )
    assert result == "removed"


async def test_watch_clear_dispatches_watch_clear_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "cleared"}
    result = await watch("clear")
    mock_bridge.send.assert_called_once_with("watch_clear", {}, timeout=30.0)
    assert result == "cleared"


async def test_watch_reset_dispatches_watch_reset_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "reset"}
    result = await watch("reset", watch_id="w1")
    mock_bridge.send.assert_called_once_with(
        "watch_reset", {"id": "w1"}, timeout=30.0
    )
    assert result == "reset"


async def test_watch_add_interval_ms_as_string(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch("add", path="/Go", component="Comp", field="field", interval_ms=1000)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["interval_ms"] == "1000"
    assert isinstance(sent["interval_ms"], str)


async def test_watch_add_action_log_omitted_as_default(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch("add", path="/Go", component="Comp", field="field", trigger_action="log")
    sent = mock_bridge.send.call_args[0][1]
    assert "action" not in sent


async def test_watch_unknown_action_raises_tool_error():
    with pytest.raises(ToolError, match="Unknown watch action"):
        await watch("bogus")
