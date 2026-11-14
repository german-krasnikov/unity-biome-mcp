import pytest
from unittest.mock import AsyncMock
from mcp.server.mcpserver.exceptions import ToolError

from unity_mcp.server import inspect


async def test_inspect_single_path(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "--- /Player ---\n[Transform]\npos: (0,0,0)"})
    result = await inspect(paths="/Player")
    mock_bridge.send.assert_called_once_with("inspect", {"paths": "/Player"}, timeout=30.0)
    assert "--- /Player ---" in result


async def test_inspect_multiple_paths(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "--- /A ---\ndata\n--- /B ---\ndata"})
    result = await inspect(paths="/A,/B")
    mock_bridge.send.assert_called_once_with("inspect", {"paths": "/A,/B"}, timeout=30.0)
    assert "--- /A ---" in result
    assert "--- /B ---" in result


async def test_inspect_with_components_filter(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "--- /Player ---\n[Transform]\npos: (0,0,0)"})
    await inspect(paths="/Player", components="Transform,Rigidbody")
    args = mock_bridge.send.call_args[0][1]
    assert args["paths"] == "/Player"
    assert args["components"] == "Transform,Rigidbody"


async def test_inspect_no_components_omitted(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "all components"})
    await inspect(paths="/X")
    args = mock_bridge.send.call_args[0][1]
    assert args["paths"] == "/X"
    assert "components" not in args


async def test_inspect_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "paths is required"})
    with pytest.raises(ToolError, match="paths is required"):
        await inspect(paths="")


async def test_inspect_unity_not_connected(mock_bridge):
    from unity_mcp import server
    original = server.slot
    server.slot = None
    try:
        with pytest.raises(ToolError, match="Server not initialized"):
            await inspect(paths="/Player")
    finally:
        server.slot = original


async def test_inspect_object_not_found_partial(mock_bridge):
    """When some objects exist and some don't, Unity returns mixed results."""
    data = "--- /Player ---\n[Transform]\npos: (0,0,0)\n--- /Missing ---\nObject not found: /Missing"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": data})
    result = await inspect(paths="/Player,/Missing")
    assert "/Player" in result
    assert "/Missing" in result


async def test_inspect_file_output(mock_bridge):
    """Large inspect result returns file path."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "file": "/tmp/MCP/data.txt"})
    result = await inspect(paths="/A,/B,/C")
    assert result == "Data saved to: /tmp/MCP/data.txt"
