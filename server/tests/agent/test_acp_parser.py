"""Tests for acp_parser.parse_acp_line() — pure function, no mocks. (11 tests)"""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from unity_mcp.adapters.acp_parser import parse_acp_line
from unity_mcp.adapters.protocol import EventContext

_FIXTURES = Path(__file__).parent / "fixtures" / "acp"


def _ctx(**kw) -> EventContext:
    defaults = {"conversation_id": "conv-1", "session_id": "sess-1", "turn_id": 1, "sequence": 0}
    defaults.update(kw)
    return EventContext(**defaults)


def _line(**fields) -> str:
    return json.dumps(fields)


# ── session/update: text ──────────────────────────────────────────────────────

def test_parse_text_delta():
    line = _line(type="session/update", content={"type": "text", "text": "hello"})
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "assistant_delta"
    assert evts[0].payload["text"] == "hello"


# ── session/update: thinking ──────────────────────────────────────────────────

def test_parse_thinking_delta():
    line = _line(type="session/update", content={"type": "thinking", "text": "hmm..."})
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "thought_delta"
    assert evts[0].payload["text"] == "hmm..."


# ── session/update: tool_call ─────────────────────────────────────────────────

def test_parse_tool_call_started():
    line = _line(type="session/update", content={
        "type": "tool_call", "name": "search", "id": "tc-1", "args": {"q": "test"},
    })
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "tool_call_started"
    assert e.payload["name"] == "search"
    assert e.payload["id"] == "tc-1"
    assert e.payload["args"] == {"q": "test"}


# ── session/update: tool_result ok ───────────────────────────────────────────

def test_parse_tool_result_ok():
    line = _line(type="session/update", content={
        "type": "tool_result", "id": "tc-1", "ok": True, "result": "done",
    })
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "tool_call_completed"
    assert evts[0].payload["id"] == "tc-1"
    assert evts[0].payload["result"] == "done"


# ── session/update: tool_result error ────────────────────────────────────────

def test_parse_tool_result_error():
    line = _line(type="session/update", content={
        "type": "tool_result", "id": "tc-1", "ok": False, "error": "boom",
    })
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "tool_call_failed"
    assert evts[0].payload["id"] == "tc-1"
    assert evts[0].payload["error"] == "boom"


# ── session/complete ──────────────────────────────────────────────────────────

def test_parse_session_complete_emits_two_events():
    line = _line(type="session/complete", cost_usd=0.001, input_tokens=100, output_tokens=50)
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 2
    kinds = [e.kind for e in evts]
    assert kinds == ["cost_update", "turn_completed"]


def test_parse_session_complete_cost_payload():
    line = _line(type="session/complete", cost_usd=0.005, input_tokens=200, output_tokens=75)
    evts = parse_acp_line(line, _ctx())
    cost = next(e for e in evts if e.kind == "cost_update")
    assert cost.payload["cost_usd"] == pytest.approx(0.005)
    assert cost.payload["input_tokens"] == 200
    assert cost.payload["output_tokens"] == 75


# ── session/error ─────────────────────────────────────────────────────────────

def test_parse_session_error():
    line = _line(type="session/error", message="something failed")
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "error"
    assert evts[0].payload["message"] == "something failed"


# ── session/request_permission ────────────────────────────────────────────────

def test_parse_permission_request():
    line = _line(
        type="session/request_permission",
        tool_name="run_tests", request_id="req-1", input={"mode": "EditMode"},
    )
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    e = evts[0]
    assert e.kind == "permission_requested"
    assert e.payload["tool_name"] == "run_tests"
    assert e.payload["request_id"] == "req-1"
    assert e.payload["input"] == {"mode": "EditMode"}


# ── session/update: tool_result missing ok → fail-closed ─────────────────────

def test_parse_tool_result_missing_ok_defaults_to_failed():
    line = _line(type="session/update", content={
        "type": "tool_result", "id": "tc-2", "error": "oops",
    })
    evts = parse_acp_line(line, _ctx())
    assert len(evts) == 1
    assert evts[0].kind == "tool_call_failed"


# ── unknown / empty / invalid ─────────────────────────────────────────────────

def test_parse_unknown_type_returns_empty():
    line = _line(type="session/unknown_future_event", data="x")
    assert parse_acp_line(line, _ctx()) == []


def test_parse_empty_line_returns_empty():
    assert parse_acp_line("", _ctx()) == []
    assert parse_acp_line("   ", _ctx()) == []


def test_parse_invalid_json_returns_empty():
    assert parse_acp_line("{not valid json", _ctx()) == []
    assert parse_acp_line("null", _ctx()) == []
