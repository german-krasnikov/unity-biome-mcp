import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware import (
    Middleware,
    CircuitBreaker,
    wrap_send,
    BLAST_RADIUS,
)
from unity_mcp.middleware_guards import _is_batch_readonly


# ─── Feature 1: Retry Watchdog ────────────────────────────────────────────────

def test_retry_watchdog_allows_first_call(mw):
    result = mw.check_retry("get_hierarchy", {})
    assert result is None


def test_retry_watchdog_allows_different_calls(mw):
    mw.check_retry("get_hierarchy", {})
    result = mw.check_retry("get_component", {"path": "/A"})
    assert result is None


def test_retry_watchdog_blocks_duplicate(mw):
    # Use write cmd — read cmds (get_hierarchy) are never blocked by design
    mw.check_retry("set_property", {"path": "/A", "prop": "x", "value": "1"})
    result = mw.check_retry("set_property", {"path": "/A", "prop": "x", "value": "1"})
    assert "RETRY" in result


def test_retry_watchdog_allows_same_cmd_different_args(mw):
    # Use write cmd — read cmds are never blocked
    mw.check_retry("set_property", {"path": "/A"})
    result = mw.check_retry("set_property", {"path": "/B"})
    assert result is None


# ─── Feature 2: Confidence Decay ─────────────────────────────────────────────

def test_confidence_appended_to_response(mw):
    # Fresh mw has confidence 1.0 — after a read it stays 1.0 (capped), no suffix
    result = mw.update_confidence("get_hierarchy", {}, "some output")
    assert "[confidence:" not in result


def test_confidence_suffix_appears_when_low(mw):
    # 0.4 + 0.15 = 0.55 >= 0.5 → no suffix
    mw.confidence = 0.4
    result = mw.update_confidence("get_hierarchy", {}, "data")
    assert "[confidence:" not in result
    # 0.2 - 0.08 = 0.12 < 0.5 → suffix present
    mw.confidence = 0.2
    result = mw.update_confidence("set_property", {}, "data")
    assert "[confidence:" in result


def test_confidence_decreases_on_write(mw):
    before = mw.confidence
    mw.update_confidence("set_property", {}, "ok")
    assert mw.confidence < before


def test_confidence_increases_on_read(mw):
    mw.confidence = 0.5
    mw.update_confidence("get_hierarchy", {}, "ok")
    assert mw.confidence > 0.5


def test_confidence_caps_at_1(mw):
    mw.confidence = 0.95
    mw.update_confidence("get_hierarchy", {}, "ok")
    assert mw.confidence <= 1.0


def test_confidence_low_warning(mw):
    mw.confidence = 0.15  # after write: 0.15 - 0.08 = 0.07 < 0.3
    result = mw.update_confidence("set_property", {}, "data")
    assert "LOW CONFIDENCE" in result


# ─── Feature 3: Taint Tracking ───────────────────────────────────────────────

def test_taint_warns_unverified_reference(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"})
    assert "TAINT" in result


def test_taint_allows_verified_value(mw):
    mw.record_read("get_component", {"path": "/Enemy"}, "Enemy component data")
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"})
    assert result is None


def test_taint_ignores_non_reference_prop(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "speed", "value": "5"})
    assert result is None


def test_taint_allows_hash_ref(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "#abc123"})
    assert result is None


# ─── Feature 4: Periodic State Injection ─────────────────────────────────────

async def test_state_injection_every_10_calls(mw):
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 10  # pipeline already incremented; maybe_inject_state checks, not increments
    result = await mw.maybe_inject_state(fake_send, "original result", "set_property")
    assert "AUTO STATE" in result
    assert "HierarchyData" in result


async def test_auto_state_staleness_gate(mw):
    """Injects at call 10, then again at call 20 (gap > 5)."""
    fake_send = AsyncMock(return_value="H")
    mw.call_count = 10  # pipeline already incremented
    mw._last_hierarchy_call = 0
    await mw.maybe_inject_state(fake_send, "r", "set_property")
    assert mw._last_hierarchy_call == 10
    fake_send.reset_mock()
    # second injection: call_count=20, gap=20-10=10 > 5
    mw.call_count = 20
    await mw.maybe_inject_state(fake_send, "r", "set_property")
    assert mw._last_hierarchy_call == 20
    fake_send.assert_called_once()


