"""Tests for SpeculativeLayer — predict + prefetch."""
import asyncio
from unittest.mock import AsyncMock
from unity_mcp.console_levels import PROBLEM_LEVELS
from unity_mcp.speculation import SpeculativeLayer, Prediction


# ── predict() ──────────────────────────────────────────────────────────────

def test_predict_returns_none_for_unknown_cmd():
    sl = SpeculativeLayer(AsyncMock())
    assert sl.predict("get_hierarchy", {}, "ok") is None


def test_predict_set_property_reference_returns_get_component():
    sl = SpeculativeLayer(AsyncMock())
    args = {"path": "/Player", "component": "Health", "prop": "targetReference", "value": "/Enemy"}
    pred = sl.predict("set_property", args, "ok")
    assert pred is not None
    assert pred.cmd == "get_component"
    assert pred.args["path"] == "/Player"
    assert pred.args["type"] == "Health"


def test_predict_set_property_non_reference_returns_none():
    sl = SpeculativeLayer(AsyncMock())
    args = {"path": "/Player", "component": "Health", "prop": "hp", "value": "100"}
    assert sl.predict("set_property", args, "ok") is None


def test_predict_wire_event_returns_validate_references():
    sl = SpeculativeLayer(AsyncMock())
    args = {"path": "/Player"}
    pred = sl.predict("wire_event", args, "ok")
    assert pred is not None
    assert pred.cmd == "validate_references"
    assert pred.args["path"] == "/Player"


def test_predict_batch_returns_get_console():
    sl = SpeculativeLayer(AsyncMock())
    pred = sl.predict("batch", {}, "ok")
    assert pred is not None
    assert pred.cmd == "get_console"


def test_predict_batch_uses_problem_levels():
    """Issue 27: batch prediction must scan Error+Exception+Assert, not just Error."""
    sl = SpeculativeLayer(AsyncMock())
    pred = sl.predict("batch", {}, "ok")
    assert pred is not None
    assert pred.args["level"] == PROBLEM_LEVELS


def test_predict_recompile_returns_get_compile_errors():
    sl = SpeculativeLayer(AsyncMock())
    pred = sl.predict("recompile", {}, "ok")
    assert pred is not None
    assert pred.cmd == "get_compile_errors"


# ── maybe_prefetch() ────────────────────────────────────────────────────────

async def test_maybe_prefetch_disabled_returns_unchanged():
    sl = SpeculativeLayer(AsyncMock(), enabled=False)
    result = await sl.maybe_prefetch("recompile", {}, "base_result")
    assert result == "base_result"


async def test_maybe_prefetch_appends_data_on_success():
    send = AsyncMock(return_value="no errors")
    sl = SpeculativeLayer(send)
    result = await sl.maybe_prefetch("recompile", {}, "base_result")
    assert "base_result" in result
    assert "PREFETCH" in result
    assert "no errors" in result


async def test_maybe_prefetch_silent_on_error():
    async def failing(*a, **kw):
        raise Exception("timeout")
    sl = SpeculativeLayer(failing)
    result = await sl.maybe_prefetch("recompile", {}, "base_result")
    assert result == "base_result"


async def test_maybe_prefetch_silent_on_oversized():
    send = AsyncMock(return_value="x" * 1000)
    sl = SpeculativeLayer(send)
    result = await sl.maybe_prefetch("recompile", {}, "base_result")
    assert result == "base_result"


# ── record_actual_next() / hit rate ────────────────────────────────────────

async def test_record_actual_next_increments_hits():
    send = AsyncMock(return_value="data")
    sl = SpeculativeLayer(send)
    await sl.maybe_prefetch("recompile", {}, "r")  # sets _last_prediction = get_compile_errors
    sl.record_actual_next("get_compile_errors")
    assert sl._hits == 1
    assert sl._misses == 0


async def test_record_actual_next_increments_misses():
    send = AsyncMock(return_value="data")
    sl = SpeculativeLayer(send)
    await sl.maybe_prefetch("recompile", {}, "r")
    sl.record_actual_next("something_else")
    assert sl._hits == 0
    assert sl._misses == 1


async def test_auto_disable_below_40_percent_hit_rate():
    """Below 40% hit rate after 50 predictions → skip prefetch."""
    send = AsyncMock(return_value="data")
    sl = SpeculativeLayer(send)
    # Simulate 50 predictions: 10 hits, 40 misses = 20% hit rate
    sl._hits = 10
    sl._misses = 40
    sl._last_prediction = None  # not currently predicting
    result = await sl.maybe_prefetch("recompile", {}, "base")
    assert result == "base"
    send.assert_not_called()
