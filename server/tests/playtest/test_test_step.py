"""Tests for test_step composite command."""
from unity_mcp.tools.runtime import test_step


async def test_test_step_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "BEFORE:\nAFTER:\nCONSOLE: ok"}
    result = await test_step("/Player", "5,0,-3")
    call = mock_bridge.send.call_args
    assert call[0][0] == "test_step"
    sent = call[0][1]
    assert sent["path"] == "/Player"
    assert sent["position"] == "5,0,-3"


async def test_test_step_timeout_calculation(mock_bridge):
    """Python timeout = Unity timeout + 10."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await test_step("/Player", "0,0,0", timeout=15.0)
    call = mock_bridge.send.call_args
    sent = call[0][1]
    assert sent["timeout"] == "15.0"
    assert call[1]["timeout"] == 25.0  # 15 + 10


async def test_test_step_optional_checks(mock_bridge):
    """checks_before/after are omitted when empty."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await test_step("/Player", "0,0,0")
    sent = mock_bridge.send.call_args[0][1]
    assert "checks_before" not in sent
    assert "checks_after" not in sent


async def test_test_step_with_checks(mock_bridge):
    """checks_before/after are included when provided."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await test_step("/Player", "5,0,5",
                    checks_before="/Player|Health|hp",
                    checks_after="/Player|Health|hp")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["checks_before"] == "/Player|Health|hp"
    assert sent["checks_after"] == "/Player|Health|hp"


async def test_test_step_wait_after(mock_bridge):
    """wait_after is sent as string."""
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await test_step("/Player", "0,0,0", wait_after=1.5)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["wait_after"] == "1.5"
