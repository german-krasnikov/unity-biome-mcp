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


async def test_inspect_uitk_depth_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk(path="/HUD", depth=2)
    args = mock_bridge.send.call_args[0][1]
    assert args["depth"] == 2
    # Double-red:
    # 1. Change to args["depth"] == 99 → fails
    # 2. Remove depth from _args → KeyError → RED


async def test_inspect_uitk_selector_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk(path="/HUD", selector=".btn")
    args = mock_bridge.send.call_args[0][1]
    assert args["selector"] == ".btn"
    # Double-red:
    # 1. Change to args["selector"] == ".other" → fails
    # 2. Remove selector forwarding → KeyError → RED


async def test_inspect_uitk_include_internal_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk(path="/HUD", include_internal=True)
    args = mock_bridge.send.call_args[0][1]
    assert args["include_internal"] is True
    # Double-red:
    # 1. Change to args["include_internal"] is False → fails
    # 2. Remove include_internal forwarding → KeyError → RED


async def test_lint_uitk_sends_correct_cmd(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk()
    cmd = mock_bridge.send.call_args[0][0]
    assert cmd == "lint_uitk"
    # Double-red:
    # 1. Change to cmd == "wrong_cmd" → fails
    # 2. Delete lint_uitk → ImportError → RED


async def test_lint_uitk_path_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk(path="Assets/UI/HUD.uss")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "Assets/UI/HUD.uss"
    # Double-red:
    # 1. Change to args["path"] == "/Wrong" → fails
    # 2. Remove path forwarding → KeyError → RED


async def test_lint_uitk_path_none_absent(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 issues"})
    await lint_uitk()
    args = mock_bridge.send.call_args[0][1]
    assert "path" not in args
    # Double-red:
    # 1. Change to assert "path" in args → fails when absent
    # 2. Forward path=None explicitly → _args drops it → absent → RED
