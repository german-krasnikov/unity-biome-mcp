"""Tests for write_session tools (Phase D Subtask 20)."""
from unittest.mock import AsyncMock, patch
import pytest

import unity_mcp.tools.write_session as _mod
from unity_mcp.tools.tool_specs import _SPECS


@pytest.fixture(autouse=True)
def bind_send():
    orig_send, orig_args = _mod._send, _mod._args
    send = AsyncMock(return_value="write_session_started")
    _mod._send = send
    _mod._args = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    yield send
    _mod._send = orig_send
    _mod._args = orig_args


async def test_start_sends_correct_command(bind_send):
    await _mod.start_write_session()
    bind_send.assert_called_once_with("start_write_session", {})


async def test_end_sync_true_calls_await_compile(bind_send):
    bind_send.return_value = "write_session_ended refresh=triggered"
    with patch("unity_mcp.tools.write_session._await_compile_fn",
               new=AsyncMock(return_value="compile clean")) as mock_compile:
        result = await _mod.end_write_session(sync=True)
    mock_compile.assert_called_once()
    assert "write_session_ended" in result
    assert "compile clean" in result


async def test_end_sync_false_skips_await_compile(bind_send):
    bind_send.return_value = "write_session_ended refresh=triggered"
    with patch("unity_mcp.tools.write_session._await_compile_fn",
               new=AsyncMock()) as mock_compile:
        await _mod.end_write_session(sync=False)
    mock_compile.assert_not_called()


async def test_end_error_skips_await_compile(bind_send):
    bind_send.return_value = "err=not_active"
    with patch("unity_mcp.tools.write_session._await_compile_fn",
               new=AsyncMock()) as mock_compile:
        result = await _mod.end_write_session(sync=True)
    mock_compile.assert_not_called()
    assert "err=not_active" in result


def test_start_write_session_in_tool_specs():
    assert "start_write_session" in _SPECS


def test_end_write_session_in_tool_specs():
    assert "end_write_session" in _SPECS