async def test_auto_state_skipped_when_recent(mw):
    """Auto-inject skipped when organic get_hierarchy was within last 5 calls.

    Drives a real get_hierarchy through wrapped() to set _last_hierarchy_call,
    then triggers the % 10 boundary — gap is <= 5 so injection must NOT fire.
    """
    call_log: list[str] = []

    async def fake_send(cmd, args, timeout=30.0):
        call_log.append(cmd)
        return "HierarchyData"

    mw = Middleware()
    wrapped = wrap_send(fake_send, mw)

    # Push 7 calls so call_count reaches 7; one of them is get_hierarchy (organic)
    for _ in range(6):
        await wrapped("get_component", {"path": "/A", "type": "T"})
    # Organic get_hierarchy at call_count=7
    await wrapped("get_hierarchy", {})
    # _last_hierarchy_call is now 7

    # Push 3 more to reach call_count=10 (% 10 boundary)
    # gap = 10 - 7 = 3, not > 5 → no auto-inject
    call_log.clear()
    for _ in range(3):
        await wrapped("get_component", {"path": "/A", "type": "T"})

    # get_hierarchy must NOT appear in call_log (no auto-injection)
    assert "get_hierarchy" not in call_log, (
        f"Auto-inject fired despite recent organic hierarchy (gap<=5). calls: {call_log}"
    )


async def test_auto_state_organic_hierarchy_suppresses_injection(mw):
    """Organic get_hierarchy within 5 calls before a % 10 boundary skips auto-inject."""
    call_log: list[str] = []

    async def fake_send(cmd, args, timeout=30.0):
        call_log.append(cmd)
        return "H"

    mw2 = Middleware()
    wrapped = wrap_send(fake_send, mw2)

    # Get to call 48 via non-hierarchy calls
    for _ in range(48):
        await wrapped("get_component", {"path": "/A", "type": "T"})

    # Organic get_hierarchy at call 49
    call_log.clear()
    await wrapped("get_hierarchy", {})
    assert mw2._last_hierarchy_call == 49

    # Call 50 hits % 10; gap = 50 - 49 = 1, not > 5 → no auto-inject
    call_log.clear()
    await wrapped("get_component", {"path": "/A", "type": "T"})
    assert "get_hierarchy" not in call_log


def test_reset_session_clears_hierarchy_call_counter(mw):
    mw._last_hierarchy_call = 50
    mw.reset_session()
    assert mw._last_hierarchy_call == 0


async def test_state_injection_not_before_10(mw):
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 0
    result = await mw.maybe_inject_state(fake_send, "original result")
    assert "AUTO STATE" not in result
    fake_send.assert_not_called()


async def test_auto_state_skips_read_cmds(mw):
    """Reads must never trigger AUTO STATE injection."""
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 10
    mw._last_hierarchy_call = 0
    for read_cmd in ("get_hierarchy", "get_component", "get_compile_errors", "screenshot"):
        result = await mw.maybe_inject_state(fake_send, "original", read_cmd)
        assert "AUTO STATE" not in result, f"AUTO STATE fired on read cmd '{read_cmd}'"
    fake_send.assert_not_called()


async def test_auto_state_fires_on_write_cmds(mw):
    """Writes at the % 10 boundary must inject AUTO STATE."""
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 10
    mw._last_hierarchy_call = 0
    result = await mw.maybe_inject_state(fake_send, "original", "set_property")
    assert "AUTO STATE" in result
    fake_send.assert_called_once()


async def test_auto_state_skips_readonly_batch(mw):
    """batch of read-only tools must NOT trigger AUTO STATE injection."""
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 10
    mw._last_hierarchy_call = 0
    cmds = "diagnose\nscene_health\nvalidate_references\nresolve_scene_refs"
    result = await mw.maybe_inject_state(fake_send, "original", "batch", {"commands": cmds})
    assert "AUTO STATE" not in result
    fake_send.assert_not_called()


async def test_state_injection_increments_counter(mw):
    """call_count is incremented by the pipeline (wrap_send), not maybe_inject_state."""
    fake_send = AsyncMock(return_value="HierarchyData")
    wrapped = wrap_send(fake_send, mw)
    mw.call_count = 0
    await wrapped("get_component", {"path": "/A", "type": "T"})
    assert mw.call_count == 1


# ─── Feature 5: Path Cache ────────────────────────────────────────────────────

def test_path_cache_empty_allows_all(mw):
    result = mw.validate_path("UnknownObject")
    assert result is None  # no cache yet


def test_path_cache_updated_on_hierarchy(mw):
    # Real Unity format: scene name at depth 0, objects at depth 1+
    hierarchy = "SampleScene\n├─ Main Camera $[Camera]\n├─ Player $[Transform]\n└─ Enemy $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    assert "/Player" in mw.known_paths
    assert "/Enemy" in mw.known_paths


