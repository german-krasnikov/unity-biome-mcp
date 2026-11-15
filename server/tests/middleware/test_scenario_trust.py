"""N0a: async playtest run lifecycle cache trust.

`Middleware._scenario_uncertain` suppresses PrefetchCache reads and background
prefetch while a dispatched playtest may still be mutating the scene. Set on a
valid `start_playtest` ack, cleared on terminal scenario evidence or an
edit-mode transition. Survives reset_session (reconnect does not prove Unity
stopped a playtest).

Tests #6, #7, #8, #11 exercise components deferred to N0a-2 (cache bypass +
prefetch suppression) and N0a-3 (force-invalidate split) — marked xfail until
those land.
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from unity_mcp.middleware import Middleware, wrap_send

# ── Guard-set on start_playtest ack ───────────────────────────────────────────


async def test_start_playtest_ack_sets_uncertain(mw):
    send_fn = AsyncMock(return_value="run_id=x")

    await wrap_send(send_fn, mw)("start_playtest", {"script": "LOG hi"})

    assert mw._scenario_uncertain is True


async def test_start_playtest_error_does_not_set_uncertain(mw):
    send_fn = AsyncMock(return_value="err: compile pending")

    await wrap_send(send_fn, mw)("start_playtest", {"script": "LOG hi"})

    assert mw._scenario_uncertain is False


# ── Guard-clear on terminal evidence ──────────────────────────────────────────


async def test_terminal_run_playtest_clears_uncertain(mw):
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="PLAYTEST: 1/1 OK")

    await wrap_send(send_fn, mw)("run_playtest", {"script": "LOG hi"})

    assert mw._scenario_uncertain is False


async def test_terminal_get_playtest_run_clears_uncertain(mw):
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="PLAYTEST: 2/2 OK")

    await wrap_send(send_fn, mw)("get_playtest_run", {"run_id": "abc"})

    assert mw._scenario_uncertain is False


async def test_running_phase_preserves_uncertain(mw):
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="phase=running")

    await wrap_send(send_fn, mw)("get_playtest_run", {"run_id": "abc"})

    assert mw._scenario_uncertain is True


async def test_sync_playtest_never_sets_uncertain(mw):
    """run_playtest never dispatches start_playtest — the guard-set condition
    never fires, so the guard stays False throughout the sync call."""
    send_fn = AsyncMock(return_value="PLAYTEST: 1/1 OK")

    await wrap_send(send_fn, mw)("run_playtest", {"script": "LOG hi"})

    assert mw._scenario_uncertain is False


# ── Guard-clear on edit-mode transition ───────────────────────────────────────


def test_edit_mode_transition_clears_uncertain(mw):
    """editor(action='stop') fast-path in track_editor_state must also clear
    the scenario guard: Play Mode ends, no playtest can still be running."""
    mw._scenario_uncertain = True

    mw.track_editor_state("editor", "ok", args={"action": "stop"})

    assert mw._scenario_uncertain is False


def test_editor_state_playing_false_clears_uncertain(mw):
    """Full-state editor(action='state') response with playing:False must
    also clear the guard (separate code path from the action='stop' fast-path)."""
    mw._scenario_uncertain = True

    mw.track_editor_state("editor", "playing:False\npaused:False\ncompiling:False\n")

    assert mw._scenario_uncertain is False


# ── Guard survives reset_session (reconnect) ──────────────────────────────────


def test_reset_session_preserves_uncertain(mw):
    """reset_session runs on reconnect; reconnecting doesn't prove Unity
    stopped a dispatched playtest, so the guard must survive it."""
    mw._scenario_uncertain = True

    mw.reset_session()

    assert mw._scenario_uncertain is True


# ── N0a-2 (deferred): cache bypass + prefetch suppression ────────────────────


async def test_cache_bypassed_while_uncertain(mw):
    from unity_mcp.prefetch_cache import PrefetchCache

    mw._prefetch_cache = PrefetchCache()
    mw._prefetch_cache.put("get_component", {"path": "/A", "type": "Transform"}, "stale cached data")
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="fresh data")

    result = await wrap_send(send_fn, mw)("get_component", {"path": "/A", "type": "Transform"})

    send_fn.assert_awaited_once()
    assert "stale cached data" not in result


async def test_cache_served_when_not_uncertain(mw):
    from unity_mcp.prefetch_cache import PrefetchCache

    mw._prefetch_cache = PrefetchCache()
    mw._prefetch_cache.put("get_component", {"path": "/A", "type": "Transform"}, "cached data")
    send_fn = AsyncMock(return_value="fresh data")

    result = await wrap_send(send_fn, mw)("get_component", {"path": "/A", "type": "Transform"})

    assert "[CACHED]" in result


async def test_background_prefetch_suppressed_while_uncertain(monkeypatch):
    """set_property has a GATE_PRIORS entry (predicts get_component). While
    uncertain, _maybe_prefetch_background must not fire that background task."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    mw = Middleware()
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="ok")

    def _reject_task(coro):
        coro.close()  # avoid "coroutine was never awaited" warning noise
        return MagicMock()

    with patch(
        "unity_mcp.middleware_pipeline.asyncio.create_task", side_effect=_reject_task
    ) as mock_create_task:
        await wrap_send(send_fn, mw)(
            "set_property",
            {"path": "/A", "component": "Transform", "prop": "m_Enabled", "value": "true"},
        )

    mock_create_task.assert_not_called()


# ── N0a-3 (deferred): force-invalidate must not clear the guard ──────────────


async def test_force_invalidate_preserves_uncertain(mw):
    """The give-up path (_force_scene_invalidate) clears caches but cannot
    prove Unity actually stopped the playtest — the guard must survive it."""
    mw._scenario_uncertain = True
    send_fn = AsyncMock(return_value="phase=running")

    await wrap_send(send_fn, mw)(
        "get_playtest_run", {"run_id": "x", "_force_scene_invalidate": "true"}
    )

    assert mw._scenario_uncertain is True
