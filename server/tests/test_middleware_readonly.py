"""P-324: Read-only endpoint must block mutation commands.

Python-side enforcement via:
  1. Middleware.check_read_only() — guard in wrap_send pipeline
  2. _send_raw() gate — UNITY_MCP_READ_ONLY=1 env var raises ToolError
"""
import os
import pytest
from unittest.mock import AsyncMock, Mock, patch
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.middleware import Middleware
from unity_mcp.middleware_pipeline import wrap_send
from unity_mcp.server import _send_raw


# ── Unit tests: check_read_only method ──────────────────────────────────────

def test_check_read_only_blocks_write_cmd():
    """is_read_only=True must return a READ_ONLY_BLOCKED message for write cmds."""
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("set_property", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


def test_check_read_only_passes_read_cmd():
    """is_read_only=True must return None for read commands."""
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("get_hierarchy", {})
    assert result is None


def test_check_read_only_returns_none_when_off():
    """Default (is_read_only=False) must never block any command."""
    mw = Middleware()
    mw.is_read_only = False
    result = mw.check_read_only("set_property", {})
    assert result is None


def test_check_read_only_blocks_create_object():
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("create_object", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


def test_check_read_only_blocks_delete_object():
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("delete_object", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


def test_check_read_only_blocks_batch():
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("batch", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


def test_check_read_only_blocks_execute_code():
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("execute_code", {})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


# ── Middleware init: UNITY_MCP_READ_ONLY env var sets is_read_only ────────────

def test_middleware_reads_env_var():
    """Middleware.__init__ must set is_read_only=True when UNITY_MCP_READ_ONLY=1."""
    with patch.dict(os.environ, {"UNITY_MCP_READ_ONLY": "1"}):
        mw = Middleware()
    assert mw.is_read_only is True


def test_middleware_default_is_read_write():
    """Middleware.__init__ defaults to is_read_only=False."""
    env = {k: v for k, v in os.environ.items() if k != "UNITY_MCP_READ_ONLY"}
    with patch.dict(os.environ, env, clear=True):
        mw = Middleware()
    assert mw.is_read_only is False


# ── Pipeline integration: wrap_send blocks writes in read-only mode ──────────

async def test_pipeline_blocks_write_in_readonly_mode():
    """wrap_send must raise ToolError for write commands when is_read_only=True."""
    send_called = []

    async def mock_send(cmd, args, timeout=0):
        send_called.append(cmd)
        return "ok"

    mw = Middleware()
    mw.is_read_only = True
    wrapped = wrap_send(mock_send, mw)

    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await wrapped("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    assert send_called == [], "send_fn must not be called when read-only guard fires"


async def test_pipeline_passes_read_in_readonly_mode():
    """wrap_send must allow read commands even when is_read_only=True."""
    send_called = []

    async def mock_send(cmd, args, timeout=0):
        send_called.append(cmd)
        return "hierarchy output"

    mw = Middleware()
    mw.is_read_only = True
    wrapped = wrap_send(mock_send, mw)

    result = await wrapped("get_hierarchy", {})

    assert "hierarchy output" in result
    assert "get_hierarchy" in send_called


async def test_pipeline_allows_write_when_readonly_off():
    """Default mode (is_read_only=False) must allow writes through."""
    send_called = []

    async def mock_send(cmd, args, timeout=0):
        send_called.append(cmd)
        return "ok"

    mw = Middleware()
    assert mw.is_read_only is False
    wrapped = wrap_send(mock_send, mw)

    await wrapped("create_object", {"name": "Cube"})

    assert "create_object" in send_called


# ── _send_raw gate: env var UNITY_MCP_READ_ONLY=1 ───────────────────────────

def _make_slot(bridge):
    s = Mock()
    s.bridge = bridge
    return s


def _make_bridge():
    b = Mock()
    b.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    return b


async def test_send_raw_blocks_write_in_readonly_env():
    """UNITY_MCP_READ_ONLY=1 must make _send_raw raise ToolError for write cmds."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with patch.dict(os.environ, {"UNITY_MCP_READ_ONLY": "1"}):
            with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
                await _send_raw("set_property", {})
    bridge.send.assert_not_called()


async def test_send_raw_passes_read_in_readonly_env():
    """UNITY_MCP_READ_ONLY=1 must allow read commands through _send_raw."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with patch.dict(os.environ, {"UNITY_MCP_READ_ONLY": "1"}):
            await _send_raw("get_hierarchy", {})
    bridge.send.assert_called_once()


async def test_send_raw_allows_write_without_env():
    """Absent UNITY_MCP_READ_ONLY allows write commands through _send_raw."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    env_without = {k: v for k, v in os.environ.items() if k != "UNITY_MCP_READ_ONLY"}
    with patch("unity_mcp.server.slot", slot):
        with patch.dict(os.environ, env_without, clear=True):
            await _send_raw("set_property", {})
    bridge.send.assert_called_once()


# ── P-420: Action-level read-only classification ─────────────────────────────

def test_check_read_only_allows_read_action_of_write_cmd():
    """P-420: scene_environment(action=get) must not be blocked in read-only mode."""
    mw = Middleware()
    mw.is_read_only = True
    assert mw.check_read_only("scene_environment", {"action": "get"}) is None
    assert mw.check_read_only("bake", {"action": "status"}) is None
    assert mw.check_read_only("bake", {"action": "settings"}) is None
    assert mw.check_read_only("package", {"action": "list"}) is None
    assert mw.check_read_only("package", {"action": "search"}) is None


def test_check_read_only_blocks_write_action_of_mixed_cmd():
    """P-420: scene_environment(action=set) must still be blocked."""
    mw = Middleware()
    mw.is_read_only = True
    result = mw.check_read_only("scene_environment", {"action": "set"})
    assert result is not None
    assert "READ_ONLY_BLOCKED" in result


async def test_pipeline_raises_tool_error_on_readonly_write():
    """P-420: wrap_send must raise ToolError (not return string) for RO writes."""
    async def mock_send(cmd, args, timeout=0):
        return "ok"

    mw = Middleware()
    mw.is_read_only = True
    wrapped = wrap_send(mock_send, mw)

    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await wrapped("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})


async def test_send_raw_allows_read_action_in_readonly_env():
    """P-420: _send_raw must allow scene_environment(action=get) in RO mode."""
    bridge = _make_bridge()
    slot = _make_slot(bridge)
    with patch("unity_mcp.server.slot", slot):
        with patch.dict(os.environ, {"UNITY_MCP_READ_ONLY": "1"}):
            await _send_raw("scene_environment", {"action": "get"})
    bridge.send.assert_called_once()


# ── P-422: Python-local file writes must respect read-only ───────────────────


async def test_save_skill_blocked_in_readonly(tmp_path, monkeypatch):
    """P-422: save_skill must raise ToolError when UNITY_MCP_READ_ONLY=1."""
    monkeypatch.chdir(tmp_path)
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    from unity_mcp.tools.skills import save_skill
    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await save_skill("test", "desc", "code")


async def test_save_template_blocked_in_readonly(tmp_path, monkeypatch):
    """P-422: save_template must raise ToolError when UNITY_MCP_READ_ONLY=1."""
    monkeypatch.chdir(tmp_path)
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    from unity_mcp.tools.skills import save_template
    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await save_template("test", "var x = 1;")


async def test_save_session_blocked_in_readonly(tmp_path, monkeypatch):
    """P-422: save_session must raise ToolError when UNITY_MCP_READ_ONLY=1."""
    monkeypatch.chdir(tmp_path)
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    from unity_mcp.tools.scene import save_session
    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await save_session()
