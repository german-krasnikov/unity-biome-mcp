"""Tests for ACP-only relay behavior.

Pure unit tests — no Unity, no TCP, no live processes.

RED: All 5 tests FAIL with current code (legacy v1/v2 negotiation present).
GREEN: Tests pass after migration to ACP-always path.
"""
import asyncio
import contextlib
import json
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.agent_event import AgentEvent
from unity_mcp.backend_def import (
    OUTPUT_FORMAT_KIMI_JSON,
    OUTPUT_FORMAT_OPENCODE_JSON,
    OUTPUT_FORMAT_PLAIN_TEXT,
)
from unity_mcp.chat_relay import ChatRelay
from unity_mcp.stream_transform import (
    _transform_kimi_line,
    _transform_opencode_line,
    _transform_plain_text_line,
)


# ─── Helpers ────────────────────────────────────────────────────────────────


def _make_backend(pid: int = 1234):
    """Mock BackendDef + CliSession pair ready for _cmd_start injection."""
    b = MagicMock()
    b.reads_stdin = True
    b.output_format = "stream-json"
    b.has_resume = True
    b.resolve_binary = AsyncMock(return_value="/bin/claude")
    b.build_args = MagicMock(return_value=(["claude"], {}, []))
    b.probe_capabilities = AsyncMock(return_value={
        "has_resume": True,
        "binary_version": "1.0",
        "has_modes": ["ask", "agent"],
    })

    sess = MagicMock()
    sess.pid = pid
    sess.start = AsyncMock()
    sess.close_stdin = MagicMock()
    sess.read_stdout_line = AsyncMock(return_value=None)
    sess.wait = AsyncMock()
    sess.drain_stderr = AsyncMock(return_value="")
    sess.exit_code = 0
    return b, sess


def _make_drain_session(*lines: str):
    """Mock CliSession that yields each string line then EOF."""
    sess = MagicMock()
    sess.pid = 1234
    sess.exit_code = 0
    sess.read_stdout_line = AsyncMock(side_effect=[*lines, None])
    sess.wait = AsyncMock()
    sess.drain_stderr = AsyncMock(return_value="")
    return sess


class _MockAdapter:
    """Minimal adapter that yields a fixed list of AgentEvents."""

    def __init__(self, events: list[AgentEvent]) -> None:
        self._events = events

    async def events(self):
        for event in self._events:
            yield event

    async def probe(self):
        from unity_mcp.agent_event import ProviderCapabilities
        return ProviderCapabilities()

    async def start(self, meta) -> None: ...
    async def prompt(self, text: str, turn_id: int) -> None: ...
    async def cancel(self) -> None: ...
    async def set_mode(self, mode: str) -> None: ...
    async def close(self) -> None: ...


async def _run_cmd_start(relay: ChatRelay, backend, sess, args: dict) -> dict:
    """Patch BACKENDS + CliSession, run _cmd_start, cancel drain task."""
    with patch("unity_mcp.chat_relay.BACKENDS", {"claude": backend}), \
         patch("unity_mcp.chat_relay.CliSession", return_value=sess):
        resp = await relay._cmd_start(args)
    if relay._drain_task:
        relay._drain_task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await relay._drain_task
    return resp


def _buffer_texts(relay: ChatRelay) -> list[str]:
    return [b.text for b in relay._relay_buf._buf]


def _buffer_json_kinds(relay: ChatRelay) -> list[str]:
    kinds = []
    for t in _buffer_texts(relay):
        try:
            obj = json.loads(t)
            if "kind" in obj:
                kinds.append(obj["kind"])
        except (json.JSONDecodeError, AttributeError):
            pass
    return kinds


# ─── Test 1: _cmd_start always returns ACP capabilities ──────────────────────


async def test_acp_start_always_succeeds():
    """_cmd_start must return capabilities without requiring protocol_version=2.

    RED:  capabilities only in response when protocol_version >= 2 in args.
    GREEN: capabilities always in response (ACP-only path, no negotiation).
    """
    relay = ChatRelay()
    backend, sess = _make_backend()
    resp = await _run_cmd_start(relay, backend, sess, {
        "backend": "claude",
        "mode": "ask",
        "model": "m",
        "mcp_port": 9500,
        # No protocol_version — ACP capabilities must be returned unconditionally
    })

    assert resp["ok"] is True
    assert "capabilities" in resp, (
        "ACP-only relay must always return capabilities on start; "
        "got keys: " + str(list(resp.keys()))
    )


# ─── Test 2: drain emits plan_step_started JSON ──────────────────────────────


async def test_drain_emits_plan_step_json():
    """Drain loop must emit plan_step_started from adapter events into relay buffer.

    RED:  _drain_stdout_loop ignores relay._adapter; plan events never reach buffer.
    GREEN: drain picks up adapter.events() and enqueues AgentEvent JSON.
    """
    relay = ChatRelay()
    plan_event = AgentEvent(
        kind="plan_step_started",
        payload={"description": "Write tests first"},
    )
    relay._adapter = _MockAdapter([plan_event])
    relay._session = _make_drain_session()  # immediate EOF — no raw stdout lines

    await relay._drain_stdout_loop()

    kinds = _buffer_json_kinds(relay)
    assert "plan_step_started" in kinds, (
        "Expected plan_step_started in relay buffer — "
        "drain loop does not yet use relay._adapter.events(). "
        f"Buffer kinds: {kinds}"
    )
    # Verify payload is preserved
    found = next(
        (json.loads(t) for t in _buffer_texts(relay)
         if _safe_kind(t) == "plan_step_started"),
        None,
    )
    assert found is not None
    assert found["payload"]["description"] == "Write tests first"


