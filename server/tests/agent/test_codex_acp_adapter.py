"""Tests for CodexAcpAdapter — mocked CliSession, no Unity. (10 tests)"""

import json
import os
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from relay_helpers import mock_sess

from unity_mcp.adapters.codex_acp import CodexAcpAdapter, _build_codex_acp_argv
from unity_mcp.agent_event import ProviderCapabilities
from unity_mcp.backend_def import BACKENDS
from unity_mcp.cli_session import SessionMeta
from unity_mcp.permission_broker import PermissionBroker


def _meta(*, model: str | None = None, session_id: str | None = None) -> SessionMeta:
    return SessionMeta(
        backend="codex", mode="ask", model=model, mcp_port=9500,
        prompt="hello", config_dir=None,
        internal_session_id=session_id,
    )


def _perm_sess(ndjson_line: str, exit_code: int = 0) -> MagicMock:
    s = mock_sess(exit_code=exit_code)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="")
    s.read_stdout_line = AsyncMock(side_effect=[ndjson_line, None])
    s._binary = "codex"
    return s


async def _collect(adapter: CodexAcpAdapter) -> list:
    return [e async for e in adapter.events()]


# ── Pure function: _build_codex_acp_argv ─────────────────────────────────────

@pytest.fixture
def mock_resolve_cmd():
    with patch(
        "unity_mcp.mcp_config_writer.resolve_server_cmd",
        return_value=("/usr/bin/python3", ["-m", "unity_mcp.server"]),
    ):
        yield


def test_build_argv_contains_exec_subcommand(mock_resolve_cmd) -> None:
    argv = _build_codex_acp_argv(_meta())
    assert argv[0] == "exec"


def test_build_argv_contains_format_flag(mock_resolve_cmd) -> None:
    argv = _build_codex_acp_argv(_meta())
    assert "--format" in argv
    idx = argv.index("--format")
    assert argv[idx + 1] == "acp"


def test_build_argv_contains_mcp_injection(mock_resolve_cmd) -> None:
    argv = _build_codex_acp_argv(_meta())
    joined = " ".join(argv)
    assert "-c" in argv
    assert "mcp_servers." in joined
    assert any("UNITY_MCP_PORT" in a for a in argv)
    assert any("/usr/bin/python3" in a for a in argv)


def test_build_argv_model_flag(mock_resolve_cmd) -> None:
    argv = _build_codex_acp_argv(_meta(model="o3"))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "o3"


def test_build_argv_resume_includes_session_id(mock_resolve_cmd) -> None:
    argv = _build_codex_acp_argv(_meta(session_id="sess-abc"))
    assert argv[:3] == ["exec", "resume", "sess-abc"]
    assert argv[3:5] == ["--format", "acp"]


# ── Adapter: probe ────────────────────────────────────────────────────────────

async def test_probe_returns_capabilities() -> None:
    backend = BACKENDS["codex"]
    adapter = CodexAcpAdapter(backend, PermissionBroker(mode="ask"))
    with patch.object(backend, "probe_capabilities", AsyncMock(return_value={
        "has_resume": True, "has_cancel": False,
        "has_modes": ["ask", "agent"], "binary_version": "1.0.0",
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
    adapter = CodexAcpAdapter(BACKENDS["codex"], PermissionBroker(mode="ask"))
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
    adapter = CodexAcpAdapter(BACKENDS["codex"], PermissionBroker(mode="full-access"))
    adapter._session = _perm_sess(perm_line)

    await _collect(adapter)

    calls = adapter._session.write_line.call_args_list
    assert len(calls) == 1
    payload = json.loads(calls[0].args[0])
    assert payload["outcome"] == "allow"
    assert payload["request_id"] == "req-allow"


# ── Feature flag factory ──────────────────────────────────────────────────────

# ── Factory: always ACP ───────────────────────────────────────────────────────

def test_make_codex_adapter_returns_acp() -> None:
    from unity_mcp.adapters import make_codex_adapter
    adapter = make_codex_adapter(BACKENDS["codex"], PermissionBroker(mode="ask"))
    assert isinstance(adapter, CodexAcpAdapter)
