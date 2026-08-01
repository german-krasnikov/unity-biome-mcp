"""Tests for set_parent tool."""
import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.tools import objects


async def test_set_parent_sends_correct_command(mock_bridge, bridge_response):
    """set_parent sends cmd=set_parent with path, parent; omits world_position_stays when default True (Pattern A')."""
    bridge_response(data="ok")
    from unity_mcp.tools.objects import set_parent
    await set_parent(path="/A", parent="/B")
    args = mock_bridge.send.call_args[0][1]
    assert mock_bridge.send.call_args[0][0] == "set_parent"
    assert args["path"] == "/A"
    assert args["parent"] == "/B"
    assert "world_position_stays" not in args


async def test_set_parent_null_parent(mock_bridge, bridge_response):
    """set_parent with parent=None does not send parent key."""
    bridge_response(data="ok")
    from unity_mcp.tools.objects import set_parent
    await set_parent(path="/A", parent=None)
    args = mock_bridge.send.call_args[0][1]
    assert "parent" not in args


async def test_set_parent_world_position_stays_false(mock_bridge, bridge_response):
    """world_position_stays=False sends 'false' string."""
    bridge_response(data="ok")
    from unity_mcp.tools.objects import set_parent
    await set_parent(path="/A", parent="/B", world_position_stays=False)
    args = mock_bridge.send.call_args[0][1]
    assert args["world_position_stays"] == "false"


async def test_delete_object_sends_force_true(mock_bridge, bridge_response):
    bridge_response(data="ok")
    from unity_mcp.tools.objects import delete_object
    await delete_object(path="/A", force=True)
    args = mock_bridge.send.call_args[0][1]
    assert args["force"] == "true"


async def test_delete_object_omits_force_when_false(mock_bridge, bridge_response):
    bridge_response(data="ok")
    from unity_mcp.tools.objects import delete_object
    await delete_object(path="/A", force=False)
    args = mock_bridge.send.call_args[0][1]
    assert "force" not in args


async def test_set_parent_error_raises_tool_error(mock_bridge):
    """set_parent raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    from unity_mcp.tools.objects import set_parent
    with pytest.raises(ToolError, match="Object not found"):
        await set_parent(path="/Missing", parent="/Root")


async def test_delete_object_error_raises_tool_error(mock_bridge):
    """delete_object raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    from unity_mcp.tools.objects import delete_object
    with pytest.raises(ToolError, match="Object not found"):
        await delete_object(path="/Missing")


def test_set_parent_registered_as_rw_idem_tool():
    """set_parent is registered with _RW_IDEM annotation (idempotent write)."""
    import ast, inspect, textwrap
    src = inspect.getsource(objects)
    tree = ast.parse(textwrap.dedent(src))
    found = None
    for node in ast.walk(tree):
        if isinstance(node, ast.FunctionDef) and node.name == "register":
            for stmt in node.body:
                if not isinstance(stmt, ast.Expr):
                    continue
                call = stmt.value
                if not isinstance(call, ast.Call):
                    continue
                if not isinstance(call.func, ast.Call):
                    continue
                inner = call.func
                if not call.args or not isinstance(call.args[0], ast.Name):
                    continue
                if call.args[0].id != "set_parent":
                    continue
                for kw in inner.keywords:
                    if kw.arg == "annotations" and isinstance(kw.value, ast.Name):
                        found = kw.value.id
    assert found == "_RW_IDEM", f"Expected _RW_IDEM annotation, got {found}"