def test_path_cache_warns_unknown_path(mw):
    hierarchy = "SampleScene\n├─ Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("/NonExistent")
    assert "PATH WARNING" in result


def test_path_cache_allows_known_path(mw):
    hierarchy = "SampleScene\n├─ Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("/Player")
    assert result is None


def test_path_cache_allows_ref_syntax(mw):
    hierarchy = "Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("$ref:abc")
    assert result is None


# ─── Feature 6: Dead Write Elimination ───────────────────────────────────────

def test_dead_write_warns_overwrite(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "2"})
    assert "OVERWRITE" in result


def test_dead_write_no_warn_first_time(mw):
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    assert result is None


def test_dead_write_cleared_on_read(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    mw.clear_write_on_read("get_component", {"path": "/A"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "2"})
    assert result is None


def test_dead_write_different_prop_no_warn(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "y", "value": "5"})
    assert result is None


# ─── Feature 7: Circuit Breaker ──────────────────────────────────────────────

def test_circuit_breaker_closed_allows():
    cb = CircuitBreaker()
    assert cb.allow_request() is True


def test_circuit_breaker_opens_after_3_failures():
    cb = CircuitBreaker(threshold=3)
    cb.record_failure()
    cb.record_failure()
    cb.record_failure()
    assert cb.get_status() == "OPEN"


def test_circuit_breaker_blocks_when_open():
    cb = CircuitBreaker(threshold=1, cooldown=9999.0)
    cb.record_failure()
    assert cb.allow_request() is False


def test_circuit_breaker_half_open_after_cooldown():
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    cb.opened_at -= 1.0
    assert cb.allow_request() is True
    assert cb.get_status() == "HALF_OPEN"


def test_circuit_breaker_closes_on_success():
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    cb.opened_at -= 1.0
    cb.allow_request()  # transitions to HALF_OPEN
    cb.record_success()
    assert cb.get_status() == "CLOSED"


# ─── wrap_send circuit integration ───────────────────────────────────────────

async def test_wrap_send_circuit_open_returns_error():
    mw = Middleware()
    mw.circuit.failures = mw.circuit.threshold  # force open
    mw.circuit.state = CircuitBreaker.OPEN
    mw.circuit.opened_at = 9e18  # never expires

    fake_send = AsyncMock(return_value="ok")
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_hierarchy", {})
    assert "circuit" in result.lower()
    fake_send.assert_not_called()


async def test_wrap_send_circuit_records_success():
    mw = Middleware()
    fake_send = AsyncMock(return_value="hierarchy data")
    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    assert mw.circuit.failures == 0
    assert mw.circuit.get_status() == "CLOSED"


async def test_wrap_send_circuit_records_failure():
    mw = Middleware()
    fake_send = AsyncMock(side_effect=ConnectionError("TCP down"))
    wrapped = wrap_send(fake_send, mw)
    with pytest.raises(ConnectionError):
        await wrapped("get_hierarchy", {})
    assert mw.circuit.failures == 1


# ─── Feature 4 (new): Starvation Monitor ─────────────────────────────────────

def test_starvation_warns_after_5_identical(mw):
    for _ in range(5):
        result = mw.check_starvation("same result")
    assert "STARVATION" in result


def test_starvation_no_warn_on_different(mw):
    results = ["result A", "result B", "result C", "result D", "result E"]
    for r in results:
        result = mw.check_starvation(r)
    assert "STARVATION" not in result


def test_starvation_no_warn_before_5(mw):
    for _ in range(4):
        result = mw.check_starvation("same result")
    assert "STARVATION" not in result


# ─── Feature 5 (new): Blast Radius Tags ──────────────────────────────────────

def test_blast_radius_warns_high(mw):
    warn = mw.check_blast_radius("delete_object")
    assert warn is not None
    assert "BLAST RADIUS" in warn


def test_blast_radius_silent_low(mw):
    warn = mw.check_blast_radius("get_hierarchy")
    assert warn is None


def test_blast_radius_table_has_entries():
    assert "delete_object" in BLAST_RADIUS
    assert BLAST_RADIUS["delete_object"] >= 3
    assert BLAST_RADIUS["get_hierarchy"] == 0


def test_blast_radius_delete_object_is_4_scene_batch_still_3():
    """Phase 5b: delete_object raised to 4; scene/batch stay at 3."""
    assert BLAST_RADIUS["delete_object"] == 4
    assert BLAST_RADIUS["scene"] == 3
    assert BLAST_RADIUS["batch"] == 3


# ─── P2: Readonly-batch blast-radius suppression ─────────────────────────────

