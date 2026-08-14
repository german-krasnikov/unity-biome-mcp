"""T23: HistoryManager unit tests."""
from __future__ import annotations

from pathlib import Path


def _make_event(kind: str, payload: dict | None = None):
    from unity_mcp.agent_event import AgentEvent

    return AgentEvent(kind=kind, payload=payload or {})


def _make_manager(tmp_path: Path):
    from unity_mcp.history.manager import HistoryManager

    return HistoryManager(tmp_path / "history", "testfp")


def test_observe_turn_started_extracts_title(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    mgr.observe(_make_event("turn_started", {"text": "How do I create a health system?"}))
    assert mgr._header.title == "How do I create a health system?"


def test_observe_turn_completed_increments_turn_count(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    mgr.observe(_make_event("turn_started", {"text": "Hello"}))
    mgr.observe(_make_event("turn_completed"))
    assert mgr._header.turn_count == 1


def test_observe_excluded_kind_writes_nothing(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    mgr.observe(_make_event("heartbeat"))
    mgr.observe(_make_event("cost_update"))
    # No JSONL file created
    assert not mgr._store.jsonl_path.exists()


def test_observe_session_started_captures_session_id(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    mgr.observe(_make_event("session_started", {"session_id": "sess_abc"}))
    # session_id captured on session_started event
    assert mgr._session_id == "sess_abc"


def test_current_conv_id_returns_none_before_open(tmp_path):
    mgr = _make_manager(tmp_path)
    assert mgr.current_conv_id() is None


def test_current_conv_id_returns_id_after_open(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    assert mgr.current_conv_id() is not None


def test_title_not_overwritten_by_second_turn(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    mgr.observe(_make_event("turn_started", {"text": "First question"}))
    mgr.observe(_make_event("turn_completed"))
    mgr.observe(_make_event("turn_started", {"text": "Second question"}))
    assert mgr._header.title == "First question"


def test_ensure_history_manager_reinits_on_fingerprint_change(monkeypatch):
    """ensure_history_manager() must replace the manager when fingerprint changes.
    This is the guard used in chat_relay.py (M12 fix)."""
    import unity_mcp.history.manager as hm_mod

    monkeypatch.setattr(hm_mod, "_manager", None)

    hm_mod.init_history_manager("fp_project_a")
    mgr_a = hm_mod.get_history_manager()
    assert mgr_a._fingerprint == "fp_project_a"

    # ensure_history_manager with a different fingerprint must replace the manager
    hm_mod.ensure_history_manager("fp_project_b")

    mgr_b = hm_mod.get_history_manager()
    assert mgr_b._fingerprint == "fp_project_b"
    assert mgr_b is not mgr_a


def test_ensure_history_manager_noop_same_fingerprint(monkeypatch):
    """ensure_history_manager() must NOT replace the manager if fingerprint matches."""
    import unity_mcp.history.manager as hm_mod

    monkeypatch.setattr(hm_mod, "_manager", None)

    hm_mod.init_history_manager("fp_stable")
    mgr_a = hm_mod.get_history_manager()

    hm_mod.ensure_history_manager("fp_stable")
    assert hm_mod.get_history_manager() is mgr_a


def test_title_truncated_to_80_chars(tmp_path):
    mgr = _make_manager(tmp_path)
    mgr.open_conversation("Claude")
    long_text = "A" * 100
    mgr.observe(_make_event("turn_started", {"text": long_text}))
    assert len(mgr._header.title) == 80
    assert mgr._header.title == "A" * 80
