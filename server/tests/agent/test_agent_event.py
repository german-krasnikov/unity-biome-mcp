"""Tests for AgentEvent + ProviderCapabilities models (T12)."""

import json
import uuid
from pathlib import Path

import jsonschema
import pytest

_REPO_ROOT = Path(__file__).parent.parent.parent.parent
_FIXTURES_DIR = _REPO_ROOT / "scripts" / "fixtures" / "agent-events"
_SCHEMA_PATH = _REPO_ROOT / "protocol" / "chat-relay" / "v2" / "agent-event.schema.json"

FIXTURE_FILES = [
    "normal-turn.jsonl",
    "multi-tool-turn.jsonl",
    "error-recovery.jsonl",
    "cancel-mid-turn.jsonl",
    "reconnect.jsonl",
    "permission-prompt.jsonl",
    "thought-events.jsonl",
    "plan-workflow.jsonl",
    "file-changes.jsonl",
    "cost-update.jsonl",
    "empty-turn.jsonl",
    "capabilities-changed.jsonl",
    "heartbeat.jsonl",
    "utf8-payloads.jsonl",
]


def _load_fixture(name: str) -> list[dict]:
    path = _FIXTURES_DIR / name
    lines = path.read_text(encoding="utf-8").strip().splitlines()
    return [json.loads(line) for line in lines if line.strip()]


# ── 1. Default fields ─────────────────────────────────────────────────────────

def test_default_fields():
    from unity_mcp.agent_event import AgentEvent
    e = AgentEvent()
    assert e.schema_version == 1
    parsed = uuid.UUID(e.event_id, version=4)
    assert str(parsed) == e.event_id
    assert "+" in e.timestamp or e.timestamp.endswith("Z")


# ── 2. Round-trip all fields ──────────────────────────────────────────────────

def test_all_envelope_fields():
    from unity_mcp.agent_event import AgentEvent
    raw = {
        "schema_version": 1,
        "event_id": "550e8400-e29b-41d4-a716-446655440000",
        "conversation_id": "conv-1",
        "session_id": "sess-1",
        "turn_id": 3,
        "sequence": 7,
        "timestamp": "2024-01-01T00:00:00+00:00",
        "kind": "turn_started",
        "payload": {"user_input": "hi"},
        "meta": {"source": "relay"},
    }
    e = AgentEvent.model_validate(raw)
    dumped = e.model_dump()
    assert dumped["turn_id"] == 3
    assert dumped["sequence"] == 7
    assert dumped["kind"] == "turn_started"
    assert dumped["payload"] == {"user_input": "hi"}
    assert dumped["meta"] == {"source": "relay"}
    e2 = AgentEvent.model_validate(dumped)
    assert e == e2


# ── 3. Unknown kind preserved ─────────────────────────────────────────────────

def test_unknown_kind_preserved():
    from unity_mcp.agent_event import AgentEvent
    e = AgentEvent.model_validate({"kind": "future_unknown_kind", "sequence": 1})
    assert e.kind == "future_unknown_kind"


# ── 4. Unknown top-level field preserved ──────────────────────────────────────

def test_unknown_top_level_field_preserved():
    from unity_mcp.agent_event import AgentEvent
    raw = {"kind": "heartbeat", "sequence": 1, "foo": "bar", "extra_int": 42}
    e = AgentEvent.model_validate(raw)
    dumped = e.model_dump()
    assert dumped.get("foo") == "bar"
    assert dumped.get("extra_int") == 42


# ── 5. Monotonic sequence per fixture ────────────────────────────────────────

def test_monotonic_sequence_fixture():
    for fname in FIXTURE_FILES:
        events = _load_fixture(fname)
        seqs = [e["sequence"] for e in events]
        for i in range(1, len(seqs)):
            assert seqs[i] > seqs[i - 1], (
                f"{fname}: sequence not monotonic at index {i}: {seqs[i-1]} → {seqs[i]}"
            )


# ── 6. Same conversation_id per fixture ──────────────────────────────────────

def test_same_conversation_id_fixture():
    for fname in FIXTURE_FILES:
        events = _load_fixture(fname)
        ids = {e["conversation_id"] for e in events}
        assert len(ids) == 1, f"{fname}: multiple conversation_ids: {ids}"


# ── 7. Tool call started payload shape ───────────────────────────────────────

def test_tool_call_payload_shape():
    from unity_mcp.agent_event import AgentEvent
    e = AgentEvent(
        kind="tool_call_started",
        sequence=1,
        payload={"name": "execute_code", "id": "tc-1", "args": {"code": "print()"}},
    )
    assert e.payload["name"] == "execute_code"
    assert e.payload["id"] == "tc-1"
    assert "args" in e.payload


# ── 8. Tool call failed payload shape ────────────────────────────────────────

def test_tool_call_failed_payload_shape():
    from unity_mcp.agent_event import AgentEvent
    e = AgentEvent(
        kind="tool_call_failed",
        sequence=1,
        payload={"id": "tc-1", "error": "timeout"},
    )
    assert e.payload["id"] == "tc-1"
    assert e.payload["error"] == "timeout"


# ── 9. Cost update payload shape ─────────────────────────────────────────────

def test_cost_update_payload_shape():
    from unity_mcp.agent_event import AgentEvent
    e = AgentEvent(
        kind="cost_update",
        sequence=1,
        payload={"cost_usd": 0.001, "input_tokens": 100, "output_tokens": 50},
    )
    assert e.payload["cost_usd"] == pytest.approx(0.001)
    assert e.payload["input_tokens"] == 100
    assert e.payload["output_tokens"] == 50


