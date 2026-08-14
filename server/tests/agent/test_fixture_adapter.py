"""Tests for FixtureAdapter — replays T12 JSONL fixtures. (7 tests)"""
from __future__ import annotations

from pathlib import Path

import pytest

from unity_mcp.adapters.fixture import FixtureAdapter
from unity_mcp.adapters.protocol import AgentAdapter

_REPO_ROOT = Path(__file__).parent.parent.parent.parent
_FIXTURES = _REPO_ROOT / "scripts" / "fixtures" / "agent-events"

_ALL_FIXTURE_FILES = [
    "normal-turn.jsonl", "multi-tool-turn.jsonl", "error-recovery.jsonl",
    "cancel-mid-turn.jsonl", "reconnect.jsonl", "permission-prompt.jsonl",
    "thought-events.jsonl", "plan-workflow.jsonl", "file-changes.jsonl",
    "cost-update.jsonl", "empty-turn.jsonl", "capabilities-changed.jsonl",
    "heartbeat.jsonl", "utf8-payloads.jsonl",
]


async def _events(name: str) -> list:
    return [e async for e in FixtureAdapter(_FIXTURES / name).events()]


async def test_fixture_adapter_normal_turn_event_count():
    evts = await _events("normal-turn.jsonl")
    assert len(evts) == 8


async def test_fixture_adapter_normal_turn_first_kind():
    evts = await _events("normal-turn.jsonl")
    assert evts[0].kind == "turn_started"


async def test_fixture_adapter_normal_turn_last_kind():
    evts = await _events("normal-turn.jsonl")
    assert evts[-1].kind == "cost_update"


async def test_fixture_adapter_multi_tool_two_tool_starts():
    evts = await _events("multi-tool-turn.jsonl")
    starts = [e for e in evts if e.kind == "tool_call_started"]
    assert len(starts) == 2


async def test_fixture_adapter_error_recovery_has_error_kind():
    evts = await _events("error-recovery.jsonl")
    assert any(e.kind == "error" for e in evts)


async def _safe_events(fname: str) -> str | None:
    """Return error string or None if fixture parses cleanly."""
    try:
        evts = await _events(fname)
        if len(evts) == 0:
            return f"{fname}: no events parsed"
    except Exception as exc:  # noqa: BLE001
        return f"{fname}: {exc}"
    return None


async def test_fixture_adapter_all_14_fixtures_parseable():
    errors = [e for fname in _ALL_FIXTURE_FILES if (e := await _safe_events(fname))]
    if errors:
        pytest.fail("\n".join(errors))


async def test_fixture_adapter_satisfies_protocol():
    adapter = FixtureAdapter(_FIXTURES / "normal-turn.jsonl")
    assert isinstance(adapter, AgentAdapter)
