"""Tests for session functions in scene.py — save_session / load_session.

Zero coverage identified in audit. Covers:
- save_session happy path (calls bridge, writes file, returns path)
- save_session OSError → returns error string
- load_session happy path (reads file, calls bridge for current hierarchy)
- load_session missing file → "No previous session found."
- load_session corrupt text → error string
- load_session register() wires _send
"""
import os
import time
import pytest
from unittest.mock import AsyncMock, MagicMock, patch, mock_open


# ── helpers ───────────────────────────────────────────────────────────────────

@pytest.fixture(autouse=True)
def _restore_scene_globals():
    """Restore scene module globals after each test to avoid cross-test pollution."""
    import unity_mcp.tools.scene as mod
    prev_send = mod._send
    prev_args = mod._args
    yield
    mod._send = prev_send
    mod._args = prev_args


def _wire(send):
    """Point scene._send at `send`."""
    import unity_mcp.tools.scene as mod
    mod._send = send
    mod._args = MagicMock()


# ── save_session ──────────────────────────────────────────────────────────────

async def test_save_session_calls_bridge_and_writes_file(tmp_path):
    send = AsyncMock(return_value="ok")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)):
        result = await _invoke_save_session()

    assert "session-context.json" in result or "session saved" in result.lower()
    written = tmp_path / ".claude" / "session-context.json"
    assert written.exists()
    content = written.read_text(encoding="utf-8")
    assert "=== hierarchy ===" in content
    assert "ok" in content


async def test_save_session_bridge_called_once(tmp_path):
    send = AsyncMock(return_value="data")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)):
        await _invoke_save_session()

    assert send.call_count == 1  # only get_hierarchy; no console, no editor_state


async def test_save_session_oserror_returns_error_string(tmp_path):
    send = AsyncMock(return_value="ok")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)), \
         patch("builtins.open", side_effect=OSError("disk full")):
        result = await _invoke_save_session()

    assert "Failed to save session" in result
    assert "disk full" in result


# ── load_session ──────────────────────────────────────────────────────────────

async def test_load_session_missing_file_returns_no_session(tmp_path):
    send = AsyncMock(return_value="current-hierarchy")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)):
        result = await _invoke_load_session()

    assert "No previous session found" in result
    send.assert_not_called()


async def test_load_session_happy_path(tmp_path):
    ts = time.time()
    session_path = tmp_path / ".claude" / "session-context.json"
    session_path.parent.mkdir(parents=True)
    session_path.write_text(f"{ts}\n=== hierarchy ===\nroot\n  child\n", encoding="utf-8")

    send = AsyncMock(return_value="current-root\n  updated")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)):
        result = await _invoke_load_session()

    assert "Previous" in result
    assert "Current" in result
    assert "root" in result
    assert "current-root" in result
    send.assert_called_once()


async def test_load_session_corrupt_json_returns_error(tmp_path):
    session_path = tmp_path / ".claude" / "session-context.json"
    session_path.parent.mkdir(parents=True)
    session_path.write_text("NOTAFLOAT\n=== hierarchy ===\nhier\n", encoding="utf-8")

    send = AsyncMock(return_value="current")
    _wire(send)

    with patch("os.getcwd", return_value=str(tmp_path)):
        result = await _invoke_load_session()

    assert "corrupt" in result.lower() or "unreadable" in result.lower()
    send.assert_not_called()


# ── register() ────────────────────────────────────────────────────────────────

def test_scene_register_sets_send():
    import unity_mcp.tools.scene as mod
    mod._send = None
    mcp = MagicMock()
    mcp.tool.return_value = lambda fn: fn
    send = AsyncMock()
    args = MagicMock()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args
    assert mcp.tool.call_count >= 2  # save_session + load_session at minimum


# ── internal helpers ──────────────────────────────────────────────────────────

async def _invoke_save_session():
    from unity_mcp.tools.scene import save_session
    return await save_session()


async def _invoke_load_session():
    from unity_mcp.tools.scene import load_session
    return await load_session()
