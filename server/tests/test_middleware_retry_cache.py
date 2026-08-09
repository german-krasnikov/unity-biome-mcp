"""Cycle 7a: sticky-cache retry tests for Middleware.

RED phase — all tests expected to fail before implementation.
"""
import time
import pytest
from unity_mcp.middleware import Middleware, READ_CMDS


@pytest.fixture
def mw():
    return Middleware()


@pytest.fixture
def mw_validated(monkeypatch):
    """Middleware with schema_cache enabled (overrides conftest UNITY_MCP_VALIDATE=0)."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "1")
    return Middleware()


# ── Test 1: sentinel ─────────────────────────────────────────────────────────

def test_retry_cache_expires_after_ttl(mw):
    """Block within TTL, pass after TTL expires."""
    mw._RETRY_TTL = 0.1
    # First call — allowed
    assert mw.check_retry("set_property", {"path": "/A", "prop": "x"}) is None
    # Second call within TTL — blocked
    r2 = mw.check_retry("set_property", {"path": "/A", "prop": "x"})
    assert r2 is not None and "RETRY" in r2
    # Wait for TTL to expire
    time.sleep(0.15)
    # Third call after TTL — allowed
    assert mw.check_retry("set_property", {"path": "/A", "prop": "x"}) is None


# ── Test 2: read commands never blocked ───────────────────────────────────────

def test_read_commands_never_blocked(mw):
    """READ cmds always return None regardless of repeat count."""
    for cmd in ("get_hierarchy", "get_console", "get_compile_errors",
                "validate_references", "screenshot"):
        for _ in range(20):
            assert mw.check_retry(cmd, {"path": "/same"}) is None


# ── Test 3: reset_session clears state ────────────────────────────────────────

def test_reset_session_clears_state(mw):
    """After reset_session, previously blocked call passes."""
    mw.check_retry("set_property", {"path": "/X"})
    blocked = mw.check_retry("set_property", {"path": "/X"})
    assert blocked is not None  # confirm it was blocked
    mw.reset_session()
    assert mw.check_retry("set_property", {"path": "/X"}) is None


# ── Test: RETRY_CACHE max size eviction ──────────────────────────────────────

def test_retry_cache_max_size_eviction(mw):
    """Cache should not grow beyond _RETRY_MAX entries."""
    for i in range(mw._RETRY_MAX + 10):
        mw.check_retry("set_property", {"path": f"/Obj{i}"})
    assert len(mw._retry_cache) <= mw._RETRY_MAX


# ── Test: write commands ARE blocked on repeat ────────────────────────────────

def test_write_commands_blocked_on_repeat(mw):
    """Write commands (not in READ_CMDS) block on identical repeat."""
    mw._RETRY_TTL = 999.0  # keep TTL long so it doesn't expire
    mw.check_retry("create_object", {"name": "Cube"})
    result = mw.check_retry("create_object", {"name": "Cube"})
    assert result is not None and "RETRY" in result


# ── Test: _hashes dead field removed ─────────────────────────────────────────

def test_no_hashes_field(mw):
    """_hashes was write-only dead code — must no longer exist."""
    assert not hasattr(mw, "_hashes")


# ── Test: reset_session clears both response_hashes and last_writes ───────────

def test_reset_session_clears_response_hashes_and_last_writes(mw):
    """reset_session() must clear both _response_hashes and _last_writes."""
    # populate _response_hashes via check_duplicate_response
    mw._response_hashes.append("abc123")
    # populate _last_writes via check_duplicate_write
    mw._last_writes["k"] = "v"

    mw.reset_session()

    assert len(mw._response_hashes) == 0
    assert len(mw._last_writes) == 0


# ── LRU eviction ORDER: retry cache ──────────────────────────────────────────

def test_retry_cache_evicts_oldest_not_newest(mw):
    """Fill _retry_cache to max+1 via check_retry; oldest entry evicted."""
    mw._RETRY_MAX = 3
    mw._retry_cache.clear()
    # Use real write cmds — unknown cmds are not-write and skip the cache
    mw.check_retry("set_property", {"p": "a"})
    mw.check_retry("create_object", {"p": "b"})
    mw.check_retry("delete_object", {"p": "c"})
    assert len(mw._retry_cache) == 3
    oldest_key = next(iter(mw._retry_cache))
    mw.check_retry("manage_component", {"p": "d"})
    assert len(mw._retry_cache) == 3
    assert oldest_key not in mw._retry_cache


# ── Zone 8: middleware_reads gaps ─────────────────────────────────────────────

def test_update_confidence_floor_at_zero(mw):
    """confidence can't go below 0.0 after repeated writes."""
    mw.confidence = 0.0
    mw.update_confidence("set_property", {}, "ok")
    assert mw.confidence == 0.0