def test_is_batch_readonly_all_reads():
    cmds = "get_component path=Foo type=Bar\nget_hierarchy root=Foo depth=1"
    assert _is_batch_readonly(cmds) is True


def test_is_batch_readonly_mixed():
    cmds = "get_component path=Foo type=Bar\nset_property path=Foo prop=x value=1"
    assert _is_batch_readonly(cmds) is False


def test_is_batch_readonly_empty_string():
    assert _is_batch_readonly("") is True


def test_is_batch_readonly_blank_lines_only():
    assert _is_batch_readonly("   \n  \n") is True


def test_blast_radius_readonly_batch_no_warn(mw):
    cmds = "get_component path=Foo type=Bar\nget_hierarchy root=Foo"
    assert mw.check_blast_radius("batch", {"commands": cmds}) is None


def test_blast_radius_resolve_scene_refs_batch_no_warn(mw):
    """resolve_scene_refs is read-only — a batch of verify tools must not warn."""
    cmds = "diagnose\nscene_health\nvalidate_references\nresolve_scene_refs"
    assert mw.check_blast_radius("batch", {"commands": cmds}) is None


def test_blast_radius_write_batch_warns(mw):
    """High-blast write (delete_object=4) in batch must still warn."""
    cmds = "delete_object path=Foo"
    assert mw.check_blast_radius("batch", {"commands": cmds}) is not None


def test_blast_radius_low_write_batch_no_warn(mw):
    """Low-blast write (set_property=1) in batch does NOT warn."""
    cmds = "set_property path=Foo prop=x value=1"
    assert mw.check_blast_radius("batch", {"commands": cmds}) is None


def test_blast_radius_no_args_still_warns(mw):
    # backward compat: batch without args → conservative, warns
    assert mw.check_blast_radius("batch") is not None


def test_verification_readonly_batch_no_count(mw):
    cmds = "get_component path=Foo type=Bar"
    before = mw._mutation_count
    mw.check_verification_needed("batch", {"commands": cmds})
    assert mw._mutation_count == before


def test_transition_readonly_batch_resets_consecutive_writes(mw):
    mw._consecutive_writes = 2
    mw.transition("batch", {"commands": "get_component path=Foo type=Bar"})
    assert mw._consecutive_writes == 0


# ─── Feature 6 (new): Incremental Verification ───────────────────────────────

def test_verification_checkpoint_every_10(mw):
    for _ in range(9):
        mw.check_verification_needed("set_property")
    result = mw.check_verification_needed("set_property")
    assert "VERIFICATION" in result


def test_verification_no_warn_before_10(mw):
    for _ in range(9):
        result = mw.check_verification_needed("set_property")
    assert result is None


def test_verification_only_counts_writes(mw):
    for _ in range(5):
        result = mw.check_verification_needed("get_hierarchy")
    assert result is None


# ─── Feature 7 (new): Alive Check ────────────────────────────────────────────

def test_alive_check_recent_ok(mw):
    mw._last_success = __import__("time").time()
    assert mw.check_alive() is True


def test_alive_check_stale_false(mw):
    mw._last_success = 0.0  # epoch — definitely stale
    assert mw.check_alive() is False


# ─── Feature 12: Workflow Phase FSM ──────────────────────────────────────────

def test_workflow_fsm_warns_after_3_writes(mw):
    mw.transition("set_property")
    mw.transition("set_property")
    result = mw.transition("set_property")
    assert "write" in result.lower() or "verif" in result.lower()


def test_workflow_fsm_resets_on_read(mw):
    mw.transition("set_property")
    mw.transition("set_property")
    mw.transition("get_hierarchy")  # read resets counter
    result = mw.transition("set_property")
    assert result is None


# ─── F08: strip_defaults in wrap_send ────────────────────────────────────────

async def test_wrap_send_strips_defaults_for_get_component():
    """position (0,0,0) stripped; mass:1 kept (not a Unity internal field); localPosition kept."""
    # Item 22: "mass: 1" is a user-facing field — NOT stripped. Only "m_mass: 1" is stripped.
    raw = "[Transform]\nposition: (0, 0, 0)\nlocalPosition: (5, 3, 0)\n[Rigidbody]\nmass: 1\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/A", "type": ""})
    assert "position: (0, 0, 0)" not in result
    assert "mass: 1" in result
    assert "localPosition: (5, 3, 0)" in result


async def test_wrap_send_no_strip_for_hierarchy():
    """get_hierarchy NOT in _STRIP_CMDS — drag:0 kept."""
    raw = "SampleScene\nRigidbody drag: 0\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_hierarchy", {})
    assert "drag: 0" in result


