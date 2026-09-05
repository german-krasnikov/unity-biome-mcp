"""Tests for Watch System tools (B4c: collapsed into single watch(action=...) dispatcher)."""
from unittest.mock import AsyncMock

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import get_watches, watch
from unity_mcp.tools.watch import WatchModule, register


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


def _plain_args(**kwargs) -> dict:
    """Mirrors server.py's own _args(**kwargs) factory (drop None values)."""
    return {k: v for k, v in kwargs.items() if v is not None}


async def test_two_watch_module_instances_do_not_cross_contaminate():
    """Instance-scoped _send/_args: two WatchModule objects must never share state.
    A naive module-global `bind(globals(), ...)` design fails this because a second
    register() call overwrites the shared _send/_args globals -- mod_a would start
    hitting send_b after mod_b is created."""
    # WatchModule awaits _send(...) directly and returns its result unwrapped --
    # the real server.py's _send() already unwraps {"ok":...,"data":...} before
    # returning, so a bound send callable here returns a plain string, matching
    # the contract WatchModule actually depends on.
    send_a = AsyncMock(return_value="from_a")
    send_b = AsyncMock(return_value="from_b")
    mod_a = WatchModule(send_a, _plain_args)
    mod_b = WatchModule(send_b, _plain_args)

    result_a1 = await mod_a.get_watches()
    send_a.assert_called_once()
    send_b.assert_not_called()
    assert result_a1 == "from_a"

    result_b1 = await mod_b.get_watches()
    send_b.assert_called_once()
    assert result_b1 == "from_b"

    # mod_a must still hit send_a after mod_b was used.
    result_a2 = await mod_a.get_watches()
    assert send_a.call_count == 2
    assert send_b.call_count == 1
    assert result_a2 == "from_a"


def test_register_called_twice_returns_independently_bound_modules():
    """The module-level register(mcp, send, args) compat shim remains a process-wide
    singleton pointer (documented, accepted scope limit) -- but each call must still
    return a fully independent WatchModule bound to its own send/args."""
    fake_mcp_a = type("FakeMcp", (), {"tool": lambda self, **_: (lambda fn: fn)})()
    fake_mcp_b = type("FakeMcp", (), {"tool": lambda self, **_: (lambda fn: fn)})()
    send_a = AsyncMock(return_value={"ok": True, "data": "from_a"})
    send_b = AsyncMock(return_value={"ok": True, "data": "from_b"})

    mod_a = register(fake_mcp_a, send_a, _plain_args)
    mod_b = register(fake_mcp_b, send_b, _plain_args)

    assert mod_a is not mod_b
    assert mod_a._send is send_a
    assert mod_b._send is send_b
