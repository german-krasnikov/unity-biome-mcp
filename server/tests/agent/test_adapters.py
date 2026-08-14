"""Tests for LegacyCliAdapter — mocked CliSession, no Unity. (18 tests)"""
from __future__ import annotations

import json
from unittest.mock import AsyncMock, MagicMock, patch

from relay_helpers import mock_sess

from unity_mcp.adapters.legacy import LegacyCliAdapter
from unity_mcp.agent_event import ProviderCapabilities
from unity_mcp.backend_def import BACKENDS
from unity_mcp.cli_session import SessionMeta


def _make_meta(mode: str = "ask") -> SessionMeta:
    return SessionMeta(
        backend="claude", mode=mode, model=None, mcp_port=9500,
        prompt="", config_dir=None,
    )


def _text_delta(text: str) -> str:
    return json.dumps({
        "type": "stream_event",
        "event": {"type": "content_block_delta", "delta": {"type": "text_delta", "text": text}},
    })


def _result_line(sid: str = "s1", cost: float = 0.001, inp: int = 100, out: int = 50) -> str:
    return json.dumps({
        "type": "result", "session_id": sid, "total_cost_usd": cost,
        "usage": {
            "input_tokens": inp, "output_tokens": out,
            "cache_creation_input_tokens": 0, "cache_read_input_tokens": 0,
        },
    })


def _sess_with_lines(*lines, exit_code: int = 0) -> MagicMock:
    """Return a mock CliSession that yields the given lines then EOF."""
    s = mock_sess(exit_code=exit_code)
    s.wait = AsyncMock()
    s.drain_stderr = AsyncMock(return_value="")
    s.read_stdout_line = AsyncMock(side_effect=[*lines, None])
    return s


async def _collect(adapter: LegacyCliAdapter) -> list:
    return [e async for e in adapter.events()]


# ── probe ──────────────────────────────────────────────────────────────────────

async def test_adapter_probe_returns_provider_capabilities():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    with patch.object(adapter._backend, "probe_capabilities", AsyncMock(return_value={
        "has_resume": True, "has_cancel": False,
        "has_modes": ["ask", "agent"], "binary_version": "1.0",
    })):
        caps = await adapter.probe()
    assert isinstance(caps, ProviderCapabilities)
    assert "thought_delta" in caps.events


# ── start ──────────────────────────────────────────────────────────────────────

async def test_adapter_start_creates_session():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    mock_cli = MagicMock()
    mock_cli.start = AsyncMock()
    mock_cli.close_stdin = MagicMock()

    with (
        patch("unity_mcp.adapters.legacy.CliSession", return_value=mock_cli),
        patch.object(adapter._backend, "resolve_binary", AsyncMock(return_value="/usr/bin/claude")),
        patch.object(adapter._backend, "build_args", return_value=([], {}, [])),
    ):
        await adapter.start(_make_meta())

    mock_cli.start.assert_called_once()


async def test_adapter_start_resets_accumulator():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._acc.active = True  # dirty state

    mock_cli = MagicMock()
    mock_cli.start = AsyncMock()
    mock_cli.close_stdin = MagicMock()

    with (
        patch("unity_mcp.adapters.legacy.CliSession", return_value=mock_cli),
        patch.object(adapter._backend, "resolve_binary", AsyncMock(return_value="/usr/bin/claude")),
        patch.object(adapter._backend, "build_args", return_value=([], {}, [])),
    ):
        await adapter.start(_make_meta())

    assert not adapter._acc.active


async def test_adapter_start_resets_seq_to_zero():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._seq = 5

    mock_cli = MagicMock()
    mock_cli.start = AsyncMock()
    mock_cli.close_stdin = MagicMock()

    with (
        patch("unity_mcp.adapters.legacy.CliSession", return_value=mock_cli),
        patch.object(adapter._backend, "resolve_binary", AsyncMock(return_value="/usr/bin/claude")),
        patch.object(adapter._backend, "build_args", return_value=([], {}, [])),
    ):
        await adapter.start(_make_meta())

    assert adapter._seq == 0


