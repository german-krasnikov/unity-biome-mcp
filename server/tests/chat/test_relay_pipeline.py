"""Integration tests for ChatRelay drain pipeline — real Python subprocesses, no Unity.

Uses sys.executable -c '<script>' as a mock CLI, verifying the full path:
  _cmd_spawn → _drain_stdout_loop → _transform_line → _enqueue → _cmd_events

After ACP-only migration: buffer always contains JSON AgentEvent objects.
"""
import asyncio
import json
import sys

from unity_mcp.chat_relay import ChatRelay
from unity_mcp.stream_transform import _transform_line, _transform_plain_text_line, _transform_codex_line

# ── Mock CLI scripts (executed via sys.executable -c) ────────────────────────

ECHO_STREAM = """\
import sys, json
for d in [
    {"type": "system", "subtype": "init", "session_id": "sid1"},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "Hello!"}}},
    {"type": "result", "is_error": False, "session_id": "sid1",
        "total_cost_usd": 0.001, "usage": {"input_tokens": 10, "output_tokens": 5}},
]:
    print(json.dumps(d), flush=True)
"""

TOOL_RESULT_THEN_TEXT = """\
import sys, json
for d in [
    {"type": "stream_event", "event": {"type": "content_block_start",
        "content_block": {"type": "tool_result"}}},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "SUPPRESSED"}}},
    {"type": "stream_event", "event": {"type": "content_block_stop"}},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "visible text"}}},
    {"type": "result", "is_error": False},
]:
    print(json.dumps(d), flush=True)
"""

EXIT_ZERO = "pass"
EXIT_ONE  = "import sys; sys.exit(1)"
RAW_LINE  = "print('raw_output_line', flush=True)"

# ── Helpers ──────────────────────────────────────────────────────────────────

def event_lines(resp: dict) -> list[str]:
    """Extract text lines from _cmd_events response (skips interleaved seq lines)."""
    parts = resp["data"].splitlines()
    return [parts[i] for i in range(1, len(parts), 2)]


def _kinds(lines: list[str]) -> list[str]:
    kinds = []
    for l in lines:
        try:
            kinds.append(json.loads(l)["kind"])
        except (json.JSONDecodeError, KeyError):
            pass
    return kinds


def _texts(lines: list[str]) -> list[str]:
    """Extract payload.text from assistant_delta events."""
    result = []
    for l in lines:
        try:
            obj = json.loads(l)
            if obj.get("kind") == "assistant_delta":
                result.append(obj["payload"]["text"])
        except (json.JSONDecodeError, KeyError):
            pass
    return result


async def wait_for_events(relay: ChatRelay, after: int = -1, timeout: float = 2.0) -> list[str]:
    """Poll _cmd_events, accumulating until terminal turn_completed or error event or timeout."""
    deadline = asyncio.get_running_loop().time() + timeout
    all_lines: list[str] = []
    current_after = after
    while asyncio.get_running_loop().time() < deadline:
        resp = await relay._cmd_events({"after_seq": current_after, "timeout_ms": 200})
        batch = event_lines(resp)
        if batch:
            all_lines.extend(batch)
            parts = resp["data"].splitlines()
            current_after = int(parts[-2])
            # Terminal: turn_completed or error JSON event
            terminal_kinds = {"turn_completed", "error"}
            if any(k in terminal_kinds for k in _kinds(batch)):
                return all_lines
        await asyncio.sleep(0.05)
    return all_lines


async def spawn(relay: ChatRelay, script: str, transform: bool = True) -> None:
    """Spawn subprocess. transform=True → stream-json; False → plain-text."""
    relay._transform_fn = _transform_line if transform else _transform_plain_text_line
    await relay._cmd_spawn({
        "binary": sys.executable,
        "argv": ["-c", script],
        "env_set": {},
        "env_strip": [],
    })

# ── Tests ────────────────────────────────────────────────────────────────────

async def test_pipeline_text_arrives_as_json():
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    lines = await wait_for_events(relay)
    kinds = _kinds(lines)
    assert "session_started" in kinds
    assert "assistant_delta" in kinds
    assert "turn_completed" in kinds
    delta_texts = _texts(lines)
    assert "Hello!" in delta_texts
    await relay._kill_current()


