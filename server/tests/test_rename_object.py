"""TDD tests for rename_object tool."""
from pathlib import Path
import pytest
from mcp.server.fastmcp.exceptions import ToolError


async def test_sends_correct_command(mock_bridge):
    """rename_object sends 'rename_object' command with path and name."""
    mock_bridge.send.return_value = {"ok": True, "data": "/Boss"}
    from unity_mcp.tools.objects import rename_object
    await rename_object("/Grunt", "Boss")
    cmd = mock_bridge.send.call_args[0][0]
    sent = mock_bridge.send.call_args[0][1]
    assert cmd == "rename_object"
    assert sent == {"path": "/Grunt", "name": "Boss"}


async def test_returns_new_path(mock_bridge):
    """rename_object returns the new scene path returned by Unity."""
    mock_bridge.send.return_value = {"ok": True, "data": "/Boss"}
    from unity_mcp.tools.objects import rename_object
    result = await rename_object("/Grunt", "Boss")
    assert result == "/Boss"


async def test_empty_name_forwarded_to_csharp(mock_bridge):
    """Python does not validate empty name — C# validates; empty name is forwarded."""
    mock_bridge.send.return_value = {"ok": True, "data": ""}
    from unity_mcp.tools.objects import rename_object
    await rename_object("/Grunt", "")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["name"] == ""


async def test_bridge_error_raises_tool_error(mock_bridge):
    """Bridge ok=False response raises ToolError."""
    mock_bridge.send.return_value = {"ok": False, "err": "not found"}
    from unity_mcp.tools.objects import rename_object
    with pytest.raises(ToolError):
        await rename_object("/Missing", "Boss")


def test_registered_as_rw_idem():
    """rename_object is registered with _RW_IDEM annotation (idempotent write)."""
    src = (Path(__file__).parent.parent / "src/unity_mcp/tools/objects.py").read_text(encoding="utf-8")
    assert "_RW_IDEM)(rename_object)" in src
