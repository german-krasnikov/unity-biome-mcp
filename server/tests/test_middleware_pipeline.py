"""Tests for wrap_send dict-response extraction — specifically the file+data path."""
import os
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from unity_mcp.middleware_pipeline import wrap_send, _strip_flags, _check_prefetch_and_circuit


# ── _strip_flags ──────────────────────────────────────────────────────────────

def test_strip_flags_removes_all_internal_flags():
    """_strip_flags must remove all 5 internal marker flags from args."""
    args = {
        "_no_reflect": True,
        "_no_distill": False,
        "_explicit_path": True,
        "_no_validate": True,
        "_no_strip": False,
        "path": "/Obj",
        "value": "42",
    }
    clean, flags = _strip_flags(args)
    for key in ("_no_reflect", "_no_distill", "_explicit_path", "_no_validate", "_no_strip"):
        assert key not in clean
    assert clean == {"path": "/Obj", "value": "42"}


def test_strip_flags_populates_flags_dict():
    """_strip_flags must return flags dict with all 5 keys as bools."""
    args = {"_no_reflect": True, "_no_distill": False}
    _, flags = _strip_flags(args)
    assert flags["_no_reflect"] is True
    assert flags["_no_distill"] is False
    assert flags["_no_strip"] is False  # default when absent


def test_strip_flags_passes_through_non_internal_keys():
    """Non-internal keys must be preserved unchanged."""
    args = {"path": "/A", "component": "Rigidbody", "_no_strip": True}
    clean, _ = _strip_flags(args)
    assert "path" in clean
    assert "component" in clean


# ── _check_prefetch_and_circuit ───────────────────────────────────────────────

def _make_mw_stub(*, circuit_open=False, prefetch_hit=None):
    """Build minimal mw stub for _check_prefetch_and_circuit."""
    mw = MagicMock()
    mw._prefetch_cache = None
    mw.circuit.allow_request.return_value = not circuit_open
    mw.circuit.remaining.return_value = 4
    mw.circuit.state = object()
    mw.circuit.HALF_OPEN = object()  # different object → not half_open
    return mw


def test_check_prefetch_circuit_open_returns_string():
    """When circuit is OPEN, must return ⚡ Circuit OPEN string."""
    mw = _make_mw_stub(circuit_open=True)
    result = _check_prefetch_and_circuit("ping", {}, mw)
    assert result is not None
    assert "Circuit OPEN" in result


def test_check_prefetch_circuit_closed_returns_none():
    """When circuit is closed and no cache, must return None to continue."""
    mw = _make_mw_stub(circuit_open=False)
    result = _check_prefetch_and_circuit("ping", {}, mw)
    assert result is None


def test_check_prefetch_cache_hit_returns_cached():
    """When prefetch cache has a hit, must return the cached string."""
    from unittest.mock import MagicMock
    from unity_mcp.middleware_types import _READ_CACHEABLE
    mw = MagicMock()
    mw.circuit.allow_request.return_value = True
    mw.circuit.state = object()
    mw.circuit.HALF_OPEN = object()
    mw._prefetch_cache = MagicMock()
    mw._prefetch_cache.get.return_value = "cached hierarchy data"
    # Use a known cacheable command
    cmd = next(iter(_READ_CACHEABLE))
    result = _check_prefetch_and_circuit(cmd, {}, mw)
    assert result is not None
    assert "cached hierarchy data" in result


def test_read_cacheable_excludes_get_compile_errors():
    """ARC-5 T2: get_compile_errors' truth changes autonomously (manual edit,
    Hot Reload, another session, or plain wall-clock progress after a
    recompile) with no tracked write in this pipeline to key an invalidation
    off. Membership in _READ_CACHEABLE is the sole "safe to serve up to TTL
    stale" contract — this command must never re-enter it."""
    from unity_mcp.middleware_types import _READ_CACHEABLE
    assert "get_compile_errors" not in _READ_CACHEABLE