async def test_pipeline_all_events_are_json():
    """ACP drain: every buffer item is valid JSON with a kind field."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    lines = await wait_for_events(relay)
    for l in lines:
        parsed = json.loads(l)  # must not raise
        assert "kind" in parsed, f"Missing 'kind': {l}"
    await relay._kill_current()


async def test_pipeline_tool_result_text_suppressed():
    relay = ChatRelay()
    await spawn(relay, TOOL_RESULT_THEN_TEXT)
    lines = await wait_for_events(relay)
    assert not any("SUPPRESSED" in l for l in lines)
    assert "visible text" in _texts(lines)
    await relay._kill_current()


async def test_pipeline_clean_exit_produces_done_event():
    relay = ChatRelay()
    await spawn(relay, EXIT_ZERO)
    lines = await wait_for_events(relay)
    kinds = _kinds(lines)
    assert "turn_completed" in kinds
    assert "error" not in kinds
    await relay._kill_current()


async def test_pipeline_exit1_produces_error_event():
    relay = ChatRelay()
    await spawn(relay, EXIT_ONE)
    lines = await wait_for_events(relay)
    assert "error" in _kinds(lines)
    await relay._kill_current()


async def test_pipeline_plain_text_wraps_as_assistant_delta():
    """plain-text transform: raw stdout lines arrive as assistant_delta JSON events."""
    relay = ChatRelay()
    await spawn(relay, RAW_LINE, transform=False)
    lines = await wait_for_events(relay)
    assert "raw_output_line" in _texts(lines)
    await relay._kill_current()


async def test_pipeline_seq_monotonic_across_respawns():
    """Seq numbers never reset between kills — C# lastSeq filter stays valid."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)
    seq_after_first = relay._next_seq

    await relay._kill_current()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)

    assert relay._next_seq > seq_after_first
    await relay._kill_current()


async def test_pipeline_kill_clears_buffer():
    """kill() clears the event buffer to prevent cross-session event leakage."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)
    assert len(relay._buf) > 0

    await relay._kill_current()
    assert len(relay._buf) == 0


# ── Codex pipeline scripts ────────────────────────────────────────────────────

CODEX_MCP_TOOL_CALL = """\
import sys, json
print(json.dumps({"type": "item.started", "item": {
    "type": "mcp_tool_call",
    "tool": "get_hierarchy",
    "id": "item_1",
    "arguments": {"full": True}
}}), flush=True)
print(json.dumps({"type": "item.completed", "item": {
    "type": "mcp_tool_call",
    "id": "item_1",
    "status": "success",
    "result": {"content": [{"type": "text", "text": "Main Camera"}]}
}}), flush=True)
"""

PRINT_PORT_ENV = """\
import os, sys
print(os.environ.get('UNITY_MCP_PORT', 'MISSING'), flush=True)
"""

# ── Tests ─────────────────────────────────────────────────────────────────────

async def test_pipeline_codex_mcp_tool_call_emits_tc_event():
    """T5: Codex item.started mcp_tool_call → tool_call_started JSON in buffer."""
    relay = ChatRelay()
    relay._transform_fn = _transform_codex_line
    await relay._cmd_spawn({
        "binary": sys.executable,
        "argv": ["-c", CODEX_MCP_TOOL_CALL],
        "env_set": {},
        "env_strip": [],
    })
    lines = await wait_for_events(relay)
    tc_events = [json.loads(l) for l in lines
                 if _safe_kind(l) == "tool_call_started"]
    assert tc_events, f"tool_call_started not found; got: {lines}"
    assert tc_events[0]["payload"]["name"] == "get_hierarchy"
    await relay._kill_current()


async def test_pipeline_codex_mcp_tool_result_emits_tr_event():
    """T6: Codex item.completed mcp_tool_call → tool_call_completed JSON in buffer."""
    relay = ChatRelay()
    relay._transform_fn = _transform_codex_line
    await relay._cmd_spawn({
        "binary": sys.executable,
        "argv": ["-c", CODEX_MCP_TOOL_CALL],
        "env_set": {},
        "env_strip": [],
    })
    lines = await wait_for_events(relay)
    tr_events = [json.loads(l) for l in lines
                 if _safe_kind(l) == "tool_call_completed"]
    assert tr_events, f"tool_call_completed not found; got: {lines}"
    await relay._kill_current()


async def test_pipeline_env_port_forwarded_to_subprocess():
    """T7: env_set UNITY_MCP_PORT is forwarded to the real subprocess environment."""
    relay = ChatRelay()
    relay._transform_fn = _transform_plain_text_line
    await relay._cmd_spawn({
        "binary": sys.executable,
        "argv": ["-c", PRINT_PORT_ENV],
        "env_set": {"UNITY_MCP_PORT": "9601"},
        "env_strip": [],
    })
    lines = await wait_for_events(relay)
    assert "9601" in _texts(lines), f"9601 not found in texts; got: {lines}"
    await relay._kill_current()


def _safe_kind(text: str) -> str:
    try:
        return json.loads(text).get("kind", "")
    except (json.JSONDecodeError, AttributeError):
        return ""
