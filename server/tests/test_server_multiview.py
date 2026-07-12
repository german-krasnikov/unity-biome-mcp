from unittest.mock import AsyncMock, patch


async def test_screenshot_multi_view_sends_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "file": "/tmp/MCP/multiview.png"})
    from unity_mcp.tools.screenshot import screenshot
    await screenshot(camera="multi_view", path="Player", width=320)
    call_args = mock_bridge.send.call_args
    args = call_args[0][1]
    assert args["camera"] == "multi_view"
    assert args["path"] == "Player"
    assert args["width"] == 320
