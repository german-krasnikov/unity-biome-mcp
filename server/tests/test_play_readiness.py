"""Tests for play-readiness epoch tracking (MCP-LIFE-004)."""
import asyncio
import pytest
from unittest.mock import AsyncMock, patch
from unity_mcp.play_state import PlayReadinessTracker, PlayState
from unity_mcp.tools.editor_state import parse_play_epoch, parse_world_ready


# ---------------------------------------------------------------------------
# editor_state helpers
# ---------------------------------------------------------------------------

def test_play_status_includes_epoch():
    state = "playing:True\nplay_epoch:3\nworld_ready:True"
    assert parse_play_epoch(state) == 3


def test_parse_play_epoch_absent_returns_none():
    state = "playing:True\npaused:False"
    assert parse_play_epoch(state) is None


def test_parse_world_ready_true():
    state = "playing:True\nplay_epoch:1\nworld_ready:True"
    assert parse_world_ready(state) is True


def test_parse_world_ready_false():
    state = "playing:True\nplay_epoch:1\nworld_ready:False"
    assert parse_world_ready(state) is False


def test_parse_world_ready_absent_returns_false():
    """Backward compat: old Unity that doesn't send world_ready."""
    state = "playing:True"
    assert parse_world_ready(state) is False


# ---------------------------------------------------------------------------
# PlayReadinessTracker — state transitions
# ---------------------------------------------------------------------------

def test_initial_state_not_ready():
    tracker = PlayReadinessTracker()
    s = tracker.state
    assert s.playing is False
    assert s.epoch == 0
    assert s.ready is False


def test_play_ready_requires_first_frame():
    """ready=False immediately after entering Play, ready=True after first frame signal."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:False")
    assert tracker.state.playing is True
    assert tracker.state.ready is False

    tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")
    assert tracker.state.ready is True


def test_epoch_increments_on_reentry():
    """Stop + play again → epoch increases in tracker."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")
    assert tracker.state.epoch == 1

    tracker.update("playing:False\nplay_epoch:1\nworld_ready:False")
    tracker.update("playing:True\nplay_epoch:2\nworld_ready:True")
    assert tracker.state.epoch == 2
    assert tracker.state.ready is True


def test_stop_clears_ready():
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")
    assert tracker.state.ready is True

    tracker.update("playing:False\nplay_epoch:1")
    assert tracker.state.ready is False
    assert tracker.state.playing is False


def test_backward_compat_no_epoch_no_world_ready():
    """Old Unity without epoch/world_ready: ready = playing (fallback)."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True")
    # No world_ready field → falls back: ready when playing
    assert tracker.state.ready is True


# ---------------------------------------------------------------------------
# PlayReadinessTracker — wait_for_ready
# ---------------------------------------------------------------------------

async def test_run_playtest_waits_for_ready():
    """wait_for_ready waits until ready=True, calls poll between checks."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:False")

    call_count = 0

    async def poll():
        nonlocal call_count
        call_count += 1
        if call_count >= 2:
            tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")

    await tracker.wait_for_ready(timeout=5.0, poll=poll, interval=0.0)
    assert tracker.state.ready is True
    assert call_count >= 2


async def test_wait_for_ready_times_out():
    """wait_for_ready raises TimeoutError when never becomes ready."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:False")

    async def poll():
        pass  # never updates to ready

    with pytest.raises(TimeoutError, match="play readiness"):
        await tracker.wait_for_ready(timeout=0.05, poll=poll, interval=0.01)


async def test_wait_for_ready_immediate_if_already_ready():
    """wait_for_ready returns immediately when already ready — no poll calls."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")

    poll_called = False

    async def poll():
        nonlocal poll_called
        poll_called = True

    await tracker.wait_for_ready(timeout=5.0, poll=poll, interval=0.0)
    assert not poll_called


async def test_wait_for_ready_fallback_no_world_ready_field():
    """Old Unity (no world_ready): wait_for_ready resolves when playing=True."""
    tracker = PlayReadinessTracker()
    tracker.update("playing:True")  # no world_ready — backward compat ready

    async def poll():
        pass

    # Should return immediately (already ready in fallback mode)
    await tracker.wait_for_ready(timeout=1.0, poll=poll, interval=0.0)
    assert tracker.state.ready is True


# ---------------------------------------------------------------------------
# Integration: _wait_for_play_state uses world_ready when available
# ---------------------------------------------------------------------------

async def test_wait_for_play_state_waits_for_world_ready(mock_bridge):
    """_wait_for_play_state polls until world_ready:True, not just playing:True."""
    from unity_mcp.tools.runtime import _wait_for_play_state

    # playing:True immediately but world_ready:False on first poll,
    # world_ready:True on second poll. Implementation MUST poll at least twice.
    state_responses = [
        "playing:True\nplay_epoch:1\nworld_ready:False",
        "playing:True\nplay_epoch:1\nworld_ready:True",
    ]
    state_calls: list[str] = []

    async def send_side_effect(cmd, args, **kw):
        if cmd == "editor" and args.get("action") == "state":
            resp = state_responses[min(len(state_calls), len(state_responses) - 1)]
            state_calls.append(resp)
            return {"ok": True, "data": resp}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = send_side_effect

    await _wait_for_play_state(True, "play")

    # Must have polled twice: first had world_ready:False, second had world_ready:True
    assert len(state_calls) >= 2, (
        f"Expected >=2 state polls (to wait for world_ready), got {len(state_calls)}: {state_calls}"
    )
