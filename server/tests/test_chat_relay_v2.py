"""Tests for T14: relay v2 protocol negotiation and JSON drain path.

All tests are pure unit tests — no Unity, no TCP, no live processes.
"""
import asyncio
import contextlib
import json
import os
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.chat_relay import ChatRelay
from unity_mcp.stream_transform import _transform_plain_text_line

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


async def _run_cmd_start(relay: ChatRelay, backend, sess, args: dict) -> dict:
    """Patch BACKENDS + CliSession, run _cmd_start, cancel drain task."""
    with patch("unity_mcp.chat_relay.BACKENDS", {"claude": backend}), \
         patch("unity_mcp.chat_relay.CliSession", return_value=sess):
        resp = await relay._cmd_start(args)
    # cancel drain task to avoid pending-task warnings
    if relay._drain_task:
        relay._drain_task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await relay._drain_task
    return resp


# ─── Test 1: v1 negotiated when protocol_version absent ─────────────────────

async def test_v1_negotiated_when_protocol_version_absent():
    relay = ChatRelay()
    backend, sess = _make_backend()
    resp = await _run_cmd_start(relay, backend, sess, {
        "backend": "claude", "mode": "ask", "model": "m", "mcp_port": 9500,
    })
    assert resp["ok"] is True
    assert "negotiated_version" not in resp
    assert "capabilities" not in resp


# ─── Test 2: v2 negotiated when protocol_version=2 ──────────────────────────

async def test_v2_negotiated_when_protocol_version_2():
    relay = ChatRelay()
    backend, sess = _make_backend()
    resp = await _run_cmd_start(relay, backend, sess, {
        "backend": "claude", "mode": "ask", "model": "m", "mcp_port": 9500,
        "protocol_version": 2,
    })
    assert resp["ok"] is True
    assert resp["negotiated_version"] == 2
    caps = resp["capabilities"]
    assert caps["provider_id"] == "claude"
    assert caps["protocol_version"] == "2.0"
    assert isinstance(caps["modes"], list)


# ─── Test 3: feature flag disables v2 ───────────────────────────────────────

async def test_v2_disabled_by_env_flag():
    relay = ChatRelay()
    backend, sess = _make_backend()
    with patch.dict(os.environ, {"UNITY_MCP_CHAT_PROTOCOL_V2": "0"}):
        resp = await _run_cmd_start(relay, backend, sess, {
            "backend": "claude", "mode": "ask", "model": "m", "mcp_port": 9500,
            "protocol_version": 2,
        })
    assert resp["ok"] is True
    assert "negotiated_version" not in resp


# ─── Test 4: v1 drain emits pipe-format strings ─────────────────────────────

async def test_v1_drain_emits_pipe_format():
    relay = ChatRelay()
    relay._protocol_version = 1
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session("hello world")

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    pipe_events = [t for t in texts if t.startswith("t|")]
    assert len(pipe_events) >= 1
    assert pipe_events[0] == "t|hello world"


# ─── Test 5: v2 drain emits JSON lines ──────────────────────────────────────

async def test_v2_drain_emits_json_lines():
    relay = ChatRelay()
    relay._protocol_version = 2
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session("hello world")

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    json_events = _parse_kind_events(texts)
    assert len(json_events) >= 1
    assert json_events[0]["kind"] == "assistant_delta"
    assert json_events[0]["payload"]["text"] == "hello world"


# ─── Test 6: v2 JSON lines validate against schema ──────────────────────────

async def test_v2_json_lines_validate_against_schema():
    jsonschema = pytest.importorskip("jsonschema")

    schema_path = (
        Path(__file__).resolve().parents[3]
        / "protocol" / "chat-relay" / "v2" / "agent-event.schema.json"
    )
    if not schema_path.exists():
        pytest.skip("schema file not found")
    schema = json.loads(schema_path.read_text(encoding="utf-8"))

    relay = ChatRelay()
    relay._protocol_version = 2
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session("hello")

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    json_events = _parse_kind_events(texts)
    assert len(json_events) >= 1
    for event in json_events:
        jsonschema.validate(instance=event, schema=schema)


# ─── Test 7: v2 sequence is monotonic across events ─────────────────────────

async def test_v2_sequence_monotonic_across_events():
    relay = ChatRelay()
    relay._protocol_version = 2
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session("line1", "line2", "line3")

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    seqs = [d["sequence"] for d in _parse_kind_events(texts) if "sequence" in d]
    assert len(seqs) >= 3
    assert seqs == sorted(seqs), "sequences must be in ascending order"
    assert len(seqs) == len(set(seqs)), "sequences must be unique (strictly monotonic)"


# ─── Test 8: v2 EOF emits turn_completed JSON (not raw pipe) ────────────────

async def test_v2_eof_emits_turn_completed_json():
    """EOF pipe strings must go through v2 conversion — no raw pipe format in v2 buffer."""
    relay = ChatRelay()
    relay._protocol_version = 2
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session()  # immediate EOF, no content lines

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    kinds = [d["kind"] for d in _parse_kind_events(texts)]
    assert "turn_completed" in kinds, f"Expected turn_completed in kinds, got: {kinds}"
    pipe_strings = [t for t in texts if "|" in t and not t.startswith("{")]
    assert pipe_strings == [], f"Raw pipe strings found in v2 buffer: {pipe_strings}"


# ─── Test 9: v2 buffer contains no pipe-format strings after full drain ──────

async def test_v2_buffer_contains_no_pipe_format_strings():
    """Every item in v2 buffer must be valid JSON with 'kind' field."""
    relay = ChatRelay()
    relay._protocol_version = 2
    relay._transform_fn = _transform_plain_text_line
    relay._session = _make_drain_session("hello world")

    await relay._drain_stdout_loop()

    texts = [b.text for b in relay._relay_buf._buf]
    for t in texts:
        parsed = _try_parse(t)
        assert parsed is not None, f"Non-JSON item in v2 buffer: {t!r}"
        assert "kind" in parsed, f"Missing 'kind' in v2 buffer item: {t}"


# ─── Helpers ────────────────────────────────────────────────────────────────

def _parse_kind_events(texts: list[str]) -> list[dict]:
    """Return parsed JSON dicts that have a 'kind' field (AgentEvent lines)."""
    result = []
    for t in texts:
        parsed = _try_parse(t)
        if parsed is not None and "kind" in parsed:
            result.append(parsed)
    return result


def _try_parse(s: str) -> dict | None:
    try:
        return json.loads(s)
    except json.JSONDecodeError:
        return None