async def test_wrap_send_no_strip_escape_hatch():
    """_no_strip=True skips stripping even for get_component."""
    raw = "[Transform]\nposition: (0, 0, 0)\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/A", "_no_strip": True})
    assert "position: (0, 0, 0)" in result


# ── Bounded LRU on Middleware collections ─────────────────────────────────────

def test_clean_paths_bounded():
    """_clean_paths must not exceed 256 entries."""
    mw = Middleware()
    for i in range(300):
        mw.record_read("get_component", {"path": f"/Obj{i}"}, "")
    assert len(mw._clean_paths) <= 256


def test_component_cache_bounded():
    """_component_cache must not exceed 256 entries."""
    mw = Middleware()
    for i in range(300):
        mw.cache_components("get_component", {"path": f"/Obj{i}", "type": "Rigidbody"}, "")
    assert len(mw._component_cache) <= 256


def test_last_writes_bounded():
    """_last_writes must not exceed 128 entries."""
    mw = Middleware()
    for i in range(200):
        mw.check_dead_write("set_property", {"path": f"/Obj{i}", "component": "T", "prop": f"p{i}", "value": "v"})
    assert len(mw._last_writes) <= 128


# ─── LRU eviction ORDER: component cache ─────────────────────────────────────

def test_component_cache_evicts_oldest_not_newest():
    """Fill _component_cache to max+1; oldest path evicted, newest kept."""
    mw = Middleware()
    mw._MAX_COMPONENTS = 3
    mw._component_cache.clear()
    mw._lru_add_component("/a", "C")
    mw._lru_add_component("/b", "C")
    mw._lru_add_component("/c", "C")
    mw._lru_add_component("/d", "C")  # overflow — /a is oldest
    assert "/a" not in mw._component_cache
    assert "/b" in mw._component_cache
    assert "/c" in mw._component_cache
    assert "/d" in mw._component_cache


# ─── CircuitBreaker.remaining ────────────────────────────────────────────────

def test_circuit_breaker_remaining_when_open():
    """remaining() returns positive time while circuit is open."""
    cb = CircuitBreaker(threshold=1, cooldown=30.0)
    cb.record_failure()
    assert cb.state == CircuitBreaker.OPEN
    r = cb.remaining()
    assert 0.0 < r <= 30.0


def test_circuit_breaker_remaining_when_closed():
    """remaining() returns 0.0 when circuit is closed (cooldown not started)."""
    cb = CircuitBreaker(threshold=3, cooldown=15.0)
    # opened_at=0.0, so monotonic()-0 >> cooldown → max(0, negative) = 0.0
    assert cb.remaining() == 0.0


def test_circuit_breaker_remaining_after_expired():
    """remaining() returns 0.0 once cooldown has passed."""
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    assert cb.remaining() == 0.0


# ─── Middleware.__init__ log-dir branch ──────────────────────────────────────

def test_middleware_init_creates_log_dir(tmp_path, monkeypatch):
    """When UNITY_MCP_LOG_DIR is set to a non-existent dir, it gets created."""
    log_dir = str(tmp_path / "new_log_dir")
    monkeypatch.setenv("UNITY_MCP_LOG_DIR", log_dir)
    mw2 = Middleware()
    import os
    assert os.path.isdir(log_dir)
    assert mw2._mutation_log is not None
    mw2._mutation_log.close()


def test_middleware_init_no_log_dir_by_default(monkeypatch):
    """Without UNITY_MCP_LOG_DIR, _mutation_log stays None."""
    monkeypatch.delenv("UNITY_MCP_LOG_DIR", raising=False)
    mw2 = Middleware()
    assert mw2._mutation_log is None


# ── CircuitBreaker HALF_OPEN probe flag ───────────────────────────────────────

def test_circuit_breaker_half_open_allows_all_concurrent():
    """In HALF_OPEN state, ALL requests pass (P-092: no false 'Circuit OPEN' for concurrent
    requests while probe is in flight). The circuit closes on success, re-opens on enough
    failures — concurrent requests are not blocked by the probe slot."""
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()  # → OPEN
    cb.state = CircuitBreaker.HALF_OPEN
    cb._probe_in_flight = False
    assert cb.allow_request()   # first request passes
    assert cb.allow_request()   # second also passes (not blocked)


