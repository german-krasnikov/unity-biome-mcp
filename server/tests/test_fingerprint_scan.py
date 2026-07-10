"""Tests for fingerprint and scan_scene tools."""
from unittest.mock import AsyncMock
from unity_mcp.server import fingerprint, scan_scene


async def test_fingerprint_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "fp:ABCD1234"}
    result = await fingerprint()
    mock_bridge.send.assert_called_once_with("fingerprint", {"depth": 3}, timeout=10.0)
    assert result == "fp:ABCD1234"


async def test_fingerprint_default_depth(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "fp:00000000"}
    await fingerprint()
    call_args = mock_bridge.send.call_args[0][1]
    assert call_args["depth"] == 3


async def test_fingerprint_custom_path_and_depth(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "fp:12345678"}
    await fingerprint(path="Player", depth=5)
    mock_bridge.send.assert_called_once_with(
        "fingerprint", {"path": "Player", "depth": 5}, timeout=10.0
    )


async def test_scan_scene_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "SCAN: 10 objects\n  colliders: 2 (20%)"}
    result = await scan_scene()
    mock_bridge.send.assert_called_once_with("scan_scene", {}, timeout=30.0)
    assert "SCAN" in result