# ─── Test 3: drain emits file_change_detected JSON ───────────────────────────


async def test_drain_emits_file_change_json():
    """Drain loop must emit file_change_detected from adapter events into relay buffer.

    RED:  _drain_stdout_loop ignores relay._adapter; file events never reach buffer.
    GREEN: drain picks up adapter.events() and enqueues file_change_detected JSON.
    """
    relay = ChatRelay()
    file_event = AgentEvent(
        kind="file_change_detected",
        payload={"path": "/Assets/Scripts/PlayerController.cs"},
    )
    relay._adapter = _MockAdapter([file_event])
    relay._session = _make_drain_session()

    await relay._drain_stdout_loop()

    kinds = _buffer_json_kinds(relay)
    assert "file_change_detected" in kinds, (
        "Expected file_change_detected in relay buffer — "
        "drain loop does not yet use relay._adapter.events(). "
        f"Buffer kinds: {kinds}"
    )


# ─── Test 4: EOF produces turn_completed JSON without manual protocol setup ───


async def test_turn_completed_on_eof():
    """Clean process exit must produce turn_completed JSON without setting _protocol_version=2.

    RED:  default _protocol_version=1 means EOF emits d| pipe string, not JSON.
    GREEN: ACP-always drain emits turn_completed JSON unconditionally.
    """
    relay = ChatRelay()
    relay._transform_fn = _transform_plain_text_line
    # Do NOT set relay._protocol_version = 2 — that field is being removed
    relay._session = _make_drain_session()  # immediate clean EOF

    await relay._drain_stdout_loop()

    kinds = _buffer_json_kinds(relay)
    assert "turn_completed" in kinds, (
        "Expected turn_completed JSON event on clean EOF. "
        f"Buffer kinds: {kinds}, texts: {_buffer_texts(relay)}"
    )


# ─── Test 5: buffer never contains pipe-format strings ───────────────────────


async def test_no_pipe_format_in_buffer():
    """Relay buffer must never contain raw pipe-format strings (t|, e|, d|, etc.).

    RED:  default _protocol_version=1 puts pipe strings directly in buffer.
    GREEN: ACP-always drain always enqueues JSON; no pipe strings ever enter buffer.
    """
    relay = ChatRelay()
    relay._transform_fn = _transform_plain_text_line
    # Do NOT set relay._protocol_version = 2
    relay._session = _make_drain_session("hello world")

    await relay._drain_stdout_loop()

    texts = _buffer_texts(relay)
    pipe_strings = [t for t in texts if "|" in t and not t.startswith("{")]
    assert pipe_strings == [], (
        f"Pipe-format strings found in relay buffer: {pipe_strings}"
    )


# ─── Test 6: _cmd_start selects correct transform per backend ────────────────


@pytest.mark.parametrize("backend_name,output_fmt,expected_fn", [
    ("kimi",     OUTPUT_FORMAT_KIMI_JSON,     _transform_kimi_line),
    ("opencode", OUTPUT_FORMAT_OPENCODE_JSON, _transform_opencode_line),
    ("agy",      OUTPUT_FORMAT_PLAIN_TEXT,    _transform_plain_text_line),
])
async def test_cmd_start_selects_correct_transform_per_backend(
    backend_name, output_fmt, expected_fn,
):
    """_cmd_start must wire the correct _transform_fn for each non-claude backend."""
    relay = ChatRelay()

    backend = MagicMock()
    backend.reads_stdin = True
    backend.output_format = output_fmt
    backend.has_resume = False
    backend.resolve_binary = AsyncMock(return_value="/bin/fake")
    backend.build_args = MagicMock(return_value=([], {}, []))
    backend.probe_capabilities = AsyncMock(return_value={})

    sess = MagicMock()
    sess.pid = 1234
    sess.start = AsyncMock()
    sess.read_stdout_line = AsyncMock(return_value=None)
    sess.wait = AsyncMock()
    sess.drain_stderr = AsyncMock(return_value="")
    sess.exit_code = 0
    sess.close_stdin = MagicMock()

    with patch("unity_mcp.chat_relay.BACKENDS", {backend_name: backend}), \
         patch("unity_mcp.chat_relay.CliSession", return_value=sess):
        resp = await relay._cmd_start({
            "backend": backend_name, "mode": "ask", "model": None, "mcp_port": 9500,
        })

    if relay._drain_task:
        relay._drain_task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await relay._drain_task

    assert resp["ok"] is True
    assert relay._transform_fn is expected_fn, (
        f"{backend_name}: expected {expected_fn.__name__}, "
        f"got {relay._transform_fn.__name__}"
    )


# ─── Internal helper ─────────────────────────────────────────────────────────


def _safe_kind(text: str) -> str:
    try:
        return json.loads(text).get("kind", "")
    except (json.JSONDecodeError, AttributeError):
        return ""
