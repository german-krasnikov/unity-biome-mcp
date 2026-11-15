"""G1: render_mode parameter forwarding for create_ui.

Tests verify:
- render_mode="camera" reaches bridge args
- render_mode absent → key NOT in bridge args (None filtered by _args)
"""
from unittest.mock import AsyncMock

from unity_mcp.server import create_ui


async def test_create_ui_render_mode_param_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Canvas"})
    await create_ui(type="Canvas", render_mode="camera")
    args = mock_bridge.send.call_args[0][1]
    assert args["render_mode"] == "camera"
    # Double-red:
    # 1. Change to args["render_mode"] == "world" → fails (wrong value)
    # 2. Remove render_mode from create_ui signature → TypeError → RED


async def test_create_ui_render_mode_default_none(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Canvas"})
    await create_ui(type="Canvas")
    args = mock_bridge.send.call_args[0][1]
    assert "render_mode" not in args
    # Double-red:
    # 1. Change to assert "render_mode" in args → fails when absent
    # 2. Forward render_mode=None explicitly → _args drops it, assertion passes —
    #    but if always forwarded as empty string → key present → RED
