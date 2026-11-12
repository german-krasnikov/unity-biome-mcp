"""TDD tests for degrade.py — graceful degradation ladder.

The ladder algorithm itself lives in biome_mcp.degrade (tested there). These
tests cover Unity's wiring: METRICS is passed in explicitly as the metrics
sink, and UNITY_MCP_DEGRADE_DISABLED is passed explicitly as disabled_env.
"""
from unittest.mock import AsyncMock, patch

import pytest

_DISABLED_ENV = "UNITY_MCP_DEGRADE_DISABLED"


# ── Test 1: all steps fail → no_fallback surface marker ──────────────────────

async def test_all_steps_fail_no_fallback():
    from biome_mcp.degrade import degrade, wrap_degraded

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    step_name, value = await degrade(
        "x", [("a", lambda: None), ("b", lambda: None)], metrics=METRICS
    )

    assert step_name == "b"
    assert value is None
    # Counter incremented once (one ladder fall)
    assert METRICS.snapshot()["counters"].get("degraded.x", 0) == 1
    # Surface helper produces canonical marker
    marker = wrap_degraded("x", "b", None)
    assert marker == "[DEGRADED:x:b:no_fallback]"


# ── Test 2: first step succeeds → no metrics, no marker ──────────────────────

async def test_first_step_succeeds_no_metrics():
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    step_name, value = await degrade(
        "x", [("primary", lambda: "hello"), ("fallback", lambda: "world")], metrics=METRICS
    )

    assert step_name == "primary"
    assert value == "hello"
    assert METRICS.snapshot()["counters"].get("degraded.x", 0) == 0


# ── Test 3: mix async and sync steps ─────────────────────────────────────────

async def test_async_and_sync_steps():
    from biome_mcp.degrade import degrade

    async def async_fail():
        return None

    def sync_success():
        return "sync_result"

    step_name, value = await degrade(
        "mixed", [("async_step", async_fail), ("sync_step", sync_success)]
    )

    assert step_name == "sync_step"
    assert value == "sync_result"


# ── Test 4: step raises exception → caught, treated as failure ────────────────

async def test_step_raises_exception_caught():
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    events = []
    original_event = METRICS.event

    def capture_event(kind, **fields):
        events.append({"kind": kind, **fields})

    METRICS.event = capture_event

    try:
        def boom():
            raise RuntimeError("boom")

        step_name, value = await degrade(
            "exc_test", [("raiser", boom), ("ok", lambda: "safe")], metrics=METRICS
        )
        assert step_name == "ok"
        assert value == "safe"
        # Event recorded with raised reason
        assert any(
            e.get("reason", "").startswith("raised:RuntimeError")
            for e in events
        )
    finally:
        METRICS.event = original_event


# ── Test 5: env UNITY_MCP_DEGRADE_DISABLED=1 → exception bubbles raw ─────────

async def test_env_disabled_propagates_exception(monkeypatch):
    from biome_mcp.degrade import degrade
    monkeypatch.setenv(_DISABLED_ENV, "1")

    def explode():
        raise ValueError("raw error")

    with pytest.raises(ValueError, match="raw error"):
        await degrade("feat", [("s1", explode), ("s2", lambda: "ok")], disabled_env=_DISABLED_ENV)


# ── Test 6: budget RouteDecision run=False → degraded marker ─────────────────

async def test_budget_skip_as_degradation():
    from biome_mcp.degrade import degrade, wrap_degraded

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    # Simulate a step that checks budget and returns None (skipped)
    def budget_check():
        return None  # budget exceeded → treated as failure

    step_name, value = await degrade("visual_diff", [
        ("budget_day_cap_exceeded", budget_check),
        ("pixel_only", lambda: "px_result"),
    ], metrics=METRICS)
    assert step_name == "pixel_only"
    assert value == "px_result"

    # Surface marker for a full-fail scenario (no_fallback suffix when value=None)
    marker_fail = wrap_degraded("visual_diff", "budget_day_cap_exceeded", None)
    assert marker_fail == "[DEGRADED:visual_diff:budget_day_cap_exceeded:no_fallback]"
    # Surface marker when a fallback value exists
    marker_ok = wrap_degraded("visual_diff", "budget_day_cap_exceeded", "px_result")
    assert "[DEGRADED:visual_diff:budget_day_cap_exceeded]" in marker_ok
    assert "no_fallback" not in marker_ok


