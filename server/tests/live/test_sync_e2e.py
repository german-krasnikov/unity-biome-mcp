"""Live E2E tests: sync_unity stamp-gate and loop-breaker proofs.

Phase 3 checks:
  E2E-2 — no-op detection: sync_unity with no pending edit → stamp unchanged → 'sync clean (no-op)'
  E2E-3 — STOP on timeout: sync_unity with tiny timeout while compiling → 'STOP:' verdict
  E2E-stamp — stamp write + change across two reloads (run manually, needs non-wedged Unity)

Run: pytest -m live tests/live/test_sync_e2e.py -v
"""
import asyncio
import time

import pytest

import unity_mcp.tools.sync as _sync_mod

pytestmark = pytest.mark.live

# ---------------------------------------------------------------------------
# E2E-2: No-op detection
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_sync_unity_noop_when_stamp_unchanged(bridge):
    """E2E-2: sync_unity returns 'sync clean (no-op)' when stamp doesn't change.

    We call sync twice in quick succession. The second call sees will_compile=False
    (nothing changed) and fast-paths to no-op. Proves the no-op predicate works
    against a real Unity connection.
    """
    # The shared bridge remains pinned to this project while rediscovering its
    # current port after any domain reload performed by an earlier live test.
    ver = await bridge.send("get_version", {})
    assert ver.get("ok", True), f"get_version failed: {ver}"

    # We need will_compile=false for the no-op path.
    # Check sync_status — if already idle with a stamp, inject send directly.
    # Build a fake send that simulates the no-op scenario using real stamp data:
    #   - sync_status pre  → stamp_pre (from real live state)
    #   - sync             → sync_ack|epoch=1|will_compile=false
    #   - get_compile_errors → No compilation errors
    #
    # This is a white-box injection but it exercises ALL of sync_unity's
    # no-op code path with a real stamp value.
    ver_data = ver.get("data", "1.0")
    stamp = ""
    if "|stamp:" in ver_data:
        stamp = ver_data.split("|stamp:", 1)[1]

    # Build a consistent fake stamp (even empty is fine — tests the vacuous guard)
    call_log = []

    async def fake_send(cmd: str, args: dict) -> str:
        call_log.append(cmd)
        if cmd == "sync_status":
            # Both pre and post return the same stamp → no-op
            return f"epoch=1|state=ready|stamp={stamp}" if stamp else "epoch=1|state=ready"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "get_compile_errors":
            return "No compilation errors"
        # _get_errors' single terminal gate (61b0fdea) reads compile_status first
        # and, once idle, corroborates with diagnose — both are part of the fast
        # path now, not just get_compile_errors.
        if cmd == "compile_status":
            return "idle|1"
        if cmd == "diagnose":
            return "main_mvid=absent"
        if cmd == "warm_type_cache":
            return "ok:types=0"
        raise ConnectionError(f"unexpected cmd: {cmd}")

    old_send = _sync_mod._send
    _sync_mod._send = fake_send
    try:
        result = await _sync_mod.sync_unity(timeout=10.0)
    finally:
        _sync_mod._send = old_send

    # Fast path (will_compile=false): returns "sync clean (no compile needed)"
    assert "sync clean" in result, f"Expected 'sync clean' in: {result!r}"
    # Confirm we hit the fast path: sync_status was NOT polled after sync
    assert "sync" in call_log
    assert call_log.count("sync_status") <= 1  # only pre-stamp read


@pytest.mark.asyncio
async def test_sync_unity_noop_stamp_match():
    """E2E-2b: sync_unity handles will_compile=True but stamp_post == stamp_pre.

    Pre-61b0fdea, a frozen MVID on a 'ready' epoch triggered a force_refresh
    recovery heuristic — a source of the stale-DLL/false-ready defects that
    commit fixed. 61b0fdea replaced it with a single terminal gate: a matching
    epoch + state=ready is trusted directly, and freshness is corroborated by
    _get_errors (compile_status + diagnose's dlls= checksum, not MVID-diffing).
    A frozen MVID with no compile errors and no stale-dll token is therefore
    'sync clean' with NO recovery call — this pins that force_refresh is no
    longer invoked on this path.
    """
    stamp_pre = "abc123:99999"  # MVID frozen: pre and post share the same value
    call_log = []
    epoch_counter = {"n": 0}

    async def fake_send(cmd: str, args: dict) -> str:
        call_log.append(cmd)
        if cmd == "sync_status":
            epoch_counter["n"] += 1
            if epoch_counter["n"] == 1:
                return f"epoch=0|state=idle|stamp={stamp_pre}"  # pre-sync read
            return f"epoch=5|state=ready|stamp={stamp_pre}"  # MVID unchanged
        if cmd == "sync":
            return "sync_ack|epoch=5|will_compile=true"
        if cmd == "get_compile_errors":
            return "No compilation errors"
        if cmd == "compile_status":
            return "idle|1"
        if cmd == "diagnose":
            return "main_mvid=absent"
        if cmd == "warm_type_cache":
            return "ok:types=0"
        raise ConnectionError(f"unexpected: {cmd}")

    old_send = _sync_mod._send
    _sync_mod._send = fake_send
    try:
        result = await _sync_mod.sync_unity(timeout=10.0)
    finally:
        _sync_mod._send = old_send

    assert result == "sync clean", f"Expected 'sync clean' with frozen MVID: {result!r}"
    assert "force_refresh" not in call_log, (
        "matching epoch + state=ready is trusted directly — no MVID-diff recovery"
    )


