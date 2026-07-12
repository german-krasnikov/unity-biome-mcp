"""Tests for MiddlewareAsyncMixin._background_prefetch — P1-15 zero coverage.

Covers:
- happy path: result cached in _prefetch_cache
- dict result: extracts data field
- string result: stored as-is
- send_fn raises: METRICS.inc("prefetch.error"), no crash
- cache is None: no crash, result discarded
- empty result: nothing cached
"""
from unittest.mock import AsyncMock, MagicMock, patch


def _make_mixin():
    """Instantiate MiddlewareAsyncMixin with minimal attrs required by _background_prefetch."""
    from unity_mcp.middleware_async import MiddlewareAsyncMixin
    from unity_mcp.prefetch_cache import PrefetchCache

    obj = MiddlewareAsyncMixin.__new__(MiddlewareAsyncMixin)
    obj._prefetch_cache = PrefetchCache()
    return obj


# ── happy paths ───────────────────────────────────────────────────────────────

async def test_background_prefetch_caches_string_result():
    m = _make_mixin()
    send_fn = AsyncMock(return_value="hierarchy text")

    await m._background_prefetch("get_hierarchy", {"summary": "true"}, send_fn)

    cached = m._prefetch_cache.get("get_hierarchy", {"summary": "true"})
    assert cached == "hierarchy text"


async def test_background_prefetch_caches_dict_data_field():
    m = _make_mixin()
    send_fn = AsyncMock(return_value={"data": "component info", "ok": True})

    await m._background_prefetch("get_component", {"path": "/A", "type": "T"}, send_fn)

    cached = m._prefetch_cache.get("get_component", {"path": "/A", "type": "T"})
    assert cached == "component info"


async def test_background_prefetch_empty_result_not_cached():
    m = _make_mixin()
    send_fn = AsyncMock(return_value="")

    await m._background_prefetch("get_component", {"path": "/A", "type": "T"}, send_fn)

    assert m._prefetch_cache.get("get_component", {"path": "/A", "type": "T"}) is None


# no-assert: crash guard
async def test_background_prefetch_cache_none_no_crash():
    """Verifies _background_prefetch does not raise when cache is None."""
    m = _make_mixin()
    m._prefetch_cache = None
    send_fn = AsyncMock(return_value="data")

    # Must not raise
    await m._background_prefetch("get_hierarchy", {}, send_fn)
    send_fn.assert_awaited_once()


# ── error handling ────────────────────────────────────────────────────────────

async def test_background_prefetch_send_raises_increments_metric():
    from unity_mcp.metrics import METRICS
    METRICS.reset()

    m = _make_mixin()
    send_fn = AsyncMock(side_effect=ConnectionError("gone"))

    await m._background_prefetch("get_hierarchy", {}, send_fn)

    snap = METRICS.snapshot()["counters"]
    assert snap.get("prefetch.error", 0) == 1


# no-assert: crash guard
async def test_background_prefetch_send_raises_no_crash():
    """Verifies _background_prefetch swallows RuntimeError without propagating."""
    m = _make_mixin()
    send_fn = AsyncMock(side_effect=RuntimeError("boom"))

    # Exception swallowed — background task must not propagate
    await m._background_prefetch("get_hierarchy", {}, send_fn)
    send_fn.assert_awaited_once()
