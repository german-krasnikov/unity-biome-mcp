"""Integration tests for wrap_send pipeline ordering, flag-stripping, and circuit-cache seam.

Covers: PY6.arch.3, PY6.arch.4, PY6.test.2, PY6.test.4, PY6.test.5, X5.cross.4
"""
import asyncio
import os
from unittest.mock import AsyncMock


# ── PY6.arch.3: HALF_OPEN + cache hit → record_success() ─────────────────────

async def test_half_open_cache_hit_heals_circuit(monkeypatch):
    """Cache hit on a HALF_OPEN probe must call record_success() → circuit CLOSED."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "1")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")

    from unity_mcp.middleware import Middleware, wrap_send
    from unity_mcp.middleware_types import CircuitBreaker

    mw = Middleware()

    # Seed PrefetchCache with a cached response for get_component
    mw._prefetch_cache.put("get_component", {"path": "/A", "type": "T"}, "cached-value")

    # Force circuit into HALF_OPEN — first probe slot available
    mw.circuit.state = CircuitBreaker.HALF_OPEN
    mw.circuit._probe_in_flight = False  # probe slot free

    async def fake_send(cmd, args, timeout=30.0):
        return "unexpected-tcp-call"

    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/A", "type": "T"})

    # Cache hit must close the circuit
    assert mw.circuit.state == CircuitBreaker.CLOSED, f"Expected CLOSED, got {mw.circuit.get_status()}"
    assert "cached-value" in result or "[CACHED]" in result


# ── PY6.arch.4: Internal flags stripped before bridge ─────────────────────────

async def test_internal_flags_stripped_before_bridge(monkeypatch):
    """None of the 5 internal flags must appear in args sent to bridge."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    captured = {}

    async def fake_send(cmd, args, timeout=30.0):
        captured.update(args)
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {
        "_no_reflect": True,
        "_no_distill": True,
        "_explicit_path": True,
        "_no_validate": True,
        "_no_strip": True,
    })

    for flag in ("_no_reflect", "_no_distill", "_explicit_path", "_no_validate", "_no_strip"):
        assert flag not in captured, f"Flag {flag} leaked to bridge"


# ── PY6.arch.4: Warnings prepended (not appended) to result ───────────────────

async def test_warning_prepend_not_append(monkeypatch):
    """taint_warn + blast_warn appear BEFORE the original result text."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw.known_paths.add("/A")

    async def fake_send(cmd, args, timeout=30.0):
        return "BRIDGE_RESULT"

    wrapped = wrap_send(fake_send, mw)
    # trigger taint: no prior read of /Enemy, value looks like a path reference
    result = await wrapped("set_property", {
        "path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"
    })

    bridge_idx = result.find("BRIDGE_RESULT")
    assert bridge_idx > 0, f"BRIDGE_RESULT not in result: {result!r}"
    # Any warning present must appear before the bridge result
    taint_idx = result.find("TAINT")
    if taint_idx != -1:
        assert taint_idx < bridge_idx, "TAINT warning must be BEFORE bridge result"


# ── PY6.test.2: Alive-check→ping integration through wrap_send ────────────────

async def test_wrap_send_pings_when_stale(monkeypatch):
    """When _last_success=0, wrap_send calls ping before the actual command."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw._last_success = 0.0  # stale

    call_log = []

    async def fake_send(cmd, args, timeout=30.0):
        call_log.append(cmd)
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})

    assert call_log[0] == "ping", f"Expected ping first, got {call_log}"
    assert "get_hierarchy" in call_log


# ── PY6.test.4: TAINT warning appears in wrapped() result ─────────────────────

async def test_wrap_send_taint_warning_in_result(monkeypatch):
    """TAINT warning from check_taint() appears in string returned by wrapped()."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw.known_paths.add("/A")

    async def fake_send(cmd, args, timeout=30.0):
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    # No prior read of /Enemy → TAINT
    result = await wrapped("set_property", {
        "path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"
    })

    assert "TAINT" in result, f"Expected TAINT in result, got: {result!r}"


async def test_wrap_send_overwrite_warning_in_result(monkeypatch):
    """OVERWRITE warning from check_dead_write() appears in string returned by wrapped()."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw.known_paths.add("/A")
    call_count = [0]

    async def fake_send(cmd, args, timeout=30.0):
        call_count[0] += 1
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    # First write primes dead-write tracking; second write with different value → OVERWRITE
    # (different value means different hash → RETRY not triggered, OVERWRITE is)
    await wrapped("set_property", {"path": "/A", "component": "C", "prop": "hp", "value": "100"})
    result = await wrapped("set_property", {"path": "/A", "component": "C", "prop": "hp", "value": "200"})

    assert "OVERWRITE" in result, f"Expected OVERWRITE in result, got: {result!r}"


# ── PY6.test.5: reroute + session reset through wrapped() ─────────────────────

async def test_wrap_send_reroute_clears_after_session_reset(monkeypatch):
    """Play mode routes set_property→set_runtime_property; reset_session restores edit mode."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw.known_paths.add("/P")
    bridge_calls = []

    async def fake_send(cmd, args, timeout=30.0):
        bridge_calls.append(cmd)
        return "ok"

    wrapped = wrap_send(fake_send, mw)

    # Play mode — should reroute to set_runtime_property
    mw.is_playing = True
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})
    assert bridge_calls[-1] == "set_runtime_property", f"Expected set_runtime_property, got {bridge_calls}"

    # Reset session → edit mode restored
    mw.reset_session()
    await wrapped("set_property", {"path": "/P", "prop": "x", "value": "1"})
    assert bridge_calls[-1] == "set_property", f"Expected set_property after reset, got {bridge_calls}"


# ── X5.cross.4: reconnect → reset_session → wrap_send resumes ─────────────────

async def test_wrap_send_resumes_after_reconnect(monkeypatch):
    """After retry-block + reset_session(), next identical call reaches send_fn."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")

    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw.known_paths.add("/X")
    bridge_calls = []

    async def fake_send(cmd, args, timeout=30.0):
        bridge_calls.append(cmd)
        return "ok"

    wrapped = wrap_send(fake_send, mw)

    # First call: primes retry cache
    await wrapped("set_property", {"path": "/X", "prop": "v", "value": "1"})
    # Second identical call: retry-blocked (no bridge call)
    result2 = await wrapped("set_property", {"path": "/X", "prop": "v", "value": "1"})
    assert "RETRY" in result2, f"Expected RETRY block, got: {result2!r}"

    bridge_before = len(bridge_calls)

    # Simulate reconnect callback
    mw.reset_session()

    # Third identical call must reach bridge
    await wrapped("set_property", {"path": "/X", "prop": "v", "value": "1"})
    assert len(bridge_calls) > bridge_before, "Expected bridge call after reset_session"
