"""Tests for AcpAgentAdapter — mocked CliSession, no Unity. (14 tests)"""
from __future__ import annotations

import json
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from relay_helpers import mock_sess

from unity_mcp.adapters.acp import AcpAgentAdapter
from unity_mcp.adapters.acp_parser import parse_acp_line
from unity_mcp.adapters.protocol import EventContext
from unity_mcp.agent_event import ProviderCapabilities
from unity_mcp.backend_def import BACKENDS
from unity_mcp.permission_broker import PermissionBroker

_FIXTURES = Path(__file__).parent / "fixtures" / "acp"


def _ctx() -> EventContext:
    return EventContext(conversation_id="conv-1", session_id="sess-1", turn_id=1, sequence=0)


def _perm_sess(ndjson_line: str, exit_code: int = 0) -> MagicMock:
    """Mock CliSession yielding one NDJSON line then EOF."""
    s = mock_sess(exit_code=exit_code)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="")
    s.read_stdout_line = AsyncMock(side_effect=[ndjson_line, None])
    s._binary = "opencode"
    return s


async def _collect(adapter: AcpAgentAdapter) -> list:
    return [e async for e in adapter.events()]


# ── Fixture round-trip (parametrized) ─────────────────────────────────────────

@pytest.mark.parametrize("fixture_file,expected_kind", [
    ("session-create.ndjson",                 "session_started"),
    ("session-update-text.ndjson",            "assistant_delta"),
    ("session-update-thinking.ndjson",        "thought_delta"),
    ("session-update-tool-call.ndjson",       "tool_call_started"),
    ("session-update-tool-result.ndjson",     "tool_call_completed"),
    ("session-update-tool-result-err.ndjson", "tool_call_failed"),
    ("session-complete.ndjson",               "cost_update"),
    ("session-complete.ndjson",               "turn_completed"),
    ("session-error.ndjson",                  "error"),
    ("permission-request.ndjson",             "permission_requested"),
])
def test_parser_fixture_round_trip(fixture_file: str, expected_kind: str) -> None:
    content = (_FIXTURES / fixture_file).read_text(encoding="utf-8").strip()
    evts = parse_acp_line(content, _ctx())
    assert any(e.kind == expected_kind for e in evts), (
        f"{fixture_file}: expected kind '{expected_kind}' not found in {[e.kind for e in evts]}"
    )


def test_unknown_fixture_returns_empty() -> None:
    content = (_FIXTURES / "unknown-type.ndjson").read_text(encoding="utf-8").strip()
    assert parse_acp_line(content, _ctx()) == []


# ── Adapter: probe ────────────────────────────────────────────────────────────

async def test_probe_returns_acp_capabilities() -> None:
    backend = BACKENDS["opencode"]
    adapter = AcpAgentAdapter(backend, PermissionBroker(mode="ask"))
    with patch.object(backend, "probe_capabilities", AsyncMock(return_value={
        "has_resume": True, "has_cancel": False,
        "has_modes": ["ask", "agent"], "binary_version": "0.3.94",
    })):
        caps = await adapter.probe()
    assert isinstance(caps, ProviderCapabilities)
    assert caps.transport == "stdio"


# ── Adapter: permission deny ──────────────────────────────────────────────────

async def test_adapter_permission_denied_writes_to_stdin() -> None:
    """ask mode → deny for write tools → write_line called with outcome=deny."""
    perm_line = json.dumps({
        "type": "session/request_permission",
        "tool_name": "execute_code",   # write tool → denied in ask mode
        "request_id": "req-deny",
        "input": {},
    })
    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="ask"))
    adapter._session = _perm_sess(perm_line)

    await _collect(adapter)

    calls = adapter._session.write_line.call_args_list
    assert len(calls) == 1
    payload = json.loads(calls[0].args[0])
    assert payload["type"] == "session/permission_response"
    assert payload["request_id"] == "req-deny"
    assert payload["outcome"] == "deny"


# ── Adapter: permission allow ─────────────────────────────────────────────────

async def test_adapter_permission_allowed_writes_to_stdin() -> None:
    """full-access mode → allow all → write_line called with outcome=allow."""
    perm_line = json.dumps({
        "type": "session/request_permission",
        "tool_name": "execute_code",
        "request_id": "req-allow",
        "input": {},
    })
    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="full-access"))
    adapter._session = _perm_sess(perm_line)

    await _collect(adapter)

    calls = adapter._session.write_line.call_args_list
    assert len(calls) == 1
    payload = json.loads(calls[0].args[0])
    assert payload["type"] == "session/permission_response"
    assert payload["request_id"] == "req-allow"
    assert payload["outcome"] == "allow"


# ── Adapter: prompt ──────────────────────────────────────────────────────────

async def test_prompt_writes_raw_text_to_stdin() -> None:
    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="ask"))
    adapter._session = _perm_sess("")  # dummy session
    await adapter.prompt("hello world", turn_id=3)
    adapter._session.write_line.assert_called_once_with("hello world")
    assert adapter._turn_id == 3


# ── Adapter: non-zero exit emits error ───────────────────────────────────────

# ── C2: _respond_to_permission must not propagate OS-level subprocess death ──

async def test_respond_to_permission_silent_on_dead_subprocess() -> None:
    """C2: write_line raises RuntimeError (dead subprocess) → events() must not raise."""
    perm_line = json.dumps({
        "type": "session/request_permission",
        "tool_name": "execute_code",
        "request_id": "req-dead",
        "input": {},
    })
    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="ask"))
    s = _perm_sess(perm_line)
    s.write_line = AsyncMock(side_effect=RuntimeError("broken pipe"))
    adapter._session = s

    # Must not raise — collect should complete normally
    evts = await _collect(adapter)
    # permission_requested event was yielded before the write attempt
    assert any(e.kind == "permission_requested" for e in evts)


async def test_nonzero_exit_emits_error_event() -> None:
    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="ask"))
    s = mock_sess(exit_code=1)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="segfault")
    s.read_stdout_line = AsyncMock(side_effect=[None])
    s._binary = "opencode"
    adapter._session = s

    evts = await _collect(adapter)
    assert len(evts) == 1
    assert evts[0].kind == "error"
    assert "exited 1" in evts[0].payload["message"]
    assert "segfault" in evts[0].payload["message"]


# ── Cancel ────────────────────────────────────────────────────────────────────

async def test_adapter_cancel_stops_events_generator():
    """cancel() after first event stops the generator (same guard as LegacyCliAdapter)."""
    text_line = (_FIXTURES / "session-update-text.ndjson").read_text(encoding="utf-8").strip()

    adapter = AcpAgentAdapter(BACKENDS["opencode"], PermissionBroker(mode="ask"))
    s = mock_sess(exit_code=0)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="")
    s.read_stdout_line = AsyncMock(side_effect=[text_line, text_line, None])
    s._binary = "opencode"
    adapter._session = s

    collected = []
    async for evt in adapter.events():
        collected.append(evt)
        if len(collected) == 1:
            await adapter.cancel()

    assert len(collected) == 1
    assert adapter._session is None
