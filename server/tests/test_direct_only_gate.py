"""P-011: direct_only tools must not be dispatched over TCP to Unity."""
import pytest
from unittest.mock import AsyncMock, Mock, patch
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import _send_raw


def _make_slot(bridge):
    slot = Mock()
    slot.bridge = bridge
    return slot


def _make_bridge():
    b = Mock()
    b.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    return b


async def test_direct_only_cmd_raises_tool_error():
    """discover_tools is direct_only — must raise ToolError before any bridge call."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with pytest.raises(ToolError, match="direct_only|Python-only|control-plane"):
            await _send_raw("discover_tools", {})
    bridge.send.assert_not_called()


async def test_non_direct_only_cmd_passes_through():
    """get_hierarchy has no direct_only — must reach the bridge."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        await _send_raw("get_hierarchy", {})
    bridge.send.assert_called_once()


async def test_batch_passes_through():
    """batch is not direct_only — must reach the bridge."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        await _send_raw("batch", {})
    bridge.send.assert_called_once()


async def test_console_mark_raises_tool_error():
    """console_mark is direct_only — must be blocked."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with pytest.raises(ToolError):
            await _send_raw("console_mark", {})
    bridge.send.assert_not_called()


async def test_mcp_status_raises_tool_error():
    """mcp_status is direct_only — must be blocked."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with pytest.raises(ToolError):
            await _send_raw("mcp_status", {})
    bridge.send.assert_not_called()


async def test_unknown_cmd_passes_through():
    """Unknown commands are not in _SPECS — fail-open, let bridge handle them."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        await _send_raw("totally_unknown_command", {})
    bridge.send.assert_called_once()
