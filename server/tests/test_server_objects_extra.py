"""TDD tests for new object tools — Q8 (set_sibling_index)."""
import pytest
from unittest.mock import AsyncMock


async def test_set_sibling_index_sends_correct_args(mock_bridge):
    """set_sibling_index sends path and index (as string) to bridge."""
    from unity_mcp.tools.objects import set_sibling_index
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await set_sibling_index(path="/Parent/Child", index=2)
    args = mock_bridge.send.call_args[0][1]
    assert args == {"path": "/Parent/Child", "index": "2"}


async def test_set_sibling_index_zero_index(mock_bridge):
    """set_sibling_index with index=0 moves to first child."""
    from unity_mcp.tools.objects import set_sibling_index
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await set_sibling_index(path="/Root/Child", index=0)
    args = mock_bridge.send.call_args[0][1]
    assert args["index"] == "0"
    assert args["path"] == "/Root/Child"
