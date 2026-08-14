"""T23: HistoryStore unit tests."""
from __future__ import annotations

import json
import stat
from pathlib import Path


def _make_event(kind: str = "turn_started", text: str = "hello") -> object:
    from unity_mcp.agent_event import AgentEvent

    return AgentEvent(kind=kind, payload={"text": text})


def _make_header(conv_id: str = "c1"):
    from unity_mcp.history.models import ConversationHeader

    return ConversationHeader(
        conv_id=conv_id, title="T", created_at="c", updated_at="u",
        backend="Claude", session_id="", turn_count=1, fingerprint="fp",
    )


def _make_store(tmp_path: Path, conv_id: str = "c1"):
    from unity_mcp.history.store import HistoryStore

    conv_dir = tmp_path / "history"
    conv_dir.mkdir(parents=True, exist_ok=True)
    return HistoryStore(conv_dir, conv_id)


def test_append_event_writes_jsonl_line(tmp_path):
    store = _make_store(tmp_path)
    evt = _make_event()
    store.append_event(evt)
    lines = store.jsonl_path.read_text(encoding="utf-8").splitlines()
    assert len(lines) == 1
    data = json.loads(lines[0])
    assert data["kind"] == "turn_started"


def test_append_event_multiple_lines(tmp_path):
    store = _make_store(tmp_path)
    store.append_event(_make_event("turn_started"))
    store.append_event(_make_event("assistant_delta"))
    store.append_event(_make_event("turn_completed"))
    lines = store.jsonl_path.read_text(encoding="utf-8").splitlines()
    assert len(lines) == 3


def test_flush_header_creates_meta_json(tmp_path):
    store = _make_store(tmp_path)
    header = _make_header()
    store.flush_header(header)
    assert store.meta_path.exists()
    data = json.loads(store.meta_path.read_text(encoding="utf-8"))
    assert data["id"] == "c1"
    assert data["v"] == 1


def test_append_event_silent_on_oserror(tmp_path):
    """Append to a read-only dir must not raise."""
    store = _make_store(tmp_path)
    # Make the conv_dir read-only
    conv_dir = tmp_path / "history"
    conv_dir.chmod(stat.S_IREAD | stat.S_IEXEC)
    try:
        store.append_event(_make_event())  # must not raise
    finally:
        conv_dir.chmod(stat.S_IRWXU)


def test_file_created_on_first_append(tmp_path):
    store = _make_store(tmp_path, conv_id="newconv")
    assert not store.jsonl_path.exists()
    store.append_event(_make_event())
    assert store.jsonl_path.exists()
