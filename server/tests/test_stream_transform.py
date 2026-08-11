"""Pure unit tests for stream_transform — no subprocess, no async, no fixtures."""
import json

import pytest

from unity_mcp.stream_transform import (
    _ToolCallAcc, _transform_line,
    _transform_plain_text_line, _transform_codex_line, _transform_opencode_line,
    _transform_kimi_line,
)


# ── text delta ───────────────────────────────────────────────────────────────

def test_text_delta():
    line = '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"hello"}}}'
    assert _transform_line(line, _ToolCallAcc()) == ["t|hello"]


def test_empty_text_delta():
    line = '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":""}}}'
    assert _transform_line(line, _ToolCallAcc()) == ["t|"]


def test_text_with_pipe():
    """Pipe chars in text are safe — RelayEventParser splits only first pipe."""
    line = '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"a|b|c"}}}'
    assert _transform_line(line, _ToolCallAcc()) == ["t|a|b|c"]


# ── session init ─────────────────────────────────────────────────────────────

def test_session_init():
    line = '{"type":"system","subtype":"init","session_id":"abc123"}'
    assert _transform_line(line, _ToolCallAcc()) == ["si|abc123"]


def test_api_retry():
    """M2: api_retry is transient — suppressed, not shown as permanent error."""
    line = '{"type":"system","subtype":"api_retry","error":"Rate limit"}'
    assert _transform_line(line, _ToolCallAcc()) == []


def test_unknown_system_subtype():
    line = '{"type":"system","subtype":"future_thing"}'
    assert _transform_line(line, _ToolCallAcc()) == []


# ── result ───────────────────────────────────────────────────────────────────

def test_result_success():
    line = json.dumps({"type": "result", "is_error": False, "session_id": "s1",
                       "total_cost_usd": 0.001, "usage": {"input_tokens": 100, "output_tokens": 50}})
    assert _transform_line(line, _ToolCallAcc()) == ["d|s1|0.001|100|50"]


def test_result_error():
    line = json.dumps({"type": "result", "is_error": True, "error": "Timeout"})
    assert _transform_line(line, _ToolCallAcc()) == ["e|Timeout"]


def test_synthetic_done():
    """Relay's own clean-exit synthetic becomes d|..."""
    line = json.dumps({"type": "result", "subtype": "done", "is_error": False})
    r = _transform_line(line, _ToolCallAcc())
    assert r == ["d||0|0|0"]


def test_synthetic_error():
    line = json.dumps({"type": "result", "is_error": True, "error": "Process cli exited 1"})
    assert _transform_line(line, _ToolCallAcc()) == ["e|Process cli exited 1"]


# ── tool call accumulation ────────────────────────────────────────────────────

def test_tool_call_full_sequence():
    acc = _ToolCallAcc()
    # start
    r1 = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start","content_block":{"type":"tool_use","name":"bash","id":"tid1"}}}',
        acc,
    )
    assert r1 == [] and acc.active and acc.name == "bash" and acc.id == "tid1"
    # delta 1
    r2 = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"input_json_delta","partial_json":"{\\"cmd\\":"}}}',
        acc,
    )
    assert r2 == []
    # delta 2
    r3 = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"input_json_delta","partial_json":"\\"ls\\"}"}}}',
        acc,
    )
    assert r3 == []
    # stop
    r4 = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_stop"}}',
        acc,
    )
    assert r4 == ['tc|bash|tid1|{"cmd":"ls"}']
    assert not acc.active


def test_text_block_stop_ignored():
    """content_block_stop on a text block emits nothing."""
    acc = _ToolCallAcc()  # acc.active is False (no prior tool_use start)
    r = _transform_line('{"type":"stream_event","event":{"type":"content_block_stop"}}', acc)
    assert r == []


