"""Phase D regression: fail-fast guard blocks runtime-only cmds in edit mode."""
import pytest
import asyncio
from unity_mcp.middleware import Middleware
from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
from unity_mcp.middleware_pipeline import wrap_send


def _make_mw(play_state_known: bool = False, is_playing: bool = False) -> Middleware:
    mw = Middleware()
    mw._play_state_known = play_state_known
    mw.is_playing = is_playing
    return mw


# ── check_play_mode_required unit tests ──────────────────────────────────────

def test_guard_passes_when_state_unknown():
    mw = _make_mw(play_state_known=False)
    assert mw.check_play_mode_required("invoke_method") is None


def test_guard_passes_when_playing():
    mw = _make_mw(play_state_known=True, is_playing=True)
    assert mw.check_play_mode_required("invoke_method") is None


def test_guard_blocks_when_edit_mode():
    mw = _make_mw(play_state_known=True, is_playing=False)
    result = mw.check_play_mode_required("invoke_method")
    assert result is not None
    assert "invoke_method" in result
    assert "Play Mode" in result


def test_guard_passes_non_runtime_cmd():
    mw = _make_mw(play_state_known=True, is_playing=False)
    assert mw.check_play_mode_required("get_component") is None


def test_guard_passes_watch_remove():
    assert "watch_remove" not in _RUNTIME_ONLY_CMDS
    mw = _make_mw(play_state_known=True, is_playing=False)
    assert mw.check_play_mode_required("watch_remove") is None


# ── track_editor_state sets _play_state_known ────────────────────────────────

def test_track_editor_state_sets_known():
    mw = _make_mw()
    assert not mw._play_state_known
    mw.track_editor_state("editor", "playing:False\npaused:False\ncompiling:False\n")
    assert mw._play_state_known
    assert not mw.is_playing


def test_track_editor_state_sets_playing():
    mw = _make_mw()
    mw.track_editor_state("editor", "playing:True\npaused:False\ncompiling:False\n")
    assert mw._play_state_known
    assert mw.is_playing


# ── P-415: action result fast-path ───────────────────────────────────────────

def test_track_editor_state_entered_sets_playing():
    """P-415: editor(action='play') returning 'entered' must set is_playing=True."""
    mw = _make_mw(play_state_known=True, is_playing=False)
    mw.track_editor_state("editor", "entered", args={"action": "play"})
    assert mw._play_state_known is True
    assert mw.is_playing is True


def test_track_editor_state_already_playing_sets_playing():
    """P-415: 'already_playing' must also set is_playing=True."""
    mw = _make_mw(play_state_known=False)
    mw.track_editor_state("editor", "already_playing", args={"action": "play"})
    assert mw._play_state_known is True
    assert mw.is_playing is True


def test_play_guard_does_not_block_after_editor_play_entered():
    """P-415: runtime cmd allowed after editor(play) returns 'entered'."""
    mw = _make_mw(play_state_known=True, is_playing=False)
    mw.track_editor_state("editor", "entered", args={"action": "play"})
    assert mw.check_play_mode_required("run_playtest") is None


def test_stop_action_ok_clears_playing():
    """P-415: editor(action='stop') returning 'ok' must set is_playing=False."""
    mw = _make_mw(play_state_known=True, is_playing=True)
    mw.track_editor_state("editor", "ok", args={"action": "stop"})
    assert mw.is_playing is False


# ── pipeline integration test ─────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_pipeline_blocks_runtime_cmd_in_edit_mode():
    """wrap_send must return error without calling send_fn for runtime cmd in edit mode."""
    send_called = []

    async def mock_send(cmd, args, timeout=0):
        send_called.append(cmd)
        return "ok"

    mw = _make_mw(play_state_known=True, is_playing=False)
    wrapped = wrap_send(mock_send, mw)

    result = await wrapped("invoke_method", {"path": "/Player", "component": "PC", "method": "M"})

    assert "invoke_method" in result
    assert "Play Mode" in result
    assert send_called == [], f"send_fn was called despite edit mode guard: {send_called}"
