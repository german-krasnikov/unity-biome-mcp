"""Tests for Play Mode auto-routing in middleware."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware import Middleware, wrap_send


@pytest.fixture
def mw():
    return Middleware()


# ─── track_editor_state ───────────────────────────────────────────────────────

def test_track_state_playing_from_editor_response(mw):
    mw.track_editor_state("editor", "playing:True\npaused:False\ncompiling:False\n")
    assert mw.is_playing is True


def test_track_state_stopped_from_editor_response(mw):
    mw.track_editor_state("editor", "playing:False\npaused:False\ncompiling:False\n")
    assert mw.is_playing is False


def test_track_state_ignores_non_editor_cmd(mw):
    mw.track_editor_state("get_hierarchy", "playing:True\npaused:False\n")
    assert mw.is_playing is False  # non-editor cmd ignored


def test_track_state_paused_counts_as_playing(mw):
    mw.track_editor_state("editor", "playing:False\npaused:True\ncompiling:False\n")
    assert mw.is_playing is True


# ─── reroute_cmd ─────────────────────────────────────────────────────────────

def test_reroute_set_property_to_runtime_in_play_mode(mw):
    mw.is_playing = True
    cmd, args = mw.reroute_cmd("set_property", {"path": "/P", "component": "T", "prop": "speed", "value": "5"})
    assert cmd == "set_runtime_property"
    assert args["field"] == "speed"
    assert "prop" not in args


def test_reroute_set_runtime_to_set_property_outside_play(mw):
    # Reverse reroute removed: set_runtime_property outside play is passed through as-is
    mw.is_playing = False
    cmd, args = mw.reroute_cmd("set_runtime_property", {"path": "/P", "component": "T", "field": "speed", "value": "5"})
    assert cmd == "set_runtime_property"
    assert args["field"] == "speed"


def test_reroute_noop_when_already_correct_play_mode(mw):
    mw.is_playing = True
    cmd, args = mw.reroute_cmd("set_runtime_property", {"path": "/P", "component": "T", "field": "speed", "value": "5"})
    assert cmd == "set_runtime_property"


def test_reroute_noop_when_already_correct_edit_mode(mw):
    mw.is_playing = False
    cmd, args = mw.reroute_cmd("set_property", {"path": "/P", "component": "T", "prop": "speed", "value": "5"})
    assert cmd == "set_property"


def test_reroute_other_cmds_unchanged(mw):
    mw.is_playing = True
    cmd, args = mw.reroute_cmd("get_hierarchy", {})
    assert cmd == "get_hierarchy"


# ─── wrap_send integration ────────────────────────────────────────────────────

async def test_wrap_send_reroutes_set_property_during_play():
    mw = Middleware()
    mw.is_playing = True
    captured = {}

    async def fake_send(cmd, args, timeout=30.0):
        captured["cmd"] = cmd
        captured["args"] = args
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("set_property", {"path": "/P", "component": "T", "prop": "speed", "value": "5"})
    assert captured["cmd"] == "set_runtime_property"
    assert captured["args"]["field"] == "speed"


async def test_wrap_send_tracks_editor_state():
    mw = Middleware()
    fake_send = AsyncMock(return_value="playing:True\npaused:False\ncompiling:False\n")
    wrapped = wrap_send(fake_send, mw)
    await wrapped("editor", {"action": "state"})
    assert mw.is_playing is True