# P-092 / G15: concurrent requests must not trip circuit when all succeed
async def test_circuit_stays_closed_on_concurrent_success():
    """G15: 3 concurrent requests all succeeding must not produce any 'Circuit OPEN'
    response, even if circuit is in HALF_OPEN while the first probe runs.

    Root cause: HALF_OPEN probe slot blocks subsequent concurrent requests
    immediately with 'Circuit OPEN', even though the probe (first request)
    succeeds and closes the circuit. The others should pass through.

    asyncio.sleep(0) inside send_fn ensures a true event-loop yield, so
    R2 and R3 call allow_request() while R1's probe is still in flight."""
    import asyncio
    mw = Middleware()
    # Force OPEN circuit with expired cooldown → HALF_OPEN on first allow_request()
    mw.circuit.failures = mw.circuit.threshold
    mw.circuit.state = CircuitBreaker.OPEN
    mw.circuit.opened_at = 0.0  # cooldown already expired

    async def succeed(cmd, args, timeout=0):
        await asyncio.sleep(0)  # force event-loop switch so R2/R3 see HALF_OPEN
        return "ok"

    wrapped = wrap_send(succeed, mw)
    results = await asyncio.gather(
        wrapped("get_hierarchy", {}),
        wrapped("get_hierarchy", {}),
        wrapped("get_hierarchy", {}),
    )
    circuit_open_responses = [r for r in results if "Circuit OPEN" in r or "circuit" in r.lower()]
    assert circuit_open_responses == [], (
        f"Concurrent requests got 'Circuit OPEN': {circuit_open_responses}"
    )
    assert mw.circuit.get_status() == "CLOSED"


def test_circuit_breaker_half_open_resets_after_success():
    """After probe succeeds (record_success), circuit transitions back to CLOSED."""
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    cb.state = CircuitBreaker.HALF_OPEN
    cb._probe_in_flight = False
    cb.allow_request()
    cb.record_success()
    assert cb.state == CircuitBreaker.CLOSED
    assert cb.allow_request()


# ── reset_session completeness ────────────────────────────────────────────────

def test_reset_session_clears_is_playing(mw):
    """reset_session must reset is_playing to False."""
    mw.is_playing = True
    mw.reset_session()
    assert mw.is_playing is False


def test_reset_session_resets_circuit_breaker(mw):
    """reset_session must reset the circuit breaker state to CLOSED."""
    mw.circuit.state = CircuitBreaker.OPEN
    mw.circuit.failures = 5
    mw.reset_session()
    assert mw.circuit.state == CircuitBreaker.CLOSED
    assert mw.circuit.failures == 0


def test_reset_session_clears_schema_cache(mw):
    """reset_session must invalidate schema_cache if present."""
    from unity_mcp.schema_cache import SchemaCache
    from unity_mcp.schema_guard import SchemaGuard
    if mw.schema_cache is None:
        mw.schema_cache = SchemaCache()
        mw.schema_guard = SchemaGuard(mw, mw.schema_cache)
    mw.schema_cache.put("Rigidbody", frozenset(["mass"]))
    mw.reset_session()
    assert mw.schema_cache.get("Rigidbody") is None


def test_reset_session_clears_component_cache(mw):
    """reset_session must clear _component_cache."""
    mw._component_cache["/Cube"] = {"Transform"}
    mw.reset_session()
    assert len(mw._component_cache) == 0


# ── Component cache update after manage_component ────────────────────────────

def test_cache_components_adds_on_manage_component_add(mw):
    """cache_components must update the cache when manage_component add succeeds."""
    mw._component_cache["/Cube"] = {"Transform"}
    mw.cache_components("manage_component", {"path": "/Cube", "type": "Rigidbody", "action": "add"}, "ok: added")
    assert "Rigidbody" in mw._component_cache.get("/Cube", set())


def test_cache_components_removes_on_manage_component_remove(mw):
    """cache_components must remove the component from cache on remove action."""
    mw._component_cache["/Cube"] = {"Transform", "Rigidbody"}
    mw.cache_components("manage_component", {"path": "/Cube", "type": "Rigidbody", "action": "remove"}, "ok: removed")
    assert "Rigidbody" not in mw._component_cache.get("/Cube", set())


def test_cache_components_clears_on_delete_object(mw):
    """cache_components must remove the path from cache on delete_object."""
    mw._component_cache["/Cube"] = {"Transform"}
    mw.cache_components("delete_object", {"path": "/Cube"}, "ok: deleted")
    assert "/Cube" not in mw._component_cache


# ── F16: error deduplication ──────────────────────────────────────────────────

def test_error_dedup_first_occurrence_returns_full_message(mw):
    """F16: first occurrence of an error must return full message."""
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result == "Error: path not found"


def test_error_dedup_second_identical_error_returns_repeated_form(mw):
    """F16: second identical error must return collapsed '(repeated 2x)' form."""
    mw.dedup_error("set_property", "Error: path not found")
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result.startswith("(repeated 2x)")


