import pytest
from unittest.mock import AsyncMock
from mcp.server.mcpserver.exceptions import ToolError

from unity_mcp.server import references


async def test_references_get_default(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[Script] target: /Path #123"})
    await references(action="get", path="/Player")
    mock_bridge.send.assert_called_once_with("references", {"action": "get", "path": "/Player"}, timeout=30.0)


async def test_references_get_children_true_passes_flag(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "refs"})
    await references(action="get", path="/Player", children=True)
    mock_bridge.send.assert_called_once_with("references", {"action": "get", "path": "/Player", "children": "true"}, timeout=30.0)


async def test_references_get_children_false_omits_key(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "refs"})
    await references(action="get", path="/Player", children=False)
    args = mock_bridge.send.call_args[0][1]
    assert "children" not in args


async def test_references_get_depth_nondefault_passes_depth(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "refs"})
    await references(action="get", path="/Player", depth=2)
    mock_bridge.send.assert_called_once_with("references", {"action": "get", "path": "/Player", "depth": 2}, timeout=30.0)


async def test_references_get_depth_default_omits_key(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "refs"})
    await references(action="get", path="/Player", depth=1)
    args = mock_bridge.send.call_args[0][1]
    assert "depth" not in args


async def test_references_get_returns_data_on_success(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[Script] target: /Path #123 child"})
    result = await references(action="get", path="/Player")
    assert result == "[Script] target: /Path #123 child"


async def test_references_get_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "not found"})
    with pytest.raises(ToolError, match="not found"):
        await references(action="get", path="/Missing")


async def test_references_find_to_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/Enemy #456"})
    await references(action="find_to", path="/Player")
    mock_bridge.send.assert_called_once_with("references", {"action": "find_to", "path": "/Player"}, timeout=30.0)


async def test_references_find_to_returns_data(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/Enemy #456"})
    result = await references(action="find_to", path="/Player")
    assert result == "/Enemy #456"


async def test_references_find_to_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "object not found"})
    with pytest.raises(ToolError, match="object not found"):
        await references(action="find_to", path="/Missing")


async def test_references_remap_calls_bridge_with_source_and_target(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "remapped 3"})
    await references(action="remap", path="/OldPlayer", source="/OldPlayer", target="/NewPlayer")
    mock_bridge.send.assert_called_once_with("references", {"action": "remap", "path": "/OldPlayer", "source": "/OldPlayer", "target": "/NewPlayer"}, timeout=30.0)


async def test_references_remap_with_mappings_passes_mappings(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "remapped 2"})
    await references(action="remap", path="/Old", source="/Old", target="/New", mappings="a=b\nc=d")
    mock_bridge.send.assert_called_once_with("references", {"action": "remap", "path": "/Old", "source": "/Old", "target": "/New", "mappings": "a=b\nc=d"}, timeout=30.0)


async def test_references_remap_without_mappings_omits_key(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "remapped 1"})
    await references(action="remap", path="/Old", source="/Old", target="/New")
    args = mock_bridge.send.call_args[0][1]
    assert "mappings" not in args


async def test_references_remap_returns_data_on_success(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "remapped 5"})
    result = await references(action="remap", path="/Old", source="/Old", target="/New")
    assert result == "remapped 5"


async def test_references_remap_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "source not found"})
    with pytest.raises(ToolError, match="source not found"):
        await references(action="remap", path="/Missing", source="/Missing", target="/New")