# ── 10. UTF-8 payload round-trip ─────────────────────────────────────────────

def test_utf8_payload_roundtrip():
    from unity_mcp.agent_event import AgentEvent
    text = "こんにちは مرحبا 🎮"
    e = AgentEvent(kind="assistant_delta", sequence=1, payload={"text": text})
    serialized = json.dumps(e.model_dump(), ensure_ascii=False)
    e2 = AgentEvent.model_validate(json.loads(serialized))
    assert e2.payload["text"] == text


# ── 11. Empty turn has no delta events ───────────────────────────────────────

def test_empty_turn_fixture():
    events = _load_fixture("empty-turn.jsonl")
    kinds = [e["kind"] for e in events]
    assert "assistant_delta" not in kinds
    assert "thought_delta" not in kinds


# ── 12. Thought events fixture has thought_delta ─────────────────────────────

def test_thought_events_fixture():
    events = _load_fixture("thought-events.jsonl")
    kinds = [e["kind"] for e in events]
    assert "thought_delta" in kinds


# ── 13. All fixtures parse without ValidationError ───────────────────────────

def _validate_event(raw: dict, fname: str, i: int) -> str | None:
    from pydantic import ValidationError

    from unity_mcp.agent_event import AgentEvent
    try:
        AgentEvent.model_validate(raw)
        return None
    except ValidationError as exc:
        return f"{fname} line {i+1}: {exc}"


def _validate_schema(raw: dict, schema: dict, fname: str, i: int) -> str | None:
    try:
        jsonschema.validate(instance=raw, schema=schema)
        return None
    except jsonschema.ValidationError as exc:
        return f"{fname} line {i+1}: {exc.message}"


def test_all_fixtures_parse():
    errors = [
        err
        for fname in FIXTURE_FILES
        for i, raw in enumerate(_load_fixture(fname))
        if (err := _validate_event(raw, fname, i)) is not None
    ]
    if errors:
        pytest.fail("\n".join(errors))


# ── 14. All fixtures validate against JSON Schema ────────────────────────────

def test_all_fixtures_schema_conformance():
    schema = json.loads(_SCHEMA_PATH.read_text(encoding="utf-8"))
    errors = [
        err
        for fname in FIXTURE_FILES
        for i, raw in enumerate(_load_fixture(fname))
        if (err := _validate_schema(raw, schema, fname, i)) is not None
    ]
    if errors:
        pytest.fail("\n".join(errors))


# ── 15. Schema drift detection ───────────────────────────────────────────────

def test_schema_drift():
    from unity_mcp.agent_event import AgentEvent
    generated = AgentEvent.model_json_schema()
    committed = json.loads(_SCHEMA_PATH.read_text(encoding="utf-8"))
    assert generated == committed, (
        "Schema drift detected. Run: python scripts/export_agent_schema.py --write"
    )


# ── 16. ProviderCapabilities claude includes thought_delta ───────────────────

def test_provider_capabilities_from_probe_claude():
    from unity_mcp.agent_event import ProviderCapabilities
    probe = {
        "has_resume": True, "has_cancel": False,
        "has_modes": ["ask", "agent"], "binary_version": "1.5.0",
    }
    caps = ProviderCapabilities.from_probe("claude", probe)
    assert "thought_delta" in caps.events
    assert caps.provider_id == "claude"
    assert caps.session["has_resume"] is True
    assert caps.session["binary_version"] == "1.5.0"


# ── 17. ProviderCapabilities kimi excludes thought_delta ─────────────────────

def test_provider_capabilities_from_probe_kimi():
    from unity_mcp.agent_event import ProviderCapabilities
    probe = {"has_resume": False, "has_modes": [], "binary_version": None}
    caps = ProviderCapabilities.from_probe("kimi", probe)
    assert "thought_delta" not in caps.events
    assert caps.provider_id == "kimi"


# ── 18. from_probe maps has_modes correctly ──────────────────────────────────

def test_provider_capabilities_modes():
    from unity_mcp.agent_event import ProviderCapabilities
    probe = {"has_resume": True, "has_modes": ["ask", "agent", "auto"], "binary_version": "2.0"}
    caps = ProviderCapabilities.from_probe("claude", probe)
    assert caps.modes == ["ask", "agent", "auto"]


# ── 19. Permission keys map to the right modes ──────────────────────────────

def test_provider_capabilities_permissions():
    from unity_mcp.agent_event import ProviderCapabilities
    probe = {"has_resume": True, "has_modes": ["ask", "agent"], "binary_version": "1.0"}
    caps = ProviderCapabilities.from_probe("claude", probe)
    assert caps.permissions["has_plan_mode"] is True
    assert caps.permissions["has_agent_mode"] is True

    # Discriminating: ask-only probe → plan mode True, agent mode False
    probe_ask_only = {"has_resume": True, "has_modes": ["ask"], "binary_version": "1.0"}
    caps_ask = ProviderCapabilities.from_probe("claude", probe_ask_only)
    assert caps_ask.permissions["has_plan_mode"] is True
    assert caps_ask.permissions["has_agent_mode"] is False


# ── 20. Unknown provider defaults to all event kinds ─────────────────────────

def test_provider_capabilities_unknown_provider():
    from unity_mcp.agent_event import _ALL_KIND_LIST, ProviderCapabilities
    probe = {"has_resume": False, "has_modes": [], "binary_version": None}
    caps = ProviderCapabilities.from_probe("unknown-future-provider", probe)
    assert caps.events == _ALL_KIND_LIST