# ── TestServeCachedPrefetch ───────────────────────────────────────────────────

class TestServeCachedPrefetch:
    def test_returns_as_is_when_cached_prefix(self):
        """[CACHED:...] prefix → returned unchanged."""
        from unity_mcp.middleware_pipeline import _serve_cached_prefetch
        mw = MagicMock()
        mw.circuit.state = object()
        mw.circuit.HALF_OPEN = object()  # different sentinel → not half_open
        result = _serve_cached_prefetch("[CACHED: get_hierarchy]data", mw)
        assert result == "[CACHED: get_hierarchy]data"
        mw.circuit.record_success.assert_not_called()

    def test_wraps_without_prefix(self):
        """No [CACHED: prefix → prepend [CACHED]\\n."""
        from unity_mcp.middleware_pipeline import _serve_cached_prefetch
        mw = MagicMock()
        mw.circuit.state = object()
        mw.circuit.HALF_OPEN = object()
        result = _serve_cached_prefetch("hierarchy data", mw)
        assert result == "[CACHED]\nhierarchy data"

    def test_records_success_when_half_open(self):
        """HALF_OPEN state → record_success called."""
        from unity_mcp.middleware_pipeline import _serve_cached_prefetch
        sentinel = object()
        mw = MagicMock()
        mw.circuit.state = sentinel
        mw.circuit.HALF_OPEN = sentinel  # same object → is half_open
        _serve_cached_prefetch("data", mw)
        mw.circuit.record_success.assert_called_once()

    def test_no_record_success_when_not_half_open(self):
        """Non-HALF_OPEN state → record_success NOT called."""
        from unity_mcp.middleware_pipeline import _serve_cached_prefetch
        mw = MagicMock()
        mw.circuit.state = object()
        mw.circuit.HALF_OPEN = object()  # distinct sentinel
        _serve_cached_prefetch("data", mw)
        mw.circuit.record_success.assert_not_called()


# ── TestFindObjectsCache ──────────────────────────────────────────────────────

class TestFindObjectsCache:
    def test_returns_none_for_non_find_objects_cmd(self):
        """Non-find_objects cmd → None immediately, no cache call."""
        from unity_mcp.middleware_pipeline import _check_find_objects_cache
        mw = MagicMock()
        assert _check_find_objects_cache("get_hierarchy", {}, mw) is None
        mw.find_from_cache.assert_not_called()

    def test_returns_none_when_tag_set(self):
        """find_objects with tag filter → None (bypass disabled)."""
        from unity_mcp.middleware_pipeline import _check_find_objects_cache
        mw = MagicMock()
        assert _check_find_objects_cache("find_objects", {"tag": "Enemy"}, mw) is None
        mw.find_from_cache.assert_not_called()

    def test_returns_none_when_layer_set(self):
        """find_objects with layer filter → None."""
        from unity_mcp.middleware_pipeline import _check_find_objects_cache
        mw = MagicMock()
        assert _check_find_objects_cache("find_objects", {"layer": "Default"}, mw) is None

    def test_returns_cached_when_cache_hit(self):
        """find_objects, no filters, cache hit → returns cached string."""
        from unity_mcp.middleware_pipeline import _check_find_objects_cache
        mw = MagicMock()
        mw.find_from_cache.return_value = "/Player"
        result = _check_find_objects_cache("find_objects", {"name": "Player"}, mw)
        assert result == "/Player"

    def test_returns_none_when_cache_miss(self):
        """find_objects, no filters, cache miss → None."""
        from unity_mcp.middleware_pipeline import _check_find_objects_cache
        mw = MagicMock()
        mw.find_from_cache.return_value = None
        assert _check_find_objects_cache("find_objects", {}, mw) is None


# ── Item 2: guards see ORIGINAL cmd, speculation sees REROUTED cmd ────────────