def test_lru_add_component_existing_key_promoted(mw):
    """_lru_add_component with existing key calls move_to_end (promotes it)."""
    mw._MAX_COMPONENTS = 3
    mw._component_cache.clear()
    mw._lru_add_component("/a", "Transform")
    mw._lru_add_component("/b", "Rigidbody")
    # Re-add /a — should promote it to end, /b should now be oldest
    mw._lru_add_component("/a", "Camera")
    # Add /c to fill, then /d to evict oldest
    mw._lru_add_component("/c", "Light")
    mw._lru_add_component("/d", "AudioSource")  # should evict /b (oldest after /a promoted)
    assert "/b" not in mw._component_cache
    assert "/a" in mw._component_cache


def test_lru_add_component_existing_key_accumulates_components(mw):
    """_lru_add_component with same path adds new component to existing set."""
    mw._lru_add_component("/obj", "Transform")
    mw._lru_add_component("/obj", "Rigidbody")
    assert "Transform" in mw._component_cache["/obj"]
    assert "Rigidbody" in mw._component_cache["/obj"]


def test_track_editor_state_invalidates_schema_on_recompile(mw_validated):
    """track_editor_state('recompile', ...) invalidates schema_cache."""
    mw_validated.schema_cache.put("Transform", frozenset(["position"]))
    mw_validated.track_editor_state("recompile", "recompile started")
    assert mw_validated.schema_cache.get("Transform") is None


def test_track_editor_state_invalidates_schema_on_scene(mw_validated):
    """track_editor_state('scene', ...) invalidates schema_cache."""
    mw_validated.schema_cache.put("Rigidbody", frozenset(["mass"]))
    mw_validated.track_editor_state("scene", "scene loaded")
    assert mw_validated.schema_cache.get("Rigidbody") is None


def test_track_editor_state_no_invalidation_for_get_component(mw_validated):
    """Non-recompile/scene commands don't invalidate schema_cache."""
    mw_validated.schema_cache.put("Camera", frozenset(["fov"]))
    mw_validated.track_editor_state("get_component", "result")
    assert mw_validated.schema_cache.get("Camera") is not None


# ── P-433: mutation-aware retry watchdog ─────────────────────────────────────


def test_retry_allows_after_intermediate_mutation(mw):
    """P-433: save -> mutate -> save must NOT be blocked if another write intervened."""
    mw._RETRY_TTL = 999.0  # long TTL, no expiry
    args = {"action": "save"}
    # First save — allowed
    assert mw.check_retry("scene", args) is None
    # Intermediate different write advances retry_generation
    assert mw.check_retry("set_property", {"path": "/X", "prop": "v", "value": "1"}) is None
    # Second save with same hash — must be allowed (intervening write happened)
    assert mw.check_retry("scene", args) is None


def test_retry_blocks_true_duplicate_without_mutation(mw):
    """P-433: save -> save (no mutation) must still be blocked."""
    mw._RETRY_TTL = 999.0
    args = {"action": "save"}
    assert mw.check_retry("scene", args) is None
    result = mw.check_retry("scene", args)
    assert result is not None and "RETRY" in result


def test_retry_blocks_after_intermediate_read(mw):
    """save -> get_component(read) -> save must still be blocked."""
    mw._RETRY_TTL = 999.0
    save_args = {"action": "save"}
    assert mw.check_retry("scene", save_args) is None
    # read must NOT advance the mutation budget
    assert mw.check_retry("get_component", {"path": "/X"}) is None
    result = mw.check_retry("scene", save_args)
    assert result is not None and "RETRY" in result
