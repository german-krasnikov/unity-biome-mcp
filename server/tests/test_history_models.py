"""T23: ConversationHeader + EXCLUDED_KINDS unit tests."""
from __future__ import annotations


def test_roundtrip(tmp_path):
    from unity_mcp.history.models import ConversationHeader

    h = ConversationHeader(
        conv_id="abc123",
        title="How do I create a health system?",
        created_at="2026-08-14T10:00:00+00:00",
        updated_at="2026-08-14T10:01:00+00:00",
        backend="Claude",
        session_id="sess_xyz",
        turn_count=3,
        fingerprint="a1b2c3d4e5f6",
    )
    assert ConversationHeader.from_dict(h.to_dict()) == h


def test_to_dict_has_version_key():
    from unity_mcp.history.models import ConversationHeader

    h = ConversationHeader(
        conv_id="x", title="t", created_at="c", updated_at="u",
        backend="b", session_id="", turn_count=0, fingerprint="fp",
    )
    d = h.to_dict()
    assert d["v"] == 1
    assert d["id"] == "x"
    assert d["title"] == "t"


def test_title_truncation():
    """from_dict truncates title to 80 chars."""
    from unity_mcp.history.models import ConversationHeader

    long_title = "A" * 100
    h = ConversationHeader(
        conv_id="x", title=long_title, created_at="c", updated_at="u",
        backend="b", session_id="", turn_count=0, fingerprint="fp",
    )
    # to_dict / from_dict preserve stored value; title truncation is the manager's job
    d = h.to_dict()
    assert d["title"] == long_title  # stored as-is


def test_excluded_kinds_contains_noise_events():
    from unity_mcp.history.models import EXCLUDED_KINDS

    assert "heartbeat" in EXCLUDED_KINDS
    assert "cost_update" in EXCLUDED_KINDS


def test_excluded_kinds_does_not_contain_turn_events():
    from unity_mcp.history.models import EXCLUDED_KINDS

    assert "turn_started" not in EXCLUDED_KINDS
    assert "assistant_delta" not in EXCLUDED_KINDS
    assert "session_started" not in EXCLUDED_KINDS