async def test_guards_see_original_cmd_before_reroute(monkeypatch):
    """check_blast_radius must receive the original cmd, NOT the rerouted one.

    Scenario: reroute_cmd renames 'set_property' → 'set_runtime_property' in play mode.
    Guards must still evaluate 'set_property' (the original intent), not the rerouted name.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.known_paths.add("/P")
    mw.is_playing = True  # triggers reroute set_property → set_runtime_property

    blast_received = []
    original_blast = mw.check_blast_radius

    def capturing_blast(cmd, args=None):
        blast_received.append(cmd)
        return original_blast(cmd, args)

    mw.check_blast_radius = capturing_blast

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})

    assert blast_received, "check_blast_radius was never called"
    assert blast_received[0] == "set_property", (
        f"Guards must see original 'set_property', got '{blast_received[0]}'"
    )


async def test_speculation_sees_rerouted_cmd(monkeypatch):
    """speculation.record_actual_next must receive the rerouted cmd, not the original.

    Speculation tracks what was ACTUALLY sent to Unity — after rerouting.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unittest.mock import MagicMock
    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.known_paths.add("/P")
    mw.is_playing = True  # triggers reroute set_property → set_runtime_property

    recorded = []
    spec = MagicMock()
    spec.record_actual_next.side_effect = lambda cmd: recorded.append(cmd)
    spec.maybe_prefetch = AsyncMock(side_effect=lambda cmd, args, result: result)
    mw.speculation = spec

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})

    assert recorded, "record_actual_next was never called"
    assert recorded[0] == "set_runtime_property", (
        f"Speculation must see rerouted 'set_runtime_property', got '{recorded[0]}'"
    )


async def test_wrap_send_file_and_data_combined():
    """wrap_send must return both manifest text AND file path when response has both."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "data": "FRONT:Player(vis)\nLEFT:Player(vis)", "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert "FRONT:Player(vis)" in result
    assert "Data saved to: /tmp/mv.png" in result


async def test_wrap_send_file_only_no_data():
    """wrap_send with only 'file' key (no data) must return just the path string."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert result == "Data saved to: /tmp/mv.png"


# --- C1: wrapped()'s own default must not shadow _send_raw's category guard ---


async def test_wrap_send_forwards_zero_not_stale_thirty():
    """wrapped() called with no explicit timeout must forward timeout=0 — the
    sentinel _send_raw's `timeout <= 0` guard resolves via get_timeout(cmd) —
    not a stale hardcoded 30.0 that would shadow the category lookup."""
    received = {}

    async def fake_send(cmd, args, timeout=30.0):
        received["timeout"] = timeout
        return {"ok": True, "data": "ok"}

    wrapped = wrap_send(fake_send)
    await wrapped("ping", {})

    assert received["timeout"] == 0


async def test_wrapped_send_unwraps_raw_dict_result_not_ok():
    """wrapped() must surface the err message (not the raw dict) when ok=False,
    and must classify it as a protocol error (drives dedup_error/recorder)."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": False, "err": "boom"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("get_console", {})
    assert result == "boom"


# ── Phase 5c: REFLECT lines survive distillation ─────────────────────────────

async def test_reflect_lines_preserved_when_distill_strips(monkeypatch):
    """[REFLECT:] annotation added after TCP call must survive _maybe_distill.

    Regression guard: distiller previously received result containing [REFLECT:]
    and could strip it as noise. Fix: extract before distill, re-append after.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "1")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw._distiller_enabled = True

    async def fake_send(cmd, args, timeout=30.0):
        return "set_property result: ok"

    # Distiller that strips everything
    async def stripping_distill(cmd, args, result, no_distill=False):
        return "distilled"

    mw._maybe_distill = stripping_distill

    # Reflect that always reports a mismatch
    async def fake_reflect(cmd, args, result, send_fn):
        from collections import namedtuple
        Mismatch = namedtuple("Mismatch", ["msg"])
        return Mismatch(msg="expected 5 got 3")

    monkeypatch.setattr("unity_mcp.middleware_pipeline.WRITE_CMDS", {"set_property"})

    import unity_mcp.middleware_pipeline as _pl
    _orig_reflect_module = None
    try:
        import unity_mcp.reflect as _reflect_mod
        _orig = _reflect_mod.reflect
        _reflect_mod.reflect = fake_reflect
    except Exception:
        pytest.skip("reflect module not available")

    try:
        wrapped = mw_wrap_send(fake_send, mw)
        result = await wrapped("set_property", {"path": "/A", "prop": "x", "value": "1",
                                                 "_no_validate": True})
        assert "[REFLECT:" in result, (
            f"[REFLECT:] line was stripped by distiller. Got: {result!r}"
        )
    finally:
        _reflect_mod.reflect = _orig


