"""UI Toolkit tool tests: inspect_uitk, lint_uitk command forwarding."""
from unittest.mock import AsyncMock

from unity_mcp.server import inspect_uitk, lint_uitk


async def test_inspect_uitk_sends_correct_cmd(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk()
    cmd = mock_bridge.send.call_args[0][0]
    assert cmd == "inspect_uitk"
    # Double-red:
    # 1. Change to cmd == "wrong_cmd" → fails
    # 2. Delete inspect_uitk → ImportError → RED


async def test_inspect_uitk_path_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk(path="/Player/HUD")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/Player/HUD"
    # Double-red:
    # 1. Change to args["path"] == "/Wrong" → fails
    # 2. Remove path forwarding → key absent → KeyError → RED


async def test_inspect_uitk_path_none_absent(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk()
    args = mock_bridge.send.call_args[0][1]
    assert "path" not in args
    # Double-red:
    # 1. Change to assert "path" in args → fails when absent
    # 2. Forward path=None explicitly → _args drops it; if forwarded as "" → present → RED


async def test_lint_uitk_sends_correct_cmd(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk()
    cmd = mock_bridge.send.call_args[0][0]
    assert cmd == "lint_uitk"
    # Double-red:
    # 1. Change to cmd == "wrong_cmd" → fails
    # 2. Delete lint_uitk → ImportError → RED


async def test_lint_uitk_root_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk(root="/Canvas")
    args = mock_bridge.send.call_args[0][1]
    assert args["root"] == "/Canvas"
    # Double-red:
    # 1. Change to args["root"] == "/Wrong" → fails
    # 2. Remove root forwarding → key absent → KeyError → RED


async def test_lint_uitk_root_none_absent(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk()
    args = mock_bridge.send.call_args[0][1]
    assert "root" not in args
    # Double-red:
    # 1. Change to assert "root" in args → fails when absent
    # 2. Forward root=None → _args drops it; if forwarded as "" → present → RED
