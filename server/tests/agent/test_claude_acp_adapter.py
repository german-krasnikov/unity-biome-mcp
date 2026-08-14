"""Tests for ClaudeAcpAdapter — mocked CliSession, no Unity. (10 tests)"""
from __future__ import annotations

import json
import os
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from relay_helpers import mock_sess

from unity_mcp.adapters.claude_acp import ClaudeAcpAdapter, _build_claude_acp_argv
from unity_mcp.agent_event import ProviderCapabilities
from unity_mcp.backend_def import BACKENDS
from unity_mcp.cli_session import SessionMeta
from unity_mcp.permission_broker import PermissionBroker


def _meta(
    *,
    model: str | None = None,
    session_id: str | None = None,
    prompt: str = "hello",
) -> SessionMeta:
    return SessionMeta(
        backend="claude_acp", mode="ask", model=model, mcp_port=9500,
        prompt=prompt, config_dir=None,
        internal_session_id=session_id,
    )


def _perm_sess(ndjson_line: str, exit_code: int = 0) -> MagicMock:
    s = mock_sess(exit_code=exit_code)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="")
    s.read_stdout_line = AsyncMock(side_effect=[ndjson_line, None])
    s._binary = "claude-agent-acp"
    return s


async def _collect(adapter: ClaudeAcpAdapter) -> list:
    return [e async for e in adapter.events()]


# ── Pure function: _build_claude_acp_argv ─────────────────────────────────────

@pytest.fixture
def mock_write_config():
    with patch(
        "unity_mcp.mcp_config_writer.write_claude_config",
        return_value="/tmp/claude_mcp_config.json",
    ):
        yield


def test_build_argv_contains_mcp_config_flag(mock_write_config) -> None:
    argv = _build_claude_acp_argv(_meta())
    assert "--mcp-config" in argv  # TODO: update if flag name differs


def test_build_argv_model_flag(mock_write_config) -> None:
    argv = _build_claude_acp_argv(_meta(model="claude-opus-4-5"))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "claude-opus-4-5"


def test_build_argv_resume_includes_session_id(mock_write_config) -> None:
    argv = _build_claude_acp_argv(_meta(session_id="sess-abc"))
    assert "--resume" in argv
    assert argv[argv.index("--resume") + 1] == "sess-abc"


def test_build_argv_prompt_appended_last(mock_write_config) -> None:
    argv = _build_claude_acp_argv(_meta(prompt="do the thing"))
    assert argv[-1] == "do the thing"


def test_build_argv_no_resume_when_session_id_absent(mock_write_config) -> None:
    argv = _build_claude_acp_argv(_meta(session_id=None))
    assert "--resume" not in argv


# ── Adapter: probe ────────────────────────────────────────────────────────────

async def test_probe_returns_capabilities() -> None:
    backend = BACKENDS["claude_acp"]
    adapter = ClaudeAcpAdapter(backend, PermissionBroker(mode="ask"))
    with patch.object(backend, "probe_capabilities", AsyncMock(return_value={
        "has_resume": True, "has_cancel": False,
        "has_modes": ["ask", "agent"], "binary_version": "0.1.0",
    })):
        caps = await adapter.probe()
    assert isinstance(caps, ProviderCapabilities)
    assert caps.transport == "stdio"


# ── Adapter: permission deny ──────────────────────────────────────────────────

async def test_permission_denied_writes_to_stdin() -> None:
    """ask mode + write tool → outcome=deny written to stdin."""
    perm_line = json.dumps({
        "type": "session/request_permission",
        "tool_name": "execute_code",
        "request_id": "req-deny",
        "input": {},
    })
    adapter = ClaudeAcpAdapter(BACKENDS["claude_acp"], PermissionBroker(mode="ask"))
    adapter._session = _perm_sess(perm_line)

    await _collect(adapter)

    calls = adapter._session.write_line.call_args_list
    assert len(calls) == 1
    payload = json.loads(calls[0].args[0])
    assert payload["outcome"] == "deny"
    assert payload["request_id"] == "req-deny"


# ── Adapter: permission allow ─────────────────────────────────────────────────

async def test_permission_allowed_writes_to_stdin() -> None:
    """full-access mode → outcome=allow written to stdin."""
    perm_line = json.dumps({
        "type": "session/request_permission",
        "tool_name": "execute_code",
        "request_id": "req-allow",
        "input": {},
    })
    adapter = ClaudeAcpAdapter(BACKENDS["claude_acp"], PermissionBroker(mode="full-access"))
    adapter._session = _perm_sess(perm_line)

    await _collect(adapter)

    calls = adapter._session.write_line.call_args_list
    assert len(calls) == 1
    payload = json.loads(calls[0].args[0])
    assert payload["outcome"] == "allow"
    assert payload["request_id"] == "req-allow"


# ── Adapter: nonzero exit ────────────────────────────────────────────────────

async def test_nonzero_exit_emits_error_event() -> None:
    adapter = ClaudeAcpAdapter(BACKENDS["claude_acp"], PermissionBroker(mode="ask"))
    s = mock_sess(exit_code=1)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="segfault")
    s.read_stdout_line = AsyncMock(side_effect=[None])
    s._binary = "claude-agent-acp"
    adapter._session = s
    evts = await _collect(adapter)
    assert len(evts) == 1
    assert evts[0].kind == "error"
    assert "exited 1" in evts[0].payload["message"]


# ── Factory: always ACP ───────────────────────────────────────────────────────

def test_make_claude_adapter_returns_acp() -> None:
    from unity_mcp.adapters import make_claude_adapter
    adapter = make_claude_adapter(BACKENDS["claude"], PermissionBroker(mode="ask"))
    assert isinstance(adapter, ClaudeAcpAdapter)
