"""UI Toolkit tool tests: inspect_uitk, lint_uitk, attach_uitk command forwarding."""
from unittest.mock import AsyncMock

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import attach_uitk, inspect_uitk, lint_uitk


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


async def test_inspect_uitk_show_unity_private_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: 0 elements"})
    await inspect_uitk(path="/HUD", show_unity_private=True)
    args = mock_bridge.send.call_args[0][1]
    assert args["include_internal"] is True  # C# JSON key stays "include_internal"
    # Double-red:
    # 1. Change to args["include_internal"] is False → fails
    # 2. Remove include_internal forwarding in _args() → KeyError → RED


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


async def test_lint_uitk_fix_true_is_explicitly_unsupported(mock_bridge):
    with pytest.raises(ToolError, match="unsupported.*no file was changed"):
        await lint_uitk(path="Assets/UI/HUD.uss", fix=True)
    mock_bridge.send.assert_not_called()


def test_lint_uitk_doc_lists_exact_a1_a6_checks():
    doc = lint_uitk.__doc__ or ""
    for code in ("A1", "A2", "A3", "A4", "A5", "A6"):
        assert code in doc
    assert "CamelCase" not in doc
    assert "auto-remove" not in doc


async def test_attach_uitk_sends_cmd(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD")
    cmd = mock_bridge.send.call_args[0][0]
    assert cmd == "attach_uitk"
    # Double-red:
    # 1. Change to cmd == "wrong_cmd" → fails
    # 2. Delete attach_uitk → ImportError → RED


async def test_attach_uitk_path_in_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "/HUD"
    # Double-red:
    # 1. Change to args["path"] == "/Wrong" → fails
    # 2. Remove path forwarding → key absent → KeyError → RED


async def test_attach_uitk_uxml_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD", uxml="Assets/UI/HUD.uxml")
    args = mock_bridge.send.call_args[0][1]
    assert args["uxml"] == "Assets/UI/HUD.uxml"
    # Double-red:
    # 1. Change to args["uxml"] == "other" → fails
    # 2. Remove uxml from _args() → key absent → KeyError → RED


async def test_attach_uitk_uxml_none_absent(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD")
    args = mock_bridge.send.call_args[0][1]
    assert "uxml" not in args
    # Double-red:
    # 1. Change to assert "uxml" in args → fails when absent
    # 2. Forward uxml=None explicitly → present → RED


def test_attach_uitk_doc_describes_optional_panel_settings_truthfully():
    doc = attach_uitk.__doc__ or ""
    assert "omitted leaves the field unset" in doc
    assert "auto-created" not in doc


async def test_attach_uitk_panel_settings_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD", panel_settings="Assets/UI/PS.asset")
    args = mock_bridge.send.call_args[0][1]
    assert args["panel_settings"] == "Assets/UI/PS.asset"
    # Double-red:
    # 1. Change to args["panel_settings"] == "other" → fails
    # 2. Remove panel_settings forwarding → KeyError → RED


async def test_attach_uitk_sort_order_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok: UIDocument added"})
    await attach_uitk(path="/HUD", sort_order=5)
    args = mock_bridge.send.call_args[0][1]
    assert args["sort_order"] == 5
    # Double-red:
    # 1. Change to args["sort_order"] == 0 → fails
    # 2. Remove sort_order from _args → KeyError → RED
