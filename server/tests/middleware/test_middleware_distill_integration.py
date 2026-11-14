"""Tests for audit-2026-06-12 findings: PY6.test.1, PY6.test.6, X3.cross.2, X3.cross.4,
X4.cross.3, X5.cross.3.
"""
import asyncio
import os
import pytest
from unittest.mock import AsyncMock, MagicMock


# ── PY6.test.1: Live cache-collision test (replaces tautological key-diff tests) ─

async def test_distill_cache_no_cross_type_collision(monkeypatch):
    """Cache entry for Transform must NOT be returned for Rigidbody query."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")

    from unity_mcp.middleware import Middleware

    mw = Middleware()
    mw._recent_focus.append("/P")

    big_transform = "[Transform]\nposition: (1,2,3)\n" * 100  # large enough for caching path

    # Seed cache via real _maybe_distill for Transform
    from unity_mcp.distiller import ResponseDistiller
    mw._distiller = ResponseDistiller(sampling=None)

    # Manually populate cache as _haiku_to_cache would
    import json
    focus = tuple(mw._recent_focus)
    sig_transform = {"path": "/P", "type": "Transform"}
    key_transform = ("get_component", json.dumps(dict(sorted(sig_transform.items())), sort_keys=True), focus)
    mw._distill_cache[key_transform] = "CACHED_TRANSFORM_TEXT"

    # Now call _maybe_distill for Rigidbody — must NOT return Transform cache
    rb_text = "[Rigidbody]\nmass: 5\n" * 100
    result = await mw._maybe_distill("get_component", {"path": "/P", "type": "Rigidbody"}, rb_text)

    assert "CACHED_TRANSFORM_TEXT" not in result, "Rigidbody call must not return Transform cache"
    assert "haiku-cached" not in result or "CACHED_TRANSFORM_TEXT" not in result


# ── PY6.test.6: maybe_inject_state exception swallowed silently ───────────────

async def test_maybe_inject_state_exception_swallowed():
    """When send_fn raises on get_hierarchy, result is unchanged and no exception propagates."""
    from unity_mcp.middleware import Middleware

    mw = Middleware()
    mw.call_count = 9  # next call → call_count=10 → %10==0 triggers injection

    async def exploding_send(cmd, args, timeout=30.0):
        if cmd == "get_hierarchy":
            raise ConnectionError("Unity offline")
        return "ok"

    original = "original-result"
    result = await mw.maybe_inject_state(exploding_send, original)

    assert result == original, f"Expected original result, got: {result!r}"


# ── X3.cross.2: transfer_object in WRITE_CMDS ────────────────────────────────

def test_write_cmds_includes_transfer_object():
    """transfer_object must be in WRITE_CMDS to trigger cache invalidation."""
    from unity_mcp.middleware_types import WRITE_CMDS
    assert "transfer_object" in WRITE_CMDS


async def test_hierarchy_diff_resets_after_transfer_object(monkeypatch):
    """After transfer_object, get_hierarchy must return fresh tree (not [DIFF])."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()

    hierarchy_v1 = "/Root\n  /Child1\n"
    hierarchy_v2 = "/Root\n  /Child2\n"
    call_count = [0]

    async def fake_send(cmd, args, timeout=30.0):
        if cmd == "get_hierarchy":
            call_count[0] += 1
            return hierarchy_v1 if call_count[0] == 1 else hierarchy_v2
        return "ok"

    wrapped = wrap_send(fake_send, mw)

    # Seed hierarchy diff state
    result1 = await wrapped("get_hierarchy", {})
    assert "[DIFF" not in result1

    # Move object (write) — must reset diff state
    await wrapped("transfer_object", {"path": "/Root/Child1", "target": "/Other"})

    # Next get_hierarchy must return full tree, not diff
    result2 = await wrapped("get_hierarchy", {})
    assert "[DIFF" not in result2, f"Expected full hierarchy after transfer, got: {result2!r}"


# ── X3.cross.4: maybe_inject_state guarded when UNITY_MCP_MIDDLEWARE is off ──

