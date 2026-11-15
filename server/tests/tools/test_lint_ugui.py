"""G2: lint_ugui tool — command forwarding and response tests.

Tests verify:
- correct cmd sent to bridge
- warning response returned as-is
- root param forwarded when provided
- root NOT forwarded when absent (None filtered by _args)
"""
from unittest.mock import AsyncMock

from unity_mcp.server import lint_ugui


async def test_lint_ugui_sends_correct_cmd(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_ugui()
    cmd = mock_bridge.send.call_args[0][0]
    assert cmd == "lint_ugui"
    # Double-red:
    # 1. Change to cmd == "wrong_cmd" → fails
    # 2. Delete lint_ugui → ImportError → RED


async def test_lint_ugui_no_eventsystem_warning_in_response(mock_bridge):
    warning = "warn: EventSystem missing — UI clicks will not register"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": warning})
    result = await lint_ugui()
    assert result == warning
    # Double-red:
    # 1. Change to assert result == "something else" → fails
    # 2. Tool wraps response → assertion fails → RED


async def test_lint_ugui_root_param_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_ugui(root="/Canvas")
    args = mock_bridge.send.call_args[0][1]
    assert args["root"] == "/Canvas"
    # Double-red:
    # 1. Change to args["root"] == "/Wrong" → fails
    # 2. Remove root forwarding in lint_ugui → key absent → KeyError → RED


async def test_lint_ugui_root_none_by_default(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_ugui()
    args = mock_bridge.send.call_args[0][1]
    assert "root" not in args
    # Double-red:
    # 1. Change to assert "root" in args → fails when absent
    # 2. Forward root=None → _args drops it, passes; but if forwarded as "" → present → RED
