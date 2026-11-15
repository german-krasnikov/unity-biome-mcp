"""TDD tests for new object tools — Q8 (set_sibling_index), S4 (compress param)."""
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


# ── S4: compress parameter for get_component and inspect ──────────────────────

async def test_get_component_compress_param_forwarded(mock_bridge):
    """compress=True adds compress='true' to args sent to bridge."""
    from unity_mcp.tools.objects import get_component
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "result"})
    await get_component(path="/Player", type="Rigidbody", compress=True)
    args = mock_bridge.send.call_args[0][1]
    assert args.get("compress") == "true"


async def test_get_component_no_compress_by_default(mock_bridge):
    """compress not set by default — no extra arg sent."""
    from unity_mcp.tools.objects import get_component
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "result"})
    await get_component(path="/Player", type="Rigidbody")
    args = mock_bridge.send.call_args[0][1]
    assert "compress" not in args


async def test_inspect_compress_param_forwarded(mock_bridge):
    """compress=True adds compress='true' to args sent to bridge."""
    from unity_mcp.tools.objects import inspect
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "result"})
    await inspect(paths="/Player,/Enemy", compress=True)
    args = mock_bridge.send.call_args[0][1]
    assert args.get("compress") == "true"


async def test_inspect_no_compress_by_default(mock_bridge):
    """compress not set by default — no extra arg sent."""
    from unity_mcp.tools.objects import inspect
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "result"})
    await inspect(paths="/Player,/Enemy")
    args = mock_bridge.send.call_args[0][1]
    assert "compress" not in args