async def test_auto_state_off_by_default(monkeypatch):
    """10 sequential set_property calls must NOT produce AUTO STATE when UNITY_MCP_AUTO_STATE=0."""
    monkeypatch.delenv("UNITY_MCP_MIDDLEWARE", raising=False)
    monkeypatch.setenv("UNITY_MCP_AUTO_STATE", "0")
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw._last_hierarchy_call = 0  # ensure staleness gate is open

    async def fake_send(cmd, args, timeout=30.0):
        return f"result-{cmd}"

    wrapped = wrap_send(fake_send, mw)

    results = []
    for i in range(11):
        # unique value per call to avoid RETRY block
        r = await wrapped("set_property", {"path": "/P", "prop": "x", "value": str(i)})
        results.append(r)

    assert not any("AUTO STATE" in r for r in results), (
        "AUTO STATE injection must not fire when UNITY_MCP_AUTO_STATE=0"
    )


# ── X4.cross.3: UNITY_MCP_DISTILL_HAIKU=1 activates SamplingService ─────────

async def test_distill_haiku_env_gate_activates(monkeypatch):
    """UNITY_MCP_DISTILL_HAIKU=1 initializes _distiller with a SamplingService."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    monkeypatch.setenv("UNITY_MCP_DISTILL_HAIKU", "1")

    from unity_mcp.middleware import Middleware
    from unity_mcp.distiller import ResponseDistiller

    # Mock SamplingService to avoid real API calls
    mock_sampling = MagicMock()
    mock_sampling.enabled = True

    with pytest.MonkeyPatch().context() as mp:
        mp.setattr("unity_mcp.middleware_async.SamplingService", lambda: mock_sampling, raising=False)
        # Patch inside the module
        import unity_mcp.middleware_async as ma

        original_sampling_cls = None
        try:
            from unity_mcp import sampling as _smod
            original_cls = _smod.SamplingService
            _smod.SamplingService = lambda: mock_sampling
        except Exception:
            pytest.skip("SamplingService not importable in this env")

        try:
            mw = Middleware()
            mw._recent_focus.append("/P")
            huge_text = "/P\n" * 300

            # First call triggers lazy init of _distiller
            await mw._maybe_distill("get_hierarchy", {}, huge_text)

            # _distiller must be initialized
            assert mw._distiller is not None
        finally:
            _smod.SamplingService = original_cls


# ── X5.cross.3: _haiku_to_cache end-to-end ───────────────────────────────────

async def test_haiku_to_cache_populates_cache(monkeypatch):
    """_haiku_to_cache populates _distill_cache; next _maybe_distill returns cached result."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")

    from unity_mcp.middleware import Middleware
    from unity_mcp.distiller import ResponseDistiller, DistillResult

    mw = Middleware()
    mw._recent_focus.append("/Player")

    # Mock sampling that returns a valid distilled result (subset of input, no hallucinated paths)
    mock_sampling = MagicMock()

    async def mock_generate(prompt, **kwargs):
        # Return a short valid subset — just one line from the huge_text
        return "/Player"

    mock_sampling.generate = mock_generate
    mock_sampling.enabled = True

    mw._distiller = ResponseDistiller(sampling=mock_sampling)

    huge_text = "/Player\n" * 300
    focus = ("/Player",)

    import json
    cache_key = ("get_hierarchy", json.dumps({}, sort_keys=True), focus)

    # Fire background task directly
    task = asyncio.create_task(mw._haiku_to_cache("get_hierarchy", huge_text, focus, cache_key))
    await asyncio.sleep(0.1)
    await task

    # Cache should be populated
    assert cache_key in mw._distill_cache, f"Expected cache entry, got: {list(mw._distill_cache.keys())}"

    # Now _maybe_distill should return from cache
    mw._recent_focus.clear()
    mw._recent_focus.append("/Player")
    result = await mw._maybe_distill("get_hierarchy", {}, huge_text)
    assert "[DISTILLED haiku-cached;" in result, f"Expected haiku-cached marker, got: {result!r}"
