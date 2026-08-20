"""Tests for get_console_since overflow/wraparound boundary sentinel (MCP-CONSOLE-032).

Existing tests cover the clean watermark probe and the #MCP_INTERNAL overflow
synthetic line. These tests cover the case where get_console itself returns an
error-prefixed sentinel string (e.g. from a TCP-level buffer overflow), verifying
that get_console_since propagates it without raising.

No live Unity — get_console is mocked at module level.
"""
import time
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mod():
    import unity_mcp.tools.console as m
    return m


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch, mod):
    """Set up _send/_args so console_mark() works without a server."""
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


async def test_get_console_since_returns_overflow_sentinel_on_overflow_response(mod, monkeypatch):
    """When get_console returns an err:overflow sentinel, get_console_since propagates it.

    The caller must receive the typed sentinel string starting with 'err: overflow:'
    so it can distinguish overflow from clean results.
    """
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "err: overflow:5 logs dropped since mark"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)
    assert result.startswith("err: overflow:")


async def test_get_console_since_overflow_does_not_raise(mod, monkeypatch):
    """An overflow sentinel from get_console must never raise — caller gets a string.

    Ensures the retry/reporting layer receives a typed string, not an exception,
    even when the console ring buffer has wrapped around.
    """
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "err: overflow:5 logs dropped since mark"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)
    assert isinstance(result, str)
