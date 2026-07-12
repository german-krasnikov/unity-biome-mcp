"""Tests for playtest result summarization via Haiku (P2)."""
from unittest.mock import AsyncMock, patch, MagicMock


async def test_playtest_short_result_not_summarized(mock_bridge):
    """Results under 300 chars are returned as-is, no Haiku call."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 3/3"}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value="summary"))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    mock_svc.summarize.assert_not_called()
    assert "PASS: 3/3" in result


async def test_playtest_long_result_summarized_when_enabled(mock_bridge):
    """Results over 300 chars get summarized via Haiku when enabled."""
    long_report = "PLAYTEST: 1/3 (2.1s)\n" + "[FAIL] " * 50 + "\nmore details\n" * 10
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value="1/3 FAIL: assertion mismatch"))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    assert result == "1/3 FAIL: assertion mismatch"


async def test_playtest_long_result_kept_when_disabled(mock_bridge):
    """Results over 300 chars kept as-is when SamplingService disabled."""
    long_report = "PLAYTEST: 1/3\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=False, summarize=AsyncMock())
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    mock_svc.summarize.assert_not_called()
    assert long_report in result


async def test_playtest_summarize_fallback_on_none(mock_bridge):
    """If summarize() returns None, return compressed original."""
    long_report = "PLAYTEST: 1/3\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value=None))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    assert long_report in result