def test_error_dedup_same_text_different_cmd_not_collapsed(mw):
    """F16: same error text for different cmd must NOT be collapsed."""
    mw.dedup_error("set_property", "Error: path not found")
    result = mw.dedup_error("create_object", "Error: path not found")
    assert not result.startswith("(repeated")


def test_error_dedup_cleared_on_reset_session(mw):
    """F16: _error_dedup must be cleared on reset_session."""
    mw.dedup_error("set_property", "Error: path not found")
    mw.reset_session()
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result == "Error: path not found"


async def test_error_dedup_ignores_success_payload_containing_error_substring():
    """F16-regression: SUCCESS read whose payload merely contains 'Error' must not be deduped."""
    mw = Middleware()
    mw._prefetch_cache = None
    payload = "[Log] 12:00 ErrorManager started\n[Warning] mem high\n[Log] Player spawned"

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": payload}

    wrapped = wrap_send(send_fn, mw)
    r1 = await wrapped("get_console", {})
    r2 = await wrapped("get_console", {})
    assert "Player spawned" in r1 and "Player spawned" in r2
    assert not r2.startswith("(repeated")


def test_error_dedup_long_errors_differing_past_char80_are_distinct(mw):
    """F16: two errors differing only past char 80 must be treated as distinct (full-key)."""
    base = "Error: Component not found on path /Game/UI/Canvas/Panel/SubPanel/HealthBar/Fill"
    assert len(base) >= 80
    mw.dedup_error("get_component", base + "A")
    result = mw.dedup_error("get_component", base + "B")
    assert not result.startswith("(repeated")
    assert result == base + "B"


def test_error_dedup_dict_stays_bounded_over_long_session(mw):
    """F16: _error_dedup must stay bounded (no unbounded growth)."""
    for i in range(400):
        mw.dedup_error("set_property", f"Error #{i}")
    assert len(mw._error_dedup) <= 256


async def test_lesson_recorder_classifies_by_ok_flag_not_error_substring():
    """F16-regression: LessonRecorder must classify on ok-flag, not substring scan."""
    from unity_mcp.lessons import LessonRecorder, LessonStore
    mw = Middleware()
    mw._prefetch_cache = None
    mw.recorder = LessonRecorder(LessonStore(path=None))

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "[Error] NullRef in Foo\n[Error] again\n[Log] ok"}

    wrapped = wrap_send(send_fn, mw)
    for _ in range(4):
        await wrapped("get_console", {"level": "Error"})
    assert mw.recorder._recent_fails == {}


# ── circuit breaker is_ready_fn + cache above circuit ────────────────────────

def test_circuit_ready_fn_true_transitions_open_to_half_open():
    """is_ready_fn returning True in OPEN state transitions the circuit to HALF_OPEN."""
    ready_calls = [0]

    def is_ready():
        ready_calls[0] += 1
        return True

    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=is_ready)
    cb.record_failure()
    assert cb.state == CircuitBreaker.OPEN
    allowed = cb.allow_request()
    assert allowed is True
    assert cb.state == CircuitBreaker.HALF_OPEN
    assert ready_calls[0] == 1


def test_circuit_ready_fn_none_uses_time_based_cooldown():
    """Without is_ready_fn, OPEN state uses time-based cooldown."""
    cb = CircuitBreaker(threshold=1, cooldown=9999.0)
    cb.record_failure()
    assert not cb.allow_request()
    assert cb.state == CircuitBreaker.OPEN


def test_circuit_ready_fn_false_stays_open():
    """is_ready_fn returning False must not transition the circuit to HALF_OPEN."""
    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=lambda: False)
    cb.record_failure()
    assert not cb.allow_request()
    assert cb.state == CircuitBreaker.OPEN


def test_circuit_ready_fn_exception_falls_through_to_cooldown():
    """is_ready_fn raising must not crash allow_request; falls through to cooldown."""
    def bad_fn():
        raise RuntimeError("probe error")
    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=bad_fn)
    cb.record_failure()
    assert not cb.allow_request()


async def test_prefetch_cache_hit_served_when_circuit_open():
    """PrefetchCache hit for a cacheable read must be served even when the circuit is OPEN."""
    from unity_mcp.prefetch_cache import PrefetchCache
    mw = Middleware()
    mw.circuit = CircuitBreaker(threshold=1, cooldown=9999.0)
    mw.circuit.record_failure()
    assert mw.circuit.state == CircuitBreaker.OPEN

    mw._prefetch_cache = PrefetchCache()
    mw._prefetch_cache.put("get_component", {"path": "/Cube", "type": "Transform"}, "Transform data")

    send_called = [False]
    async def send_fn(cmd, args, timeout=30.0):
        send_called[0] = True
        return {"ok": True, "data": "from unity"}

    wrapped = wrap_send(send_fn, mw)
    result = await wrapped("get_component", {"path": "/Cube", "type": "Transform"})
    assert not send_called[0]
    assert "Transform data" in result or "CACHED" in result


