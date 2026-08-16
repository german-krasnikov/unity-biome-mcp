"""Argument-aware safety tests for the Python-only doctor tool."""

from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools import meta


@pytest.mark.asyncio
async def test_doctor_default_remains_available_in_read_only_mode(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    with (
        patch.object(meta, "run_doctor", AsyncMock(return_value=[])) as run,
        patch.object(meta, "format_report", return_value="ok"),
    ):
        assert await meta.doctor() == "ok"
    run.assert_awaited_once_with(fix=False)


@pytest.mark.asyncio
async def test_doctor_fix_is_blocked_before_file_cleanup_in_read_only_mode(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    with patch.object(meta, "run_doctor", AsyncMock()) as run:
        with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
            await meta.doctor(fix=True)
    run.assert_not_awaited()
