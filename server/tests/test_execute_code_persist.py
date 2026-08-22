"""Tests for execute_code persist_as and clear_held_types (Phase C Subtask 16)."""
from unittest.mock import AsyncMock, patch
import pytest

from unity_mcp.tools.codegen import clear_held_types, execute_code


@pytest.fixture(autouse=True)
def bind_send():
    """Bind _send and _args to test doubles, restoring originals after each test."""
    import unity_mcp.tools.codegen as _mod
    orig_send, orig_args = _mod._send, _mod._args
    send = AsyncMock(return_value={"ok": True, "data": "ok"})
    _mod._send = send
    _mod._args = lambda **kw: {k: v for k, v in kw.items()}
    yield send
    _mod._send = orig_send
    _mod._args = orig_args


async def test_persist_as_sends_arg(bind_send):
    await execute_code(code="class T {}", persist_as="T")
    call = bind_send.call_args
    assert call[0][0] == "execute_code"
    assert call[0][1]["persist_as"] == "T"


async def test_no_persist_as_omits_key(bind_send):
    await execute_code(code="return 1;")
    call = bind_send.call_args
    assert "persist_as" not in call[0][1]


async def test_clear_held_types_sends_command(bind_send):
    await clear_held_types()
    call = bind_send.call_args
    assert call[0][0] == "clear_held_types"
    assert call[0][1] == {}
