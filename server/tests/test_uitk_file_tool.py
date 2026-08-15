"""uitk_file tool unit tests — command forwarding."""
from unittest.mock import AsyncMock


async def test_uitk_file_sends_action(mock_bridge):
    """action="read" forwarded as cmd="uitk_file", args["action"]="read".
    Breaks if action is not forwarded or the command name changes.
    """
    from unity_mcp.server import uitk_file

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await uitk_file(path="Assets/UI/HUD.uxml", action="read")
    cmd = mock_bridge.send.call_args[0][0]
    args = mock_bridge.send.call_args[0][1]
    assert cmd == "uitk_file"
    assert args["action"] == "read"
    # Double-red:
    # 1. Change to args["action"] == "write" → fails
    # 2. Delete uitk_file → ImportError → RED


async def test_uitk_file_path_forwarded(mock_bridge):
    """path="Assets/UI/HUD.uxml" → args["path"] == "Assets/UI/HUD.uxml".
    Breaks if path param is dropped or renamed.
    """
    from unity_mcp.server import uitk_file

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await uitk_file(path="Assets/UI/HUD.uxml")
    args = mock_bridge.send.call_args[0][1]
    assert args["path"] == "Assets/UI/HUD.uxml"
    # Double-red:
    # 1. Change to args["path"] == "/Wrong" → fails
    # 2. Remove path forwarding → KeyError → RED


async def test_uitk_file_content_forwarded(mock_bridge):
    """content="<ui:UXML/>" → args["content"] == "<ui:UXML/>".
    Breaks if content is not forwarded when provided.
    """
    from unity_mcp.server import uitk_file

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await uitk_file(path="Assets/UI/HUD.uxml", action="write", content="<ui:UXML/>")
    args = mock_bridge.send.call_args[0][1]
    assert args["content"] == "<ui:UXML/>"
    # Double-red:
    # 1. Change to args["content"] == "other" → fails
    # 2. Remove content from _args() → KeyError → RED
