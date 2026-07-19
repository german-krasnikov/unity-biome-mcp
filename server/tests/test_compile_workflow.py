"""Tests for compile workflow fixes (Wave 2).

Covers:
  MCP091-002: compile_preflight empty-param validation
  MCP091-009: _attempt_recovery heals on no-IL-change compile
"""
import asyncio
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.code_intel as _ci
import unity_mcp.tools.sync as _sync


@pytest.fixture(autouse=True)
def _reset_ci_send():
    original = _ci._send
    yield
    _ci._send = original


# ---------------------------------------------------------------------------
# MCP091-002: compile_preflight input validation
# ---------------------------------------------------------------------------

async def test_compile_preflight_rejects_empty_file_path():
    with pytest.raises(ToolError, match="file_path is required"):
        await _ci.compile_preflight("", "public class Foo {}")


async def test_compile_preflight_rejects_whitespace_file_path():
    with pytest.raises(ToolError, match="file_path is required"):
        await _ci.compile_preflight("   ", "public class Foo {}")


async def test_compile_preflight_rejects_empty_new_content():
    with pytest.raises(ToolError, match="new_content is required"):
        await _ci.compile_preflight("Assets/Scripts/Foo.cs", "")


async def test_compile_preflight_rejects_whitespace_new_content():
    with pytest.raises(ToolError, match="new_content is required"):
        await _ci.compile_preflight("Assets/Scripts/Foo.cs", "   ")


async def test_compile_preflight_passes_valid_args():
    """Valid args reach _send (not rejected by validation)."""
    sent = {}

    async def fake_send(cmd, args=None, **kwargs):
        sent["cmd"] = cmd
        sent["args"] = args
        return "OK preflight (123ms)"

    _ci._send = fake_send
    result = await _ci.compile_preflight("Assets/Foo.cs", "public class Foo {}")
    assert sent["cmd"] == "compile_preflight"
    assert sent["args"]["file_path"] == "Assets/Foo.cs"
    assert "OK" in result


# ---------------------------------------------------------------------------
# MCP091-009: _attempt_recovery — heals vs. reports based on error presence
# ---------------------------------------------------------------------------

async def test_attempt_recovery_heals_when_no_errors(monkeypatch):
    """MCP091-009: MVID stable + compile state=ready + no errors → return None (healed)."""
    mvid = "aaaa-bbbb-cccc"
    stamp = f"{mvid}:100"

    async def send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "ok"
        if cmd == "sync_status":
            return f"epoch=1|state=ready|stamp={stamp}"
        return ""

    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", -1.0)

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.get_corroborated_errors = AsyncMock(return_value="")
        result = await _sync._attempt_recovery(send, mvid)

    assert result is None, f"Expected None (healed), got: {result!r}"


async def test_attempt_recovery_reports_reimport_when_errors(monkeypatch):
    """MCP091-009: MVID stable + compile state=ready + actual errors → REIMPORT-NEEDED."""
    mvid = "aaaa-bbbb-cccc"
    stamp = f"{mvid}:100"

    async def send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "ok"
        if cmd == "sync_status":
            return f"epoch=1|state=ready|stamp={stamp}"
        return ""

    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", -1.0)

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.get_corroborated_errors = AsyncMock(return_value="CS0246: type not found")
        result = await _sync._attempt_recovery(send, mvid)

    assert result is not None
    assert "REIMPORT-NEEDED" in result


async def test_attempt_recovery_reimport_when_compile_stuck(monkeypatch):
    """MCP091-009: MVID stable + compile state=compiling (stuck) → REIMPORT-NEEDED."""
    mvid = "aaaa-bbbb-cccc"

    async def send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "ok"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"  # stuck
        return ""

    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", -1.0)

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.get_corroborated_errors = AsyncMock(return_value="")
        result = await _sync._attempt_recovery(send, mvid)

    assert result is not None
    assert "REIMPORT-NEEDED" in result