# ── Test 7: PixelDiff.unavailable distinct from size_mismatch ────────────────

def test_pixeldiff_unavailable_field():
    from unity_mcp.visual_diff import PixelDiff
    px = PixelDiff(0, 0, True, False, unavailable=True)
    assert px.unavailable is True
    assert px.size_mismatch is True  # field still present

    px2 = PixelDiff(0, 255, True, False)  # backward compat — no unavailable
    assert px2.unavailable is False


def test_pixeldiff_unavailable_format():
    from unity_mcp.visual_diff import PixelDiff, _format_pixel
    px = PixelDiff(0, 0, False, False, unavailable=True)
    text = _format_pixel(px)
    assert "unavailable" in text.lower() or "UNAVAILABLE" in text
    assert "SIZE_MISMATCH" not in text


# ── Test 8: reflect skipped on [DEGRADED: prefix ─────────────────────────────

async def test_reflect_skipped_on_degraded_output(monkeypatch):
    from unity_mcp.middleware import wrap_send

    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    mock_send = AsyncMock(return_value="[DEGRADED:visual_diff:pixel_only] some fallback")
    reflect_mock = AsyncMock(return_value=None)

    wrapped = wrap_send(mock_send)

    with patch("unity_mcp.reflect.reflect", reflect_mock):
        result = await wrapped("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})

    # reflect must NOT have been called because result starts with [DEGRADED:
    reflect_mock.assert_not_called()
    assert "[DEGRADED:" in result


# ── Test 9: counter cardinality — one feature × N steps ──────────────────────

async def test_counter_cardinality_per_step():
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    # 3-step ladder, first 2 fail, 3rd succeeds
    await degrade("card_test", [
        ("step_a", lambda: None),
        ("step_b", lambda: None),
        ("step_c", lambda: "ok"),
    ], metrics=METRICS)

    snap = METRICS.snapshot()["counters"]
    # Per-step counters for failed steps
    assert snap.get("degraded.card_test.step_a", 0) == 1
    assert snap.get("degraded.card_test.step_b", 0) == 1
    # step_c succeeded — no counter
    assert snap.get("degraded.card_test.step_c", 0) == 0
    # ladder-level counter: 1 (at least one step fell)
    assert snap.get("degraded.card_test", 0) == 1


# ── Test 10: event log format ─────────────────────────────────────────────────

async def test_event_log_format():
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS

    events = []
    original_event = METRICS.event

    def capture(kind, **fields):
        events.append({"kind": kind, **fields})

    METRICS.event = capture
    try:
        await degrade("evt_feat", [("s1", lambda: None), ("s2", lambda: "ok")], metrics=METRICS)
    finally:
        METRICS.event = original_event

    # Should have exactly one event for the failed step
    degraded_events = [e for e in events if e["kind"] == "degraded"]
    assert len(degraded_events) == 1
    e = degraded_events[0]
    assert e["feature"] == "evt_feat"
    assert e["step"] == "s1"
    assert "reason" in e


# ── Test 11: wrap_degraded prefix exactly once ────────────────────────────────

def test_wrap_degraded_prefix_not_doubled():
    from biome_mcp.degrade import wrap_degraded
    result = wrap_degraded("feat", "step", "value")
    assert result.count("[DEGRADED:") == 1
    assert result.startswith("[DEGRADED:feat:step]\n")


# ── Test 13: disabled mode falls through None to next rung ───────────────────

async def test_disabled_mode_falls_through_on_none(monkeypatch):
    """#2: DEGRADE_DISABLED=1 + first step returns None → second step runs, returns 'ok', no marker."""
    from biome_mcp.degrade import degrade
    monkeypatch.setenv(_DISABLED_ENV, "1")

    step_name, result = await degrade("feat", [
        ("s1", lambda: None),
        ("s2", lambda: "ok"),
    ], disabled_env=_DISABLED_ENV)
    assert result == "ok"
    assert step_name == "s2"


# ── Test 14: disabled mode exception still propagates raw ─────────────────────

async def test_disabled_mode_propagates_exception_raw(monkeypatch):
    """#2: DEGRADE_DISABLED=1 + step raises → exception propagates, not suppressed."""
    from biome_mcp.degrade import degrade
    monkeypatch.setenv(_DISABLED_ENV, "1")

    def explode():
        raise ValueError("raw!")

    with pytest.raises(ValueError, match="raw!"):
        await degrade("feat", [("boom", explode), ("fallback", lambda: "safe")], disabled_env=_DISABLED_ENV)


# ── Test 15: disabled mode → METRICS.event NOT fired ─────────────────────────

async def test_disabled_mode_no_metrics_event(monkeypatch):
    """Pattern E: UNITY_MCP_DEGRADE_DISABLED=1 → METRICS.event is never called,
    even when a step falls (returns None)."""
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    monkeypatch.setenv(_DISABLED_ENV, "1")

    events = []
    monkeypatch.setattr(METRICS, "event", lambda kind, **fields: events.append({"kind": kind, **fields}))

    await degrade("dis_feat", [
        ("s1", lambda: None),   # falls
        ("s2", lambda: "ok"),   # succeeds
    ], metrics=METRICS, disabled_env=_DISABLED_ENV)

    assert events == [], f"Expected no events, got {events}"


async def test_disabled_mode_no_metrics_event_all_fail(monkeypatch):
    """Pattern E: all steps fail in disabled mode → still no METRICS.event."""
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    monkeypatch.setenv(_DISABLED_ENV, "1")

    events = []
    monkeypatch.setattr(METRICS, "event", lambda kind, **fields: events.append({"kind": kind, **fields}))

    await degrade("dis_all_fail", [
        ("s1", lambda: None),
        ("s2", lambda: None),
    ], metrics=METRICS, disabled_env=_DISABLED_ENV)

    assert events == [], f"Expected no events, got {events}"


# ── Test 16: empty steps list → ("", None), no metrics ──────────────────────

async def test_empty_steps_list_returns_empty_name_none():
    """degrade() with no steps returns ('', None) and emits no metrics."""
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    step_name, value = await degrade("empty_feat", [], metrics=METRICS)

    assert step_name == ""
    assert value is None
    snap = METRICS.snapshot()["counters"]
    assert snap.get("degraded.empty_feat", 0) == 0


# ── Test 17: disabled + async step falls through correctly ───────────────────

async def test_disabled_async_step_falls_through(monkeypatch):
    """DEGRADE_DISABLED=1 with async step that returns None → next step runs."""
    from biome_mcp.degrade import degrade
    monkeypatch.setenv(_DISABLED_ENV, "1")

    async def async_none():
        return None

    step_name, value = await degrade("feat", [
        ("async_step", async_none),
        ("fallback", lambda: "ok"),
    ], disabled_env=_DISABLED_ENV)

    assert step_name == "fallback"
    assert value == "ok"


# ── Test 12: all steps raise → last step name returned, counter=1 ─────────────

async def test_all_steps_raise_all_caught():
    from biome_mcp.degrade import degrade

    from unity_mcp.metrics import METRICS
    METRICS.reset()

    step_name, value = await degrade("all_raise", [
        ("s1", lambda: (_ for _ in ()).throw(OSError("disk"))),
        ("s2", lambda: (_ for _ in ()).throw(OSError("net"))),
    ], metrics=METRICS)

    assert step_name == "s2"
    assert value is None
    assert METRICS.snapshot()["counters"].get("degraded.all_raise", 0) == 1