# ── Claude events ──────────────────────────────────────────────────────────────

async def test_adapter_claude_text_delta_yields_assistant_delta():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(_text_delta("hello"))

    evts = await _collect(adapter)

    delta = next((e for e in evts if e.kind == "assistant_delta"), None)
    assert delta is not None
    assert delta.payload["text"] == "hello"


async def test_adapter_claude_tool_call_full_sequence():
    # 3-line sequence: content_block_start + delta + stop → single tool_call_started
    start = json.dumps({"type": "stream_event", "event": {"type": "content_block_start",
        "content_block": {"type": "tool_use", "name": "search", "id": "tc-1"}}})
    delta = json.dumps({"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "input_json_delta", "partial_json": '{"q":"x"}'}}})
    stop = json.dumps({"type": "stream_event", "event": {"type": "content_block_stop"}})

    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(start, delta, stop)

    evts = await _collect(adapter)

    tc = next((e for e in evts if e.kind == "tool_call_started"), None)
    assert tc is not None
    assert tc.payload["name"] == "search"
    assert tc.payload["id"] == "tc-1"


async def test_adapter_claude_thinking_block():
    # thinking start + delta + stop → thought_delta
    t_start = json.dumps({"type": "stream_event", "event": {"type": "content_block_start",
        "content_block": {"type": "thinking"}}})
    t_delta = json.dumps({"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "thinking_delta", "thinking": "I think..."}}})
    t_stop = json.dumps({"type": "stream_event", "event": {"type": "content_block_stop"}})

    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(t_start, t_delta, t_stop)

    evts = await _collect(adapter)

    thought = next((e for e in evts if e.kind == "thought_delta"), None)
    assert thought is not None
    assert thought.payload["text"] == "I think..."


async def test_adapter_claude_d_pipe_emits_cost_and_turn_completed():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(_result_line())

    evts = await _collect(adapter)

    kinds = [e.kind for e in evts]
    assert "cost_update" in kinds
    assert "turn_completed" in kinds


# ── Codex events ───────────────────────────────────────────────────────────────

async def test_adapter_codex_item_started_yields_tool_call_started():
    line = json.dumps({"type": "item.started", "item": {
        "type": "mcp_tool_call", "tool": "search", "id": "c-1", "arguments": {"q": "test"},
    }})
    adapter = LegacyCliAdapter(BACKENDS["codex"])
    adapter._session = _sess_with_lines(line)

    evts = await _collect(adapter)

    tc = next((e for e in evts if e.kind == "tool_call_started"), None)
    assert tc is not None
    assert tc.payload["name"] == "search"


async def test_adapter_codex_turn_completed_emits_two_events():
    line = json.dumps({"type": "turn.completed",
        "usage": {"input_tokens": 100, "output_tokens": 50}})
    adapter = LegacyCliAdapter(BACKENDS["codex"])
    adapter._session = _sess_with_lines(line)

    evts = await _collect(adapter)

    kinds = [e.kind for e in evts]
    assert "cost_update" in kinds
    assert "turn_completed" in kinds


# ── OpenCode events ────────────────────────────────────────────────────────────

async def test_adapter_opencode_tool_use_emits_two_events():
    line = json.dumps({"type": "tool_use", "part": {
        "tool": "run_tests", "callID": "c-1",
        "state": {"input": {"mode": "EditMode"}, "status": "completed", "output": "3 passed"},
    }})
    adapter = LegacyCliAdapter(BACKENDS["opencode"])
    adapter._session = _sess_with_lines(line)

    evts = await _collect(adapter)

    kinds = [e.kind for e in evts]
    assert "tool_call_started" in kinds
    assert "tool_call_completed" in kinds


# ── Kimi events ────────────────────────────────────────────────────────────────

async def test_adapter_kimi_assistant_yields_assistant_delta():
    line = json.dumps({"role": "assistant", "content": "Hello!"})
    adapter = LegacyCliAdapter(BACKENDS["kimi"])
    adapter._session = _sess_with_lines(line)

    evts = await _collect(adapter)

    delta = next((e for e in evts if e.kind == "assistant_delta"), None)
    assert delta is not None
    assert delta.payload["text"] == "Hello!"


# ── Agy (plain text) events ────────────────────────────────────────────────────

async def test_adapter_agy_plain_text_yields_assistant_delta():
    adapter = LegacyCliAdapter(BACKENDS["agy"])
    adapter._session = _sess_with_lines("Hello from Agy")

    evts = await _collect(adapter)

    delta = next((e for e in evts if e.kind == "assistant_delta"), None)
    assert delta is not None
    assert delta.payload["text"] == "Hello from Agy"


async def test_adapter_agy_clean_exit_emits_turn_completed():
    adapter = LegacyCliAdapter(BACKENDS["agy"])
    adapter._session = _sess_with_lines("Hi", exit_code=0)

    evts = await _collect(adapter)

    kinds = [e.kind for e in evts]
    assert "turn_completed" in kinds
    assert "cost_update" not in kinds


# ── Error exit ─────────────────────────────────────────────────────────────────

async def test_adapter_error_exit_emits_error_event():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    sess = mock_sess(exit_code=1)
    sess.wait = AsyncMock()
    sess.drain_stderr = AsyncMock(return_value="something went wrong")
    sess.read_stdout_line = AsyncMock(side_effect=[None])
    adapter._session = sess

    evts = await _collect(adapter)

    err = next((e for e in evts if e.kind == "error"), None)
    assert err is not None
    assert "exited 1" in err.payload["message"]


# ── Sequence monotonicity ──────────────────────────────────────────────────────

async def test_adapter_sequence_monotonically_increases():
    # 5-event Claude turn: delta, delta, thought_delta, cost_update, turn_completed
    t_start = json.dumps({"type": "stream_event", "event": {"type": "content_block_start",
        "content_block": {"type": "thinking"}}})
    t_delta = json.dumps({"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "thinking_delta", "thinking": "hmm"}}})
    t_stop = json.dumps({"type": "stream_event", "event": {"type": "content_block_stop"}})

    lines = [
        _text_delta("hi"),        # assistant_delta (1)
        _text_delta(" there"),    # assistant_delta (2)
        t_start,                  # no event
        t_delta,                  # no event
        t_stop,                   # thought_delta (3)
        _result_line(),           # cost_update (4) + turn_completed (5)
    ]
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(*lines)

    evts = await _collect(adapter)

    assert len(evts) == 5
    seqs = [e.sequence for e in evts]
    for i in range(1, len(seqs)):
        assert seqs[i] > seqs[i - 1], f"sequence not monotonic at index {i}: {seqs}"


# ── Cancel ─────────────────────────────────────────────────────────────────────

async def test_adapter_cancel_stops_events_generator():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    adapter._session = _sess_with_lines(
        _text_delta("msg1"),
        _text_delta("msg2"),
    )

    collected = []
    async for evt in adapter.events():
        collected.append(evt)
        if len(collected) == 1:
            await adapter.cancel()  # session → None; guard fires next iteration

    assert len(collected) == 1
    assert adapter._session is None


# ── set_mode ───────────────────────────────────────────────────────────────────

async def test_adapter_set_mode_replaces_session():
    adapter = LegacyCliAdapter(BACKENDS["claude"])
    old_sess = mock_sess()
    old_sess.wait = AsyncMock()
    adapter._session = old_sess
    adapter._meta = _make_meta(mode="ask")

    new_cli = MagicMock()
    new_cli.start = AsyncMock()
    new_cli.close_stdin = MagicMock()

    with (
        patch("unity_mcp.adapters.legacy.CliSession", return_value=new_cli),
        patch.object(adapter._backend, "resolve_binary", AsyncMock(return_value="/usr/bin/claude")),
        patch.object(adapter._backend, "build_args", return_value=([], {}, [])),
    ):
        await adapter.set_mode("agent")

    old_sess.kill.assert_called_once()
    assert adapter._session is new_cli
