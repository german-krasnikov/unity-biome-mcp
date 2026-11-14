"""Regression guards for PlayReadinessTracker world_ready + epoch logic (MCP-LIFE-004/005).

These tests verify Python tracker behaviour given properly-formed state strings.
No Unity connection required — all tests are pure string-parsing unit tests.
"""
from unity_mcp.play_state import PlayReadinessTracker
from unity_mcp.tools.editor_state import parse_play_epoch


def test_tracker_uses_world_ready_when_present():
    """playing:True + world_ready:true → ready."""
    t = PlayReadinessTracker()
    t.update("playing:True\nworld_ready:True\nplay_epoch:1")
    assert t.state.ready is True


def test_tracker_not_ready_when_world_ready_false():
    """world_ready:false overrides playing:True — no premature dispatch."""
    t = PlayReadinessTracker()
    t.update("playing:True\nworld_ready:False\nplay_epoch:1")
    assert t.state.ready is False


def test_tracker_fallback_when_world_ready_absent():
    """No world_ready field → backward-compat fallback: ready = playing."""
    t = PlayReadinessTracker()
    t.update("playing:True\npaused:False")
    assert t.state.ready is True


def test_tracker_parses_play_epoch():
    """play_epoch:5 in state string → tracker.state.epoch == 5."""
    t = PlayReadinessTracker()
    t.update("playing:True\nworld_ready:True\nplay_epoch:5")
    assert t.state.epoch == 5


def test_tracker_epoch_none_when_absent():
    """parse_play_epoch returns None when field is absent (used by tracker internally)."""
    assert parse_play_epoch("playing:True\npaused:False") is None


def test_tracker_ready_requires_both_playing_and_world_ready():
    """playing:False + world_ready:True → not ready (must be both)."""
    t = PlayReadinessTracker()
    t.update("playing:False\nworld_ready:True\nplay_epoch:0")
    assert t.state.ready is False