def test_multiple_tool_calls_sequential():
    """Second tool call after first must work cleanly (acc reset between)."""
    acc = _ToolCallAcc()
    for name, id_ in [("bash", "t1"), ("read", "t2")]:
        _transform_line(
            f'{{"type":"stream_event","event":{{"type":"content_block_start","content_block":{{"type":"tool_use","name":"{name}","id":"{id_}"}}}}}}',
            acc,
        )
        _transform_line(
            '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"input_json_delta","partial_json":"{}"}}}',
            acc,
        )
        r = _transform_line('{"type":"stream_event","event":{"type":"content_block_stop"}}', acc)
        assert r == [f"tc|{name}|{id_}|{{}}"]


def test_input_json_delta_no_emit():
    """input_json_delta appends to acc.args but emits nothing."""
    acc = _ToolCallAcc()
    acc.active = True
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"input_json_delta","partial_json":"part"}}}',
        acc,
    )
    assert acc.args == ["part"]


# ── control_request ───────────────────────────────────────────────────────────

def test_permission_prompt_can_use_tool():
    line = json.dumps({"type": "control_request", "request": {
        "subtype": "can_use_tool", "request_id": "r1",
        "tool_name": "bash", "input": {"cmd": "ls"},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("pp|bash|r1|") and '"cmd"' in r[0]


def test_ask_user_question():
    line = json.dumps({"type": "control_request", "request": {
        "subtype": "can_use_tool", "request_id": "r2",
        "tool_name": "AskUserQuestion", "input": {"question": "OK?"},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("au|r2|")


def test_permission_hook_callback():
    line = json.dumps({"type": "control_request", "request_id": "top_rid", "request": {
        "subtype": "hook_callback",
        "input": {"tool_name": "bash", "tool_input": {"cmd": "ls"}},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("pp|bash|top_rid|")


def test_permission_elicitation():
    line = json.dumps({"type": "control_request", "request": {
        "subtype": "elicitation", "request_id": "e1",
        "elicitation": {"prompt": "Confirm?"},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("au|e1|")


def test_control_request_permission_subtype():
    line = json.dumps({"type": "control_request", "request": {
        "subtype": "permission", "request_id": "p1",
        "tool_name": "bash", "tool_input": {"cmd": "rm -rf"},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("pp|bash|p1|")


def test_control_request_unknown_subtype():
    line = json.dumps({"type": "control_request", "request": {"subtype": "mcp_message"}})
    assert _transform_line(line, _ToolCallAcc()) == []


def test_sdk_control_request_routed():
    """sdk_control_request is treated same as control_request."""
    line = json.dumps({"type": "sdk_control_request", "request": {
        "subtype": "can_use_tool", "request_id": "r3",
        "tool_name": "bash", "input": {},
    }})
    r = _transform_line(line, _ToolCallAcc())
    assert len(r) == 1 and r[0].startswith("pp|bash|r3|")


# ── misc event types ──────────────────────────────────────────────────────────

def test_rate_limit():
    assert _transform_line('{"type":"rate_limit_event","message":"retry in 5s"}', _ToolCallAcc()) == ["rl|retry in 5s"]


def test_tool_progress():
    assert _transform_line('{"type":"tool_progress","percentage":50.0,"message":"Running..."}', _ToolCallAcc()) == ["tp|50.0|Running..."]


def test_session_state():
    assert _transform_line('{"type":"session_state_changed","state":"active"}', _ToolCallAcc()) == ["ss|active"]


# ── edge cases ───────────────────────────────────────────────────────────────

def test_malformed_json():
    assert _transform_line("not json at all", _ToolCallAcc()) == []


def test_empty_line():
    assert _transform_line("", _ToolCallAcc()) == []


def test_whitespace_only():
    assert _transform_line("   ", _ToolCallAcc()) == []


def test_unknown_type_forward_compat():
    assert _transform_line('{"type":"some_future_type","data":"x"}', _ToolCallAcc()) == []


def test_assistant_ignored():
    assert _transform_line('{"type":"assistant","message":{}}', _ToolCallAcc()) == []


def test_user_ignored():
    assert _transform_line('{"type":"user","message":{}}', _ToolCallAcc()) == []


# ─── stream_transform monkey (5 tests) ───────────────────────────────────────

def test_transform_whitespace_tab_newline():
    """E02: whitespace-only with tab and newline → [] (stripped, not JSON-parseable)."""
    assert _transform_line("   \t\n", _ToolCallAcc()) == []


def test_transform_text_delta_unicode():
    """E04: Unicode text (CJK) passes through unchanged in pipe format."""
    line = json.dumps({
        "type": "stream_event",
        "event": {"type": "content_block_delta", "delta": {"type": "text_delta", "text": "日本語"}},
    })
    assert _transform_line(line, _ToolCallAcc()) == ["t|日本語"]


def test_transform_enormous_tool_args():
    """E06: 500KB of partial_json across 10 chunks → complete_args preserved."""
    acc = _ToolCallAcc()
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start","content_block":{"type":"tool_use","name":"write","id":"id1"}}}',
        acc,
    )
    chunk = "x" * 50_000
    for _ in range(10):
        _transform_line(
            json.dumps({"type": "stream_event", "event": {"type": "content_block_delta",
                        "delta": {"type": "input_json_delta", "partial_json": chunk}}}),
            acc,
        )
    r = _transform_line('{"type":"stream_event","event":{"type":"content_block_stop"}}', acc)
    assert len(r) == 1
    assert r[0].startswith("tc|write|id1|")
    assert len(r[0]) > 500_000  # all 500K x's preserved
    assert not acc.active  # acc reset after stop


def test_transform_result_no_optional_fields():
    """E08: result with only is_error=false → d||0|0|0 (all optional fields default)."""
    assert _transform_line('{"type":"result","is_error":false}', _ToolCallAcc()) == ["d||0|0|0"]


def test_transform_hook_callback_ask_user():
    """E10: sdk_control_request hook_callback with AskUserQuestion → au|rid|tool_input."""
    line = json.dumps({
        "type": "sdk_control_request",
        "request_id": "h1",
        "request": {
            "subtype": "hook_callback",
            "input": {"tool_name": "AskUserQuestion", "tool_input": {"prompt": "ok?"}},
        },
    })
    r = _transform_line(line, _ToolCallAcc())
    assert r == ['au|h1|{"prompt":"ok?"}']


# ── _transform_plain_text_line ────────────────────────────────────────────────

def test_plain_text_wraps_as_t():
    assert _transform_plain_text_line("Hello world", _ToolCallAcc()) == ["t|Hello world"]


def test_plain_text_empty_returns_empty():
    assert _transform_plain_text_line("", _ToolCallAcc()) == []


def test_plain_text_whitespace_only_returns_empty():
    assert _transform_plain_text_line("  \n  ", _ToolCallAcc()) == []


def test_plain_text_strips_surrounding_whitespace():
    assert _transform_plain_text_line("  hello  ", _ToolCallAcc()) == ["t|hello"]


def test_plain_text_preserves_pipe_chars():
    """Pipe inside content is fine — RelayEventParser splits only on first pipe."""
    assert _transform_plain_text_line("a|b|c", _ToolCallAcc()) == ["t|a|b|c"]


def test_plain_text_acc_unused():
    """acc parameter accepted but not used; result is purely line-based."""
    acc = _ToolCallAcc()
    acc.active = True
    assert _transform_plain_text_line("text", acc) == ["t|text"]


# ── _transform_codex_line (real Codex CLI NDJSON format) ─────────────────────

def test_codex_agent_message():
    line = json.dumps({
        "type": "item.completed",
        "item": {"id": "item_0", "type": "agent_message", "text": "Hello."},
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ["t|Hello."]


def test_codex_agent_message_empty_text():
    line = json.dumps({
        "type": "item.completed",
        "item": {"id": "item_1", "type": "agent_message", "text": ""},
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == []


def test_codex_tool_call_item():
    line = json.dumps({
        "type": "item.completed",
        "item": {
            "type": "tool_call",
            "id": "tc_1",
            "call_id": "call_abc",
            "name": "bash",
            "arguments": '{"cmd":"ls"}',
        },
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ['tc|bash|call_abc|{"cmd":"ls"}']


def test_codex_tool_call_item_dict_args():
    """arguments as dict (not string) gets JSON-serialized."""
    line = json.dumps({
        "type": "item.completed",
        "item": {
            "type": "tool_call",
            "call_id": "call_x",
            "name": "read",
            "arguments": {"path": "/tmp/f"},
        },
    })
    result = _transform_codex_line(line, _ToolCallAcc())
    assert result == ['tc|read|call_x|{"path":"/tmp/f"}']


def test_codex_function_call_item_mcp():
    """Codex emits MCP tool calls as function_call, not tool_call."""
    line = json.dumps({
        "type": "item.completed",
        "item": {
            "type": "function_call",
            "call_id": "call_xyz",
            "name": "get_hierarchy",
            "arguments": "{}",
        },
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ["tc|get_hierarchy|call_xyz|{}"]


def test_codex_mcp_tool_call_started():
    """item.started with mcp_tool_call emits tc| chip immediately."""
    line = json.dumps({
        "type": "item.started",
        "item": {
            "id": "item_2",
            "type": "mcp_tool_call",
            "server": "unity",
            "tool": "get_hierarchy",
            "arguments": {"full": True},
            "result": None,
            "status": "in_progress",
        },
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ['tc|get_hierarchy|item_2|{"full":true}']


def test_codex_mcp_tool_call_completed():
    """item.completed with mcp_tool_call emits tr| result."""
    line = json.dumps({
        "type": "item.completed",
        "item": {
            "id": "item_2",
            "type": "mcp_tool_call",
            "server": "unity",
            "tool": "get_hierarchy",
            "arguments": {"full": True},
            "result": {"content": [{"type": "text", "text": "Main Camera\nGridFloor"}]},
            "status": "completed",
        },
    })
    result = _transform_codex_line(line, _ToolCallAcc())
    assert result == ["tr|item_2|true|Main Camera\nGridFloor"]


def test_codex_item_started_non_mcp_ignored():
    """item.started for non-mcp_tool_call types is ignored."""
    line = json.dumps({
        "type": "item.started",
        "item": {"id": "item_1", "type": "agent_message", "text": ""},
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == []


def test_codex_item_unknown_type_ignored():
    """Non-agent_message, non-tool_call item types are silently ignored."""
    line = json.dumps({
        "type": "item.completed",
        "item": {"type": "reasoning", "text": "thinking..."},
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == []


def test_codex_turn_completed_with_usage():
    line = json.dumps({
        "type": "turn.completed",
        "usage": {
            "input_tokens": 9724,
            "cached_input_tokens": 4992,
            "output_tokens": 22,
            "reasoning_output_tokens": 14,
        },
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ["d||0|9724|36"]


def test_codex_turn_completed_no_usage():
    line = json.dumps({"type": "turn.completed"})
    assert _transform_codex_line(line, _ToolCallAcc()) == ["d||0|0|0"]


def test_codex_turn_completed_includes_reasoning_tokens():
    line = json.dumps({
        "type": "turn.completed",
        "usage": {
            "input_tokens": 5000,
            "output_tokens": 100,
            "reasoning_output_tokens": 50,
            "cached_input_tokens": 2000,
        }
    })
    result = _transform_codex_line(line, _ToolCallAcc())
    assert result == ["d||0|5000|150"]


def test_codex_turn_completed_no_reasoning_field():
    line = json.dumps({
        "type": "turn.completed",
        "usage": {"input_tokens": 5000, "output_tokens": 100}
    })
    result = _transform_codex_line(line, _ToolCallAcc())
    assert result == ["d||0|5000|100"]


def test_codex_turn_completed_null_tokens():
    """or-0 guard prevents None + int crash when JSON has explicit null."""
    line = json.dumps({
        "type": "turn.completed",
        "usage": {"input_tokens": None, "output_tokens": None, "reasoning_output_tokens": None},
    })
    assert _transform_codex_line(line, _ToolCallAcc()) == ["d||0|0|0"]


def test_codex_error_event():
    line = json.dumps({"type": "error", "message": "stream error"})
    assert _transform_codex_line(line, _ToolCallAcc()) == ["e|stream error"]


def test_codex_thread_started_ignored():
    line = json.dumps({"type": "thread.started", "thread_id": "019f1247"})
    assert _transform_codex_line(line, _ToolCallAcc()) == []


def test_codex_turn_started_ignored():
    line = json.dumps({"type": "turn.started"})
    assert _transform_codex_line(line, _ToolCallAcc()) == []


def test_codex_malformed_json_fallback_to_text():
    assert _transform_codex_line("not json", _ToolCallAcc()) == ["t|not json"]


def test_codex_empty_line_returns_empty():
    assert _transform_codex_line("", _ToolCallAcc()) == []


def test_codex_whitespace_only_returns_empty():
    assert _transform_codex_line("  \n", _ToolCallAcc()) == []


# ── _transform_opencode_line (OpenCode run --format json) ────────────────────

def test_opencode_text_event_extracts_text():
    line = json.dumps({
        "type": "text",
        "sessionID": "ses_abc",
        "part": {"type": "text", "text": "Hello world"},
    })
    assert _transform_opencode_line(line, _ToolCallAcc()) == ["t|Hello world"]


def test_opencode_text_event_empty_text_returns_empty():
    line = json.dumps({
        "type": "text",
        "sessionID": "ses_abc",
        "part": {"type": "text", "text": ""},
    })
    assert _transform_opencode_line(line, _ToolCallAcc()) == []


def test_opencode_step_finish_returns_done():
    line = json.dumps({
        "type": "step_finish",
        "sessionID": "ses_xyz",
        "part": {
            "type": "step-finish",
            "reason": "stop",
            "tokens": {"total": 100, "input": 90, "output": 10},
            "cost": 0.002,
        },
    })
    result = _transform_opencode_line(line, _ToolCallAcc())
    assert len(result) == 1
    assert result[0].startswith("d|ses_xyz|")
    parts = result[0].split("|")
    assert parts[2] == "0.002"
    assert parts[3] == "90"
    assert parts[4] == "10"


def test_opencode_step_finish_zero_cost():
    line = json.dumps({
        "type": "step_finish",
        "sessionID": "ses_0",
        "part": {"tokens": {"input": 5, "output": 2}, "cost": 0},
    })
    result = _transform_opencode_line(line, _ToolCallAcc())
    assert result[0] == "d|ses_0|0|5|2"


def test_opencode_error_event():
    line = json.dumps({
        "type": "error",
        "part": {"error": "rate limit exceeded"},
    })
    assert _transform_opencode_line(line, _ToolCallAcc()) == ["e|rate limit exceeded"]


def test_opencode_error_no_message_fallback():
    line = json.dumps({"type": "error", "part": {}})
    assert _transform_opencode_line(line, _ToolCallAcc()) == ["e|OpenCode error"]


def test_opencode_tool_start_emits_tc():
    line = json.dumps({
        "type": "tool_start",
        "part": {"name": "bash", "id": "tid_1", "input": {"cmd": "ls"}},
    })
    result = _transform_opencode_line(line, _ToolCallAcc())
    assert len(result) == 1
    assert result[0].startswith("tc|bash|tid_1|")
    assert '"cmd"' in result[0]


def test_opencode_tool_start_no_name_returns_empty():
    line = json.dumps({"type": "tool_start", "part": {"id": "tid_1"}})
    assert _transform_opencode_line(line, _ToolCallAcc()) == []


def test_opencode_step_start_ignored():
    line = json.dumps({"type": "step_start", "part": {}})
    assert _transform_opencode_line(line, _ToolCallAcc()) == []


def test_opencode_malformed_json_fallback_to_text():
    assert _transform_opencode_line("not json at all", _ToolCallAcc()) == ["t|not json at all"]


def test_opencode_empty_line_returns_empty():
    assert _transform_opencode_line("", _ToolCallAcc()) == []


def test_opencode_whitespace_only_returns_empty():
    assert _transform_opencode_line("   ", _ToolCallAcc()) == []


# ── _transform_kimi_line (Kimi -p --output-format stream-json) ───────────────

def test_kimi_assistant_text():
    line = '{"role":"assistant","content":"Hello! How can I help you today?"}'
    assert _transform_kimi_line(line, _ToolCallAcc()) == ["t|Hello! How can I help you today?"]


def test_kimi_assistant_empty_content():
    line = '{"role":"assistant","content":""}'
    assert _transform_kimi_line(line, _ToolCallAcc()) == []


def test_kimi_meta_resume_hint():
    line = '{"role":"meta","type":"session.resume_hint","session_id":"session_534cd057","command":"kimi -r session_534cd057","content":"To resume: kimi -r session_534cd057"}'
    result = _transform_kimi_line(line, _ToolCallAcc())
    assert result == ["d|session_534cd057|0|0|0"]


def test_kimi_empty_line():
    assert _transform_kimi_line("", _ToolCallAcc()) == []


def test_kimi_malformed_json_fallback():
    assert _transform_kimi_line("not json", _ToolCallAcc()) == ["t|not json"]


def test_kimi_unknown_role():
    line = '{"role":"system","content":"something"}'
    assert _transform_kimi_line(line, _ToolCallAcc()) == []


def test_kimi_meta_unknown_type():
    line = '{"role":"meta","type":"other_event"}'
    assert _transform_kimi_line(line, _ToolCallAcc()) == []


# ── barrier tests (Phase 0.4) ─────────────────────────────────────────────────

def test_unknown_content_block_type_ignored():
    """BARRIER: unknown content_block type → empty list, no exception.
    Locks current behavior before thinking support lands."""
    acc = _ToolCallAcc()
    result = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start",'
        '"content_block":{"type":"unknown_future_type"}}}',
        acc
    )
    assert result == [], f"Unknown block type must produce [], got: {result}"


def test_tool_call_produces_tc_pipe_prefix():
    """BARRIER: completed tool call emits 'tc|...' prefixed line.
    Locks tc| format before any new event types change the pipeline."""
    acc = _ToolCallAcc()
    # start
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start",'
        '"index":0,"content_block":{"type":"tool_use","id":"t1","name":"Bash"}}}',
        acc
    )
    # args delta
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_delta",'
        '"index":0,"delta":{"type":"input_json_delta","partial_json":"{\\"cmd\\":\\"ls\\"}"}}}',
        acc
    )
    # stop
    result = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_stop","index":0}}',
        acc
    )
    assert any(line.startswith("tc|") for line in result), \
        f"tool_call stop must emit tc| line, got: {result}"


# ── tool_result → tr| (T-2.9) ────────────────────────────────────────────────

def _tr_start(tool_use_id: str = "tr_123", is_error: bool = False) -> str:
    return json.dumps({"type": "stream_event", "event": {
        "type": "content_block_start",
        "content_block": {"type": "tool_result", "tool_use_id": tool_use_id, "is_error": is_error},
    }})


def _tr_delta(text: str) -> str:
    return json.dumps({"type": "stream_event", "event": {
        "type": "content_block_delta",
        "delta": {"type": "text_delta", "text": text},
    }})


_TR_STOP = '{"type":"stream_event","event":{"type":"content_block_stop"}}'


def test_tool_result_emits_tr_on_stop():
    acc = _ToolCallAcc()
    _transform_line(_tr_start("tr_123", is_error=False), acc)
    _transform_line(_tr_delta("OK result"), acc)
    r = _transform_line(_TR_STOP, acc)
    assert r == ["tr|tr_123|true|OK result"]


def test_tool_result_error_flag_false():
    acc = _ToolCallAcc()
    _transform_line(_tr_start("tr_456", is_error=True), acc)
    _transform_line(_tr_delta("error msg"), acc)
    r = _transform_line(_TR_STOP, acc)
    assert r == ["tr|tr_456|false|error msg"]


def test_tool_result_text_truncated_at_200():
    acc = _ToolCallAcc()
    _transform_line(_tr_start(), acc)
    _transform_line(_tr_delta("X" * 300), acc)
    r = _transform_line(_TR_STOP, acc)
    assert len(r) == 1
    assert len(r[0].split("|", 3)[3]) == 200


def test_tool_result_delta_not_emitted_directly():
    acc = _ToolCallAcc()
    _transform_line(_tr_start(), acc)
    r = _transform_line(_tr_delta("secret"), acc)
    assert r == []


def test_tool_result_no_id_no_tr():
    line = json.dumps({"type": "stream_event", "event": {
        "type": "content_block_start",
        "content_block": {"type": "tool_result"},  # no tool_use_id
    }})
    acc = _ToolCallAcc()
    _transform_line(line, acc)
    _transform_line(_tr_delta("some text"), acc)
    r = _transform_line(_TR_STOP, acc)
    assert r == []


def test_tool_result_empty_content_no_tr():
    acc = _ToolCallAcc()
    _transform_line(_tr_start(), acc)
    r = _transform_line(_TR_STOP, acc)  # no delta
    assert r == []


def test_tool_result_multiple_deltas_concatenated():
    acc = _ToolCallAcc()
    _transform_line(_tr_start("tr_123"), acc)
    _transform_line(_tr_delta("hello "), acc)
    _transform_line(_tr_delta("world"), acc)
    r = _transform_line(_TR_STOP, acc)
    assert r == ["tr|tr_123|true|hello world"]


def test_text_emits_normally_after_tool_result():
    acc = _ToolCallAcc()
    _transform_line(_tr_start(), acc)
    _transform_line(_TR_STOP, acc)  # close tool_result
    r = _transform_line(_tr_delta("after"), acc)
    assert r == ["t|after"]


# ── thinking blocks → th| (T-5.1) ────────────────────────────────────────────

_START_THINKING = (
    '{"type":"stream_event","event":{"type":"content_block_start",'
    '"content_block":{"type":"thinking","thinking":""}}}'
)


def _delta_thinking(text: str) -> str:
    return (
        f'{{"type":"stream_event","event":{{"type":"content_block_delta",'
        f'"delta":{{"type":"thinking_delta","thinking":"{text}"}}}}}}'
    )


def test_thinking_single_delta_emits_th_on_stop():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("Let me think..."), acc)
    out = _transform_line(_TR_STOP, acc)
    assert out == ["th|Let me think..."]


def test_thinking_multiple_deltas_concatenated():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("Hello "), acc)
    _transform_line(_delta_thinking("World"), acc)
    out = _transform_line(_TR_STOP, acc)
    assert out == ["th|Hello World"]


def test_thinking_empty_text_not_emitted():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    out = _transform_line(_TR_STOP, acc)
    assert out == []


def test_thinking_delta_without_start_ignored():
    acc = _ToolCallAcc()
    out = _transform_line(_delta_thinking("something"), acc)
    assert out == []


def test_thinking_followed_by_text_works():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("reasoning"), acc)
    _transform_line(_TR_STOP, acc)
    r = _transform_line(_tr_delta("Hello"), acc)
    assert r == ["t|Hello"]


def test_thinking_acc_reset_after_stop():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_TR_STOP, acc)
    assert acc.thinking_active is False
    assert acc.thinking_parts == []


def test_tool_call_after_thinking_unaffected():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("some reasoning"), acc)
    _transform_line(_TR_STOP, acc)
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start",'
        '"content_block":{"type":"tool_use","name":"Bash","id":"t1"}}}',
        acc,
    )
    assert acc.active is True
    assert acc.name == "Bash"


def test_thinking_second_block_isolated():
    acc = _ToolCallAcc()
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("First"), acc)
    _transform_line(_TR_STOP, acc)
    _transform_line(_START_THINKING, acc)
    _transform_line(_delta_thinking("Second"), acc)
    out = _transform_line(_TR_STOP, acc)
    assert out == ["th|Second"]


# ── B5 — error with empty body must not be swallowed ─────────────────────────

def test_tool_error_empty_body_emits_event():
    """B5: tool_result is_error=True with no text deltas must still emit tr event."""
    acc = _ToolCallAcc()
    _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_start",'
        '"content_block":{"type":"tool_result","tool_use_id":"tu-err","is_error":true}}}',
        acc,
    )
    # No content_block_delta — empty body (tool crashed without diagnostic text)
    result = _transform_line(
        '{"type":"stream_event","event":{"type":"content_block_stop"}}',
        acc,
    )
    assert result == ["tr|tu-err|false|"]
