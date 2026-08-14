"""T21: brief_build MCP tool unit tests (no Unity, no TCP)."""
from __future__ import annotations

from unittest.mock import AsyncMock, patch

import unity_mcp.tools.brief_tool as brief_tool


async def test_brief_build_returns_text_block():
    """Happy path: result starts with [Project Brief]."""
    with patch.object(brief_tool, "_send", AsyncMock(return_value="some content")):
        result = await brief_tool.brief_build()
    assert result.startswith("[Project Brief]")


async def test_brief_build_all_kinds_default():
    """Default kinds queries console, compile_errors, and hierarchy."""
    calls: list[str] = []

    async def recording_send(cmd, args, **kw):
        calls.append(cmd)
        return "some content"

    with patch.object(brief_tool, "_send", recording_send):
        await brief_tool.brief_build()

    assert "get_console" in calls
    assert "get_compile_errors" in calls
    assert "get_hierarchy" in calls


async def test_brief_build_custom_kinds():
    """kinds='console' only invokes the console provider."""
    calls: list[str] = []

    async def recording_send(cmd, args, **kw):
        calls.append(cmd)
        return "some content"

    with patch.object(brief_tool, "_send", recording_send):
        result = await brief_tool.brief_build(kinds="console")

    assert "get_console" in calls
    assert "get_hierarchy" not in calls
    assert "get_compile_errors" not in calls
    assert "[Project Brief]" in result


async def test_brief_build_no_send_returns_disconnected_note():
    """_send=None → returns error text, no exception raised."""
    with patch.object(brief_tool, "_send", None):
        result = await brief_tool.brief_build()
    assert "disconnected" in result.lower()


async def test_brief_build_budget_param_respected():
    """Small budget causes truncation in the output."""
    large_content = "x" * 10000
    with patch.object(brief_tool, "_send", AsyncMock(return_value=large_content)):
        result = await brief_tool.brief_build(budget=50)
    assert "truncated" in result
