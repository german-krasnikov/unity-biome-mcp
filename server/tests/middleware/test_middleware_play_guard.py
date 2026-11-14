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


# ── P3 #11: guard_verdict priority owned by guards.logic, not by call order ──
# pyReadOnly has priority 30, pyNotInPlay has priority 31 (dsl/decisions/guards.logic):
# read-only must win whenever both conditions hold, however check_read_only and
# check_play_mode_required happen to be called in middleware_pipeline.

def test_guard_defers_to_read_only_when_both_block_conditions_hold():
    cmd = "set_runtime_property"  # runtime-only AND a write (_RUNTIME_ONLY_CMDS + WRITE_CMDS)
    assert cmd in _RUNTIME_ONLY_CMDS
    mw = _make_mw(play_state_known=True, is_playing=False)  # edit mode: would trip NOT_IN_PLAY alone
    mw.is_read_only = True  # would also trip READ_ONLY_BLOCKED alone

    assert mw.check_read_only(cmd, {}) is not None, "READ_ONLY_BLOCKED must win (priority 30)"
    assert mw.check_play_mode_required(cmd, {}) is None, (
        "check_play_mode_required must defer to the higher-priority read-only rule, "
        "not re-raise NOT_IN_PLAY (priority 31) for a call check_read_only already owns"
    )

    # Order independence: swapping which guard runs first must not change either answer —
    # the priority lives in guard_verdict's rule order, not in which Python if ran first.
    mw2 = _make_mw(play_state_known=True, is_playing=False)
    mw2.is_read_only = True
    assert mw2.check_play_mode_required(cmd, {}) is None
    assert mw2.check_read_only(cmd, {}) is not None


# ── P3 #11: _py_guard_facts is the single shared fact computation ───────────
# check_read_only and check_play_mode_required both delegate to this helper
# instead of each recomputing read_only/mutating — that is the mechanism that
# makes the fact set identical regardless of which guard runs first. No test
# pinned the helper itself before; a future edit that makes either caller
# bypass it (recomputing its own partial facts again) would not necessarily
# break check_read_only/check_play_mode_required's own tests but would reopen
# P3 #11, and this test exists to catch that.

def test_py_guard_facts_mutating_true_only_when_read_only_and_write():
    mw = _make_mw()
    mw.is_read_only = True
    assert mw._py_guard_facts("set_property", {}) == {"read_only": True, "mutating": True}
    assert mw._py_guard_facts("get_hierarchy", {}) == {"read_only": True, "mutating": False}


def test_py_guard_facts_mutating_always_false_when_writable():
    """A writable endpoint never classifies a call as mutating — is_write is not even asked,
    so a command nobody knows (fail-closed default) still reports mutating=False here."""
    mw = _make_mw()
    mw.is_read_only = False
    assert mw._py_guard_facts("set_property", {}) == {"read_only": False, "mutating": False}
    assert mw._py_guard_facts("totally_unregistered_xyz", {}) == {"read_only": False, "mutating": False}


def test_check_read_only_and_check_play_mode_required_both_call_shared_helper():
    """White-box guard against reopening P3 #11: both methods must source their
    site-py facts from _py_guard_facts, not from a reimplemented, independent
    read_only/mutating computation of their own. A verdict-only test would stay
    green even if a future edit duplicated the computation again, as long as
    the duplicate happened to agree with guards.logic's priority by accident."""
    import inspect

    from unity_mcp.middleware_guards import MiddlewareGuardsMixin

    for method_name in ("check_read_only", "check_play_mode_required"):
        source = inspect.getsource(getattr(MiddlewareGuardsMixin, method_name))
        assert "_py_guard_facts(" in source, f"{method_name} must call the shared _py_guard_facts helper"


def test_guard_passes_non_runtime_cmd():
    mw = _make_mw(play_state_known=True, is_playing=False)
    assert mw.check_play_mode_required("get_component") is None


def test_guard_passes_watch_remove():
    assert "watch_remove" not in _RUNTIME_ONLY_CMDS
    mw = _make_mw(play_state_known=True, is_playing=False)
    assert mw.check_play_mode_required("watch_remove") is None


# ── B05: run_playtest's Play-mode gate moved past parsing (C#-side, header-driven) ──
# Python must no longer pre-block it — a second, Python-only authority is exactly the
# failure mode B05 removes (R-05: C# is now the sole authority).

def test_run_playtest_not_in_runtime_only_cmds():
    assert "run_playtest" not in _RUNTIME_ONLY_CMDS


def test_guard_passes_run_playtest_edit_mode():
    mw = _make_mw(play_state_known=True, is_playing=False)
    assert mw.check_play_mode_required("run_playtest") is None


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


def test_track_editor_state_requested_sets_playing():
    """EditorStateHelper.Control('play') returns 'requested' — must set is_playing=True."""
    mw = _make_mw(play_state_known=False)
    mw.track_editor_state("editor", "requested", args={"action": "play"})
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


# ── V6: track_editor_state updates from non-editor commands ─────────────────

def test_mcp_status_updates_is_playing():
    """V6: mcp_status response with playing:True updates is_playing."""
    mw = _make_mw(play_state_known=False)
    mw.track_editor_state("mcp_status", "playing:True\npaused:False\n")
    assert mw._play_state_known is True
    assert mw.is_playing is True


def test_mcp_status_updates_is_not_playing():
    """V6: mcp_status response with playing:False clears is_playing."""
    mw = _make_mw(play_state_known=False)
    mw.track_editor_state("mcp_status", "playing:False\npaused:False\n")
    assert mw._play_state_known is True
    assert mw.is_playing is False


def test_ttl_prevents_rapid_non_editor_update():
    """V6: second non-editor update within 5s TTL does NOT override cached state."""
    import time
    mw = _make_mw(play_state_known=True, is_playing=True)
    mw._play_state_ts = time.monotonic()  # just updated
    mw.track_editor_state("mcp_status", "playing:False\npaused:False\n")
    assert mw.is_playing is True  # unchanged — TTL not expired


def test_editor_always_updates_regardless_of_ttl():
    """V6: editor command always wins immediately, ignoring TTL."""
    import time
    mw = _make_mw(play_state_known=True, is_playing=True)
    mw._play_state_ts = time.monotonic()  # just updated non-editor
    mw.track_editor_state("editor", "playing:False\npaused:False\n")
    assert mw.is_playing is False


def test_non_editor_without_playing_field_ignored():
    """V6: response without playing: field leaves state unchanged."""
    mw = _make_mw(play_state_known=False)
    mw.track_editor_state("get_hierarchy", "Root\n  Child")
    assert mw._play_state_known is False


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