async def test_non_cacheable_write_blocked_when_circuit_open():
    """Non-cacheable write commands must still be blocked when the circuit is OPEN."""
    mw = Middleware()
    mw.circuit = CircuitBreaker(threshold=1, cooldown=9999.0)
    mw.circuit.record_failure()
    assert mw.circuit.state == CircuitBreaker.OPEN

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "ok"}

    wrapped = wrap_send(send_fn, mw)
    result = await wrapped("set_property", {"path": "/Cube", "component": "Transform", "prop": "x", "value": "1"})
    assert "Circuit OPEN" in result


# ── F17: negative path cache ─────────────────────────────────────────────────

async def test_negative_path_cache_skips_tcp_on_second_unknown_path_call():
    """F17: second unknown-path call within TTL must NOT invoke send_fn (cache hit)."""
    mw = Middleware()
    mw.known_paths = {"/Known"}

    call_count = [0]
    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""

    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert call_count[0] == 1
    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert call_count[0] == 1, "Second call must use negative cache, not TCP"


async def test_negative_path_cache_queries_tcp_again_after_ttl():
    """F17: after TTL expires, unknown path must query TCP again."""
    from unittest.mock import patch as mock_patch
    mw = Middleware()
    mw.known_paths = {"/Known"}

    call_count = [0]
    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""

    with mock_patch("unity_mcp.middleware_paths.time") as mock_time:
        mock_time.monotonic.return_value = 1000.0
        await mw.resolve_path_live("/UnknownPath", send_fn)
        assert call_count[0] == 1

        mock_time.monotonic.return_value = 1012.0
        await mw.resolve_path_live("/UnknownPath", send_fn)
        assert call_count[0] == 2, "Expired cache must re-query TCP"


async def test_negative_path_cache_cleared_on_reset_session():
    """F17: reset_session must clear negative path cache."""
    mw = Middleware()
    mw.known_paths = {"/Known"}

    async def send_fn(cmd, args, timeout=30.0):
        return ""

    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert len(mw._negative_path_cache) > 0
    mw.reset_session()
    assert len(mw._negative_path_cache) == 0


async def test_negative_path_cache_ignores_known_paths():
    """F17: known paths must not be added to negative cache."""
    mw = Middleware()
    mw.known_paths = {"/Known"}

    call_count = [0]
    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""

    await mw.resolve_path_live("/Known", send_fn)
    assert call_count[0] == 0
    assert "/Known" not in mw._negative_path_cache


async def test_transient_tcp_failure_does_not_add_path_to_negative_cache():
    """F17-regression: transient TCP failure must NOT add path to negative cache."""
    mw = Middleware()
    mw.known_paths = {"/Known"}

    async def send_fn(cmd, args, timeout=30.0):
        raise ConnectionError("TCP dropped")

    resolved, _ = await mw.resolve_path_live("/UnknownPath", send_fn)
    assert resolved == "/UnknownPath"
    assert "/UnknownPath" not in mw._negative_path_cache


async def test_write_command_drops_negative_path_cache():
    """F17-regression: a write must drop the negative cache."""
    import time as time_mod
    mw = Middleware()
    mw._prefetch_cache = None
    mw.known_paths = set()
    mw._negative_path_cache = {"/Ghost": time_mod.monotonic() + 999}

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "created"}

    wrapped = wrap_send(send_fn, mw)
    await wrapped("create_object", {"name": "Ghost"})
    assert mw._negative_path_cache == {}


# ── Item 9: reset_session must cancel bg tasks ─────────────────────────────


async def test_reset_session_cancels_bg_tasks():
    """reset_session must cancel asyncio tasks before clearing _bg_tasks.

    Bug: old code called self._bg_tasks.clear() which drops references but
    leaves the asyncio tasks running (uncancelled) in the event loop.
    """
    import asyncio as _asyncio
    mw = Middleware()

    # Create a real long-running task and register it
    async def long_running():
        await _asyncio.sleep(9999)

    task = _asyncio.ensure_future(long_running())
    mw._bg_tasks.add(task)

    mw.reset_session()

    # Give event loop one cycle to process the cancellation
    await _asyncio.sleep(0)

    assert task.cancelled(), "reset_session must cancel bg tasks, not just drop references"
