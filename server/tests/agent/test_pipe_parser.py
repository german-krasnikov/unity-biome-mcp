"""Tests for pipe_parser.parse_pipe_string() — pure function, no mocks. (17 tests)"""
from __future__ import annotations

from unity_mcp.adapters.pipe_parser import parse_pipe_string
from unity_mcp.adapters.protocol import EventContext


def _ctx(**kw) -> EventContext:
    defaults = {"conversation_id": "conv-1", "session_id": "sess-1", "turn_id": 1, "sequence": 0}
    defaults.update(kw)
    return EventContext(**defaults)


def test_parse_t_prefix_returns_assistant_delta():
    evts = parse_pipe_string("t|hello", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "assistant_delta"
    assert evts[0].payload["text"] == "hello"


def test_parse_th_prefix_returns_thought_delta():
    evts = parse_pipe_string("th|thinking...", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "thought_delta"
    assert evts[0].payload["text"] == "thinking..."


def test_parse_tc_prefix_returns_tool_call_started():
    evts = parse_pipe_string('tc|my_tool|id-1|{"k":"v"}', _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "tool_call_started"
    assert e.payload["name"] == "my_tool"
    assert e.payload["id"] == "id-1"
    assert e.payload["args"] == {"k": "v"}


def test_parse_tr_true_returns_tool_call_completed():
    evts = parse_pipe_string("tr|id-1|true|some result", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "tool_call_completed"
    assert evts[0].payload["id"] == "id-1"
    assert evts[0].payload["result"] == "some result"


def test_parse_tr_false_returns_tool_call_failed():
    evts = parse_pipe_string("tr|id-1|false|error msg", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "tool_call_failed"
    assert evts[0].payload["id"] == "id-1"
    assert evts[0].payload["error"] == "error msg"


def test_parse_d_returns_two_events():
    evts = parse_pipe_string("d||0|100|50", _ctx())
    assert len(evts) == 2


def test_parse_d_cost_event_has_tokens():
    evts = parse_pipe_string("d||0|100|50", _ctx())
    cost = next(e for e in evts if e.kind == "cost_update")
    assert cost.payload["input_tokens"] == 100
    assert cost.payload["output_tokens"] == 50


def test_parse_d_turn_completed_has_no_payload():
    evts = parse_pipe_string("d||0|100|50", _ctx())
    turn = next(e for e in evts if e.kind == "turn_completed")
    assert turn.payload == {}


def test_parse_e_returns_error():
    evts = parse_pipe_string("e|fail msg", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "error"
    assert evts[0].payload["message"] == "fail msg"


def test_parse_si_returns_session_started():
    evts = parse_pipe_string("si|sess-1", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "session_started"
    assert evts[0].payload["provider_session_id"] == "sess-1"


def test_parse_pp_returns_permission_requested():
    evts = parse_pipe_string("pp|my_tool|req-1|{}", _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "permission_requested"
    assert e.payload["tool_name"] == "my_tool"
    assert e.payload["request_id"] == "req-1"
    assert "is_ask_user" not in e.payload


def test_parse_au_returns_permission_requested_ask_user():
    evts = parse_pipe_string("au|req-1|{}", _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "permission_requested"
    assert e.payload["request_id"] == "req-1"
    assert e.payload.get("is_ask_user") is True


def test_parse_ss_returns_capabilities_changed():
    evts = parse_pipe_string("ss|plan", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "capabilities_changed"
    assert evts[0].payload["state"] == "plan"


def test_parse_rl_returns_warning_rate_limit():
    evts = parse_pipe_string("rl|rate limited", _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "warning"
    assert evts[0].payload["code"] == "rate_limit"


def test_parse_unknown_prefix_returns_empty():
    evts = parse_pipe_string("xyz|data", _ctx())
    assert evts == []


def test_parse_sequence_attached_from_context():
    evts = parse_pipe_string("t|hello", _ctx(sequence=7))
    assert evts[0].sequence == 7


def test_parse_tp_prefix_returns_warning_with_progress():
    evts = parse_pipe_string("tp|42.5|processing assets", _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "warning"
    assert e.payload["code"] == "tool_progress"
    assert e.payload["progress_pct"] == 42.5
    assert e.payload["message"] == "processing assets"


def test_parse_conversation_id_attached():
    evts = parse_pipe_string("t|hello", _ctx(conversation_id="conv-42"))
    assert evts[0].conversation_id == "conv-42"