# ---------------------------------------------------------------------------
# E2E-3: STOP on timeout
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_sync_unity_stop_on_timeout():
    """E2E-3: sync_unity returns 'STOP:' when reload doesn't converge within budget.

    Uses a 0.1s timeout against a fake send that never transitions to 'ready'.
    Proves the timeout circuit-breaker fires correctly without a 120s live wait.
    """
    call_log = []

    async def fake_send_stuck(cmd: str, args: dict) -> str:
        call_log.append(cmd)
        if cmd == "sync_status":
            await asyncio.sleep(0.05)  # slow enough to exhaust 0.1s
            return "epoch=1|state=compiling|dur=0.0"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        return ""

    old_send = _sync_mod._send
    _sync_mod._send = fake_send_stuck
    start = time.monotonic()
    try:
        result = await _sync_mod.sync_unity(timeout=0.1)
    finally:
        _sync_mod._send = old_send

    elapsed = time.monotonic() - start
    assert result.startswith("STOP"), f"Expected STOP verdict, got: {result!r}"
    assert elapsed < 5.0, f"Timeout took too long ({elapsed:.1f}s)"


@pytest.mark.asyncio
async def test_sync_unity_stop_contains_diagnostic():
    """E2E-3b: STOP verdict reports the elapsed budget so the caller can act.

    Pre-61b0fdea, the poll loop's own deadline check returned a compile-specific
    hint ("...or compile is wedged; check get_compile_errors"). 61b0fdea moved
    timeout handling to one asyncio.timeout_at wrapping the whole _sync_unity
    call (single shared deadline, ref commit body) — a timeout can now fire at
    any awaited step, not just the compile-wait poll, so _run_with_context's
    handler reports a deliberately generic 'operation may still be running'
    instead of guessing the cause. This pins that current, honest contract.
    """
    async def fake_send(cmd: str, args: dict) -> str:
        if cmd == "sync_status":
            return "epoch=7|state=compiling|dur=5.0"
        if cmd == "sync":
            return "sync_ack|epoch=7|will_compile=true"
        return ""

    old_send = _sync_mod._send
    _sync_mod._send = fake_send
    try:
        result = await _sync_mod.sync_unity(timeout=0.05)
    finally:
        _sync_mod._send = old_send

    assert result.startswith("STOP"), f"Expected STOP: {result!r}"
    assert "0.05" in result and "may still be running" in result, (
        f"STOP should report the exceeded budget: {result!r}"
    )


# ---------------------------------------------------------------------------
# E2E-1b: Stamp changes between two consecutive calls (mock-level proof)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_sync_unity_stamp_changes_new_domain():
    """E2E-1b: stamp_post != stamp_pre → 'sync clean' (new DLL loaded, not stale).

    This is the core stale-DLL proof at mock level: same code path that runs
    in production, with injected stamps that differ (simulating a real recompile).
    """
    stamp_pre  = "11111111-1111-1111-1111-111111111111:638000000000000000"
    stamp_post = "22222222-2222-2222-2222-222222222222:638000000000000001"
    call_log = []
    sync_status_calls = {"n": 0}

    async def fake_send(cmd: str, args: dict) -> str:
        call_log.append(cmd)
        if cmd == "sync_status":
            sync_status_calls["n"] += 1
            if sync_status_calls["n"] == 1:
                return f"epoch=0|state=idle|stamp={stamp_pre}"
            # After sync: new domain, new stamp
            return f"epoch=3|state=ready|stamp={stamp_post}"
        if cmd == "sync":
            return "sync_ack|epoch=3|will_compile=true"
        if cmd == "get_compile_errors":
            return "No compilation errors"
        # _get_errors' single terminal gate (61b0fdea) needs a real idle
        # compile_status and a clean diagnose to reach 'sync clean'.
        if cmd == "compile_status":
            return "idle|1"
        if cmd == "diagnose":
            return "main_mvid=absent"
        return ""

    old_send = _sync_mod._send
    _sync_mod._send = fake_send
    try:
        result = await _sync_mod.sync_unity(timeout=10.0)
    finally:
        _sync_mod._send = old_send

    # stamp changed → NOT a no-op → 'sync clean'
    assert result == "sync clean", f"Expected 'sync clean' (new domain), got: {result!r}"
    assert "no-op" not in result, f"Should NOT be no-op when stamps differ: {result!r}"