# ── Bug 1: No false circuit-success on pre-TCP guard early exit ───────────────

async def test_circuit_halfopen_pretcp_guard_no_false_success(monkeypatch):
    """HALF_OPEN circuit + pre-TCP guard early exit must NOT call circuit.record_success.

    Regression: when probe_active=True and a guard (e.g. check_retry) returned a string
    early, the pipeline falsely called circuit.record_success(), transitioning the
    circuit to CLOSED — even though no actual TCP communication happened.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.circuit._probe_in_flight = True  # simulate HALF_OPEN probe in flight

    success_calls = []
    mw.circuit.record_success = lambda: success_calls.append(1)

    # Force pre-TCP early exit: check_retry returns a block string (no TCP happens)
    mw.check_retry = lambda cmd, args: "duplicate — retry blocked"

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    result = await wrapped("get_hierarchy", {})

    assert len(success_calls) == 0, (
        f"circuit.record_success must NOT be called on pre-TCP guard early exit; "
        f"called {len(success_calls)} time(s). Result: {result!r}"
    )


async def test_circuit_halfopen_tcp_success_closes_circuit(monkeypatch):
    """HALF_OPEN circuit + successful TCP dispatch must call circuit.record_success.

    Positive case: when the request actually reaches Unity and succeeds, the circuit
    probe is resolved correctly.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()
    mw.circuit._probe_in_flight = True  # simulate HALF_OPEN probe in flight

    success_calls = []
    mw.circuit.record_success = lambda: success_calls.append(1)

    # No early exit — command flows through to TCP
    mw.check_retry = lambda cmd, args: None

    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "data": "ok"}

    wrapped = mw_wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})

    assert len(success_calls) >= 1, (
        "circuit.record_success must be called after successful TCP dispatch"
    )


# ── Bug 2: check_read_only must run before check_retry ───────────────────────

async def test_readonly_guard_runs_before_retry_cache(monkeypatch):
    """check_read_only must reject mutating commands before check_retry caches them.

    Regression: check_retry ran before check_read_only, poisoning the retry cache
    with commands that were never actually sent (later blocked by read-only mode).
    After the fix, check_read_only fires first and check_retry is never reached.
    """
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from mcp.server.fastmcp.exceptions import ToolError
    from unity_mcp.middleware import Middleware, wrap_send as mw_wrap_send

    mw = Middleware()

    retry_calls = []
    original_check_retry = mw.check_retry
    mw.check_retry = lambda cmd, args: (retry_calls.append(cmd), original_check_retry(cmd, args))[1]

    # Read-only mode active for this command
    mw.check_read_only = lambda cmd, args: "read-only mode active"

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = mw_wrap_send(fake_send, mw)
    with pytest.raises(ToolError):
        await wrapped("set_property", {"path": "/A", "prop": "x", "value": "1",
                                       "_no_validate": True})

    assert len(retry_calls) == 0, (
        f"check_retry must NOT be called when check_read_only blocks first; "
        f"check_retry was called with: {retry_calls}"
    )
