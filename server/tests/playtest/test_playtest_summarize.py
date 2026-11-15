"""Tests for playtest result summarization via Haiku (P2).

N0b: compression/summarization is pass-path-only — a failing report now raises
ToolError before ever reaching _compress_report/summarize (see
test_playtest_public_parity.py::test_fail_report_raised_whole_not_summarized
for the failing-report evidence contract). Fixtures here are all genuine
passes so the summarization path under test is actually exercised.
"""
from unittest.mock import AsyncMock, MagicMock, patch


async def test_playtest_short_result_not_summarized(mock_bridge):
    """Results under 300 chars are returned as-is, no Haiku call."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 3/3 (0.1s) OK"}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value="summary"))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    mock_svc.summarize.assert_not_called()
    assert "PLAYTEST: 3/3" in result


async def test_playtest_long_result_summarized_when_enabled(mock_bridge):
    """Results over 300 chars get summarized via Haiku when enabled."""
    long_report = "PLAYTEST: 3/3 (2.1s) OK\n" + "[detail] " * 50 + "\nmore details\n" * 10
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value="3/3 OK: summary"))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    mock_svc.summarize.assert_called_once()
    assert result == "3/3 OK: summary"


async def test_playtest_long_result_kept_when_disabled(mock_bridge):
    """Results over 300 chars kept as-is when SamplingService disabled."""
    long_report = "PLAYTEST: 3/3 (0.1s) OK\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=False, summarize=AsyncMock())
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    mock_svc.summarize.assert_not_called()
    assert long_report in result


async def test_playtest_summarize_fallback_on_none(mock_bridge):
    """If summarize() returns None, return compressed original."""
    long_report = "PLAYTEST: 3/3 (0.1s) OK\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    mock_svc = MagicMock(enabled=True, summarize=AsyncMock(return_value=None))
    with patch("unity_mcp.tools.runtime._sampling", mock_svc):
        result = await run_playtest("LOG hi")

    assert long_report in result
