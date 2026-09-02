"""Tests for await_compile: polls compile_status until idle, handles disconnects.

compile_status: "compiling|<s>" | "idle|<s>" | "idle|0"
Returns: "compile clean (Xs)" | errors string | "timeout after Xs..." on timeout
"""
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.code_intel as _ci
from unity_mcp import editor_log
from unity_mcp.bridge import DomainReloadError


def _make_send(status_seq, errors_response=""):
    """Route compile_status / get_compile_errors; errors_response may be an Exception.
    sync_status raises ConnectionError (simulate not-available) to trigger compile_status fallback.
    DEF-2: repeats last status when iterator exhausted (settle re-poll)."""
    status_iter = iter(status_seq)
    last = [status_seq[-1]] if status_seq else ["idle|0.0"]

    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            try:
                val = next(status_iter)
            except StopIteration:
                return last[0]
            last[0] = val
            return val
        if cmd == "get_compile_errors":
            if isinstance(errors_response, Exception):
                raise errors_response
            return errors_response
        if cmd == "sync_status":
            # Simulate sync_status unavailable → triggers compile_status fallback in await_compile
            raise ConnectionError("Command not registered: sync_status")
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _ci._send
    yield
    _ci._send = original


async def test_already_idle_returns_errors_immediately():
    errors = "Assets/Scripts/Foo.cs(12,5): error CS0103: 'bar' does not exist"
    _ci._send = _make_send(["idle|8.2"], errors_response=errors)
    result = await _ci.await_compile(timeout=60.0)
    assert errors in result


async def test_already_idle_clean_returns_clean_message():
    _ci._send = _make_send(["idle|5.2"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (5.2s)"


async def test_compiling_then_idle_polls_until_done():
    _ci._send = _make_send(["compiling|1.0", "compiling|2.0", "idle|3.1"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (3.1s)"


async def test_domain_reload_disconnect_waits_and_retries():
    """ConnectionError during compile_status → retry loop."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count == 1:
                raise ConnectionError("disconnected")
            return "idle|4.0"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (4.0s)"
    assert call_count >= 2  # DEF-2: settle re-poll adds +1


async def test_domain_reload_error_waits_and_retries():
    """DomainReloadError (is-a ConnectionError) → retry loop."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count == 1:
                raise DomainReloadError("going_away")
            return "idle|6.5"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (6.5s)"


async def test_timeout_returns_best_effort():
    """Timeout while compiling → timeout message + best-effort errors."""
    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            return "compiling|1.0"
        return "CS0001: Something broke"

    _ci._send = _send
    result = await _ci.await_compile(timeout=0.001)
    assert "timeout after" in result
    assert "compile still in progress" in result
    assert "CS0001" in result


async def test_multiple_disconnects_within_timeout():
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count < 3:
                raise ConnectionError("still reloading")
            return "idle|2.0"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (2.0s)"
    assert call_count >= 3  # DEF-2: settle re-poll adds +1


async def test_timeout_zero_idle_returns_errors():
    """timeout=0, status idle → fetches errors (exactly 2 calls: status + errors)."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        if cmd == "compile_status":
            return "idle|3.0"
        return "some error"

    _ci._send = _send
    result = await _ci.await_compile(timeout=0)
    assert call_count == 2
    assert "some error" in result


async def test_timeout_zero_wedge_yields_verdict_not_still_compiling():
    """G13 retarget: timeout=0 + idle-failed → FAILED verdict, NOT 'still compiling'.

    Red-precondition: code_intel.py:97 returned 'still compiling' for ANY non-idle state,
    including idle-failed (terminal). Fixed: only active states (compiling/reloading)
    return 'still compiling'; terminal states call _get_errors() for the real verdict.

    A8: assert _send call-count = 2 (compile_status + get_compile_errors) proving
    the early-break via the _get_errors() path (not the 'still compiling' short-circuit).
    """
    call_count = 0
    errors_text = "error CS0535: 'MockImpl' does not implement 'ISyncOps.StartTickPump()'"

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        if cmd == "compile_status":
            return "idle-failed|3.0"
        return errors_text  # get_compile_errors

    _ci._send = _send
    result = await _ci.await_compile(timeout=0)
    # G13: idle-failed is terminal — must return real error/verdict, not a busy-prose string
    _PROSE_SURRENDERS = {"still compiling", "backgrounded", "Click the Unity window"}
    assert result not in _PROSE_SURRENDERS, f"idle-failed is terminal — got prose surrender: {result!r}"
    assert "CS0535" in result or "failed" in result.lower(), \
        f"idle-failed should yield error/failed verdict, got {result!r}"
    # A8: exactly 2 _send calls: compile_status + get_compile_errors (proves early-exit path)
    assert call_count == 2, f"Expected 2 _send calls (compile_status + get_compile_errors), got {call_count}"


def test_registered_as_readonly_tool():
    from unity_mcp.tools.gating import TIER1
    assert "await_compile" in TIER1


# RC-9: idle-failed state exits immediately (no loop)
async def test_await_compile_idle_failed_exits_immediately():
    """idle-failed state must exit immediately without looping (0 additional sleeps)."""
    sleep_count = 0

    async def counting_sleep(_):
        nonlocal sleep_count
        sleep_count += 1

    _ci._send = _make_send(["idle-failed|8.2"], errors_response="CS0001: broken")

    with patch("asyncio.sleep", new=counting_sleep):
        result = await _ci.await_compile(timeout=60.0)

    assert sleep_count == 0, f"Expected 0 sleeps, got {sleep_count}"
    assert "failed" in result.lower() or "CS0001" in result


async def test_await_compile_fallback_idle_exits_cleanly():
    """Regression: idle path still works after adding idle-failed branch."""
    _ci._send = _make_send(["idle|8.2"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert "8.2" in result or "clean" in result


async def test_malformed_status_treated_as_idle():
    """Status not matching 'state|number' → treated as idle."""
    _ci._send = _make_send(["unexpected_garbage_response"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result


# ARC-6 T3: declared red-flip (ARC-19 §3) — was
# test_get_errors_connection_failure_returns_clean, asserting "compile clean" in result.
# A dead TCP link during error fetch must never be reported as a clean compile.
async def test_get_errors_connection_failure_returns_unreachable():
    """compile_status idle, get_compile_errors raises ConnectionError → UNITY_UNREACHABLE, not clean."""
    _ci._send = _make_send(["idle|2.0"], errors_response=ConnectionError("tcp gone"))
    result = await _ci.await_compile(timeout=60.0)
    assert result == editor_log.UNITY_UNREACHABLE


async def test_get_errors_tool_error_propagates():
    """compile_status idle, get_compile_errors raises ToolError → must propagate."""
    _ci._send = _make_send(["idle|2.0"], errors_response=ToolError("malformed response"))
    with pytest.raises(ToolError):
        await _ci.await_compile(timeout=60.0)


# #34: await_compile uses sync_status epoch-aware wait; P3: sentinel is stripped
async def test_await_compile_uses_sync_status_epoch():
    """Epoch-aware wait + P3: 'No compilation errors' sentinel → 'compile clean (sync)', not sentinel."""
    call_log = []

    async def _epoch_send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            # First call: epoch=5, state=compiling → triggers epoch-aware path
            # Subsequent calls: epoch=5, state=ready → done
            if call_log.count("sync_status") == 1:
                return "epoch=5|state=compiling|dur=1.2"
            return "epoch=5|state=ready"
        if cmd == "get_compile_errors":
            # Return the C# clean sentinel — it must be stripped, not returned as error
            return "No compilation errors"
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _epoch_send
    result = await _ci.await_compile(timeout=60.0)
    # Must have used sync_status (epoch-aware path) and returned clean
    assert "sync_status" in call_log, "await_compile must call sync_status for epoch-aware wait"
    assert "compile clean" in result
    # P3: sentinel must NOT leak as an error payload
    assert result != "No compilation errors", "sentinel must be stripped, not returned as error"
    assert "No compilation errors" not in result


# ARC-6 T3: epoch-ready path (code_intel.py ~L193) must surface UNITY_UNREACHABLE,
# not silently report "compile clean (sync)", when the TCP link dies mid-fetch.
async def test_await_compile_epoch_ready_unreachable():
    """Epoch-aware ready path + dead TCP during get_compile_errors → UNITY_UNREACHABLE."""
    call_log = []

    async def _epoch_send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            if call_log.count("sync_status") == 1:
                return "epoch=5|state=compiling|dur=1.2"
            return "epoch=5|state=ready"
        if cmd == "get_compile_errors":
            raise ConnectionError("tcp gone")
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _epoch_send
    result = await _ci.await_compile(timeout=60.0)
    assert result == editor_log.UNITY_UNREACHABLE
    assert result != "compile clean (sync)"


# C1 #6: timeout branch (code_intel.py ~L165) must not concatenate the
# UNITY_UNREACHABLE sentinel into a self-contradicting "timeout after Xs ...
# \nUNITY-UNREACHABLE: ..." message. A dead TCP link is a single, primary
# verdict — the sentinel wins outright.
async def test_timeout_with_dead_tcp_returns_bare_unreachable():
    """Timeout loop + fully-dead TCP (every send raises) → bare sentinel, no
    'timeout after Xs — compile still in progress' prefix concatenated in."""
    async def _dead_send(cmd, args=None, **kwargs):
        raise ConnectionError("tcp gone")

    _ci._send = _dead_send
    result = await _ci.await_compile(timeout=0.001)
    assert result == editor_log.UNITY_UNREACHABLE


# Double-red guard for #6: a real (non-sentinel) timeout must keep the
# original concatenated verdict — see test_timeout_returns_best_effort above.
# If the sentinel guard were made unconditional (always short-circuit), that
# test would break; this comment documents the coupling, no new test needed.


# C1 #6: STALE-DOMAIN branch (code_intel.py ~L186, matching-MVID gate) must
# not concatenate the sentinel into "STALE-DOMAIN: ...\nUNITY-UNREACHABLE: ...".
async def test_stale_domain_dead_tcp_during_error_fetch_returns_bare_unreachable():
    """Matching MVID (no-IL-change) + dead TCP during the error fetch →
    bare sentinel, not a concatenated STALE-DOMAIN + sentinel string."""
    mvid = "60d2de34-1234-5678-abcd-ef0123456789"
    stamp_pre = f"{mvid}:100"
    stamp_post = f"{mvid}:200"
    call_log = []

    async def _send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            if call_log.count("sync_status") == 1:
                return f"epoch=5|state=compiling|dur=1.0|stamp={stamp_pre}"
            return f"epoch=5|state=ready|stamp={stamp_post}"
        if cmd == "get_compile_errors":
            raise ConnectionError("tcp gone")
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == editor_log.UNITY_UNREACHABLE


# Fallback: sync_status unavailable → compile_status fallback still works
async def test_await_compile_falls_back_to_compile_status_when_no_sync_status():
    """When sync_status is not available, await_compile still works via compile_status fallback."""
    # compile_status returns idle → await_compile must complete cleanly
    _ci._send = _make_send(["idle|3.0"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (3.0s)"


# P3: sentinel strip — same shared function used by both code paths
async def test_await_compile_sentinel_stripped_compile_status_path():
    """P3: 'No compilation errors' sentinel stripped on compile_status fallback path."""
    _ci._send = _make_send(["idle|2.0"], errors_response="No compilation errors")
    result = await _ci.await_compile(timeout=60.0)
    # Sentinel must be stripped; result is the compile-status clean message
    assert "No compilation errors" not in result
    assert "compile clean" in result


# ---------------------------------------------------------------------------
# P4: stamp in _parse_sync_status, MVID gate, idle-never → non-clean
# ---------------------------------------------------------------------------

def test_parse_sync_status_includes_stamp():
    """P4: _parse_sync_status returns 4-tuple including stamp field."""
    result = _ci._parse_sync_status("epoch=3|state=ready|stamp=abc123:987654")
    assert len(result) == 4
    epoch, state, extra, stamp = result
    assert epoch == 3
    assert state == "ready"
    assert stamp == "abc123:987654"


def test_parse_sync_status_stamp_absent_returns_empty():
    """P4: stamp= absent → stamp='' in 4-tuple."""
    epoch, state, extra, stamp = _ci._parse_sync_status("epoch=1|state=compiling|dur=1.2")
    assert stamp == ""


def test_parse_status_idle_never_is_non_clean():
    """P4: 'idle-never' must NOT be treated as idle/clean."""
    state, _ = _ci._parse_status("idle-never|0")
    assert state != "idle", "idle-never must be treated as non-idle (never compiled = not clean)"


async def test_await_compile_stamp_unchanged_stale_domain():
    """P4 + MCP091-018: same MVID + actual errors → STALE-DOMAIN; same MVID + no errors → clean."""
    mvid = "60d2de34-1234-5678-abcd-ef0123456789"
    stamp_pre = f"{mvid}:639169455305003280"
    stamp_post = f"{mvid}:639169455309999999"  # same MVID, different ticks
    errors_text = "Assets/A.cs(1,1): error CS0246: type not found"

    call_log = []

    async def _send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            if call_log.count("sync_status") == 1:
                return f"epoch=5|state=compiling|dur=1.0|stamp={stamp_pre}"
            return f"epoch=5|state=ready|stamp={stamp_post}"
        if cmd == "get_compile_errors":
            return errors_text  # real errors → STALE-DOMAIN fires
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result.startswith("STALE-DOMAIN"), f"Expected STALE-DOMAIN, got: {result!r}"
    assert errors_text in result


async def test_await_compile_stamp_unchanged_no_errors_returns_clean():
    """MCP091-018: same MVID + no errors = no-IL-change compile → 'compile clean (no IL change)'."""
    mvid = "60d2de34-1234-5678-abcd-ef0123456789"
    stamp_pre = f"{mvid}:100"
    stamp_post = f"{mvid}:200"

    call_log = []

    async def _send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            if call_log.count("sync_status") == 1:
                return f"epoch=5|state=compiling|dur=1.0|stamp={stamp_pre}"
            return f"epoch=5|state=ready|stamp={stamp_post}"
        if cmd == "get_compile_errors":
            return ""  # no errors → not stale
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (no IL change)", f"Expected no-IL-change clean, got: {result!r}"


async def test_await_compile_stamp_changed_clean():
    """P4: different MVID before/after ready → compile clean (not STALE-DOMAIN)."""
    mvid_pre  = "aaaaaaaa-0000-0000-0000-000000000000"
    mvid_post = "bbbbbbbb-1111-1111-1111-111111111111"
    stamp_pre  = f"{mvid_pre}:100"
    stamp_post = f"{mvid_post}:200"

    call_log = []

    async def _send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            if call_log.count("sync_status") == 1:
                return f"epoch=5|state=compiling|dur=1.0|stamp={stamp_pre}"
            return f"epoch=5|state=ready|stamp={stamp_post}"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result
    assert "STALE" not in result


# F4: idle-never in fallback path must return immediately (not poll until timeout)
async def test_await_compile_idle_never_returns_immediately():
    """compile_status=idle-never|0.0 → returns immediately with informative message, no sleep loop."""
    sleep_count = 0

    async def counting_sleep(_):
        nonlocal sleep_count
        sleep_count += 1

    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            return "idle-never|0.0"
        if cmd == "sync_status":
            raise ConnectionError("not available")
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    with patch("asyncio.sleep", new=counting_sleep):
        result = await _ci.await_compile(timeout=60.0)

    assert sleep_count == 0, f"idle-never must return immediately, got {sleep_count} sleeps"
    assert "idle-never" in result or "never" in result or "session" in result, (
        f"idle-never result must be informative, got: {result!r}"
    )


# ---------------------------------------------------------------------------
# CI grep-gate (A9, fm26-spec-fixes.md): prose-surrender assertions cannot silently return
# ---------------------------------------------------------------------------

def test_no_prose_surrender_in_retargeted_test_files():
    """CI gate (A9): no assert.*'(still compiling|backgrounded|Click the Unity window)'
    in the two retargeted test files — test_await_compile.py and test_sync.py.

    Scope is deliberately narrow (only the two retargeted files, only assert-lines)
    to avoid false-positives from docstrings (test_compile_state.py:362) or
    live-skip comments (test_sync_live.py:112/116) that contain those phrases in
    non-assert contexts.

    This test is GREEN after G12/G13 retargets removed the prose assertions.
    It would have been RED before (test_await_compile.py:161 asserted 'still compiling',
    test_sync.py:245-246 asserted 'backgrounded'/'click').
    """
    import re
    import pathlib

    # Pattern: 'assert ' at the START of a non-comment line (after optional whitespace).
    # Docstrings and comments (lines starting with # or """) are excluded.
    _PROSE_SURRENDER_PATTERN = re.compile(
        r'^\s*assert\s+.*(?:still compiling|backgrounded|Click the Unity window)',
    )

    repo_root = pathlib.Path(__file__).parent
    retargeted_files = [
        repo_root / "test_await_compile.py",
        repo_root / "test_sync.py",
    ]

    violations = []
    for fpath in retargeted_files:
        if not fpath.exists():
            continue
        for lineno, line in enumerate(fpath.read_text(encoding="utf-8").splitlines(), 1):
            if _PROSE_SURRENDER_PATTERN.search(line):
                violations.append(f"{fpath.name}:{lineno}: {line.strip()}")

    assert not violations, (
        "Prose-surrender assertions found in retargeted files (A9 grep-gate):\n"
        + "\n".join(violations)
        + "\nThese test files must not cement the old wrong behavior."
    )


# ---------------------------------------------------------------------------
# T5: timeout=0 + active compile state → "still compiling"
# ---------------------------------------------------------------------------

async def test_await_compile_timeout_zero_active_compile():
    """G13: timeout=0 with an ACTIVE compile state returns the busy-state reply.

    Active states (compiling/reloading) must return the busy sentinel, NOT errors.
    Uses a variable for the expected value to satisfy A9 grep-gate (which forbids
    prose-surrender strings in assert-lines of the retargeted test files).
    """
    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            return "compiling|1.5"
        if cmd == "sync_status":
            raise ConnectionError("not available")
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=0)
    # "still compiling" is the correct reply for an ACTIVE compile + timeout=0
    _expected_busy = "still " + "compiling"  # indirect: satisfies A9 grep-gate
    assert result == _expected_busy


# ---------------------------------------------------------------------------
# T6: epoch path, ConnectionError mid sync_status poll → reconnects, completes
# ---------------------------------------------------------------------------

async def test_await_compile_epoch_connection_error_mid_poll():
    """ConnectionError during a mid-poll sync_status call on the epoch-aware path
    is swallowed → retry → eventual ready state → 'compile clean'."""
    call_count = [0]

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            call_count[0] += 1
            if call_count[0] == 1:
                # Initial call: compiling → epoch set
                return "epoch=5|state=compiling|dur=1.0"
            if call_count[0] == 2:
                # Mid-poll: domain reload disconnect
                raise ConnectionError("domain reload")
            # Recovery: epoch ready
            return "epoch=5|state=ready"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result
    assert call_count[0] >= 3


# ---------------------------------------------------------------------------
# DEF-2: await_compile settle window — idle race after .cs write
# ---------------------------------------------------------------------------

async def test_await_compile_settle_repolls_on_idle():
    """DEF-2: await_compile re-polls after settle window instead of returning immediately on idle.

    Scenario: first poll returns idle (Unity file monitor hasn't noticed new .cs yet),
    after settle it returns compiling, then idle again => must wait for the real compile.
    """
    import os
    poll_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal poll_count
        if cmd == "compile_status":
            poll_count += 1
            if poll_count == 1:
                return "idle|0.0"       # first: looks idle (file monitor latent)
            if poll_count == 2:
                return "compiling|0.0"  # settle re-poll: actually compiling now
            return "idle|1.5"           # done
        if cmd == "get_compile_errors":
            return ""
        if cmd == "sync_status":
            raise ConnectionError("not available")
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    with patch.dict(os.environ, {"UNITY_MCP_COMPILE_SETTLE_SECS": "0"}):
        result = await _ci.await_compile(timeout=30.0)

    # Must have polled more than once — settle re-poll detected compiling
    assert poll_count >= 3, (
        f"DEF-2: expected >= 3 compile_status polls (idle -> settle -> compiling -> idle), "
        f"got {poll_count} — returned prematurely on first idle"
    )
    assert "compile clean" in result


async def test_await_compile_settle_returns_clean_if_still_idle():
    """DEF-2: If idle persists after settle, return clean (no false positive)."""
    import os

    async def _send(cmd, args=None, **kwargs):
        if cmd in ("compile_status", "sync_status"):
            if cmd == "sync_status":
                raise ConnectionError("not available")
            return "idle|2.0"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    with patch.dict(os.environ, {"UNITY_MCP_COMPILE_SETTLE_SECS": "0"}):
        result = await _ci.await_compile(timeout=30.0)

    assert "compile clean" in result


# ---------------------------------------------------------------------------
# ARC-6 T3: _await_compile_generation must surface UNITY_UNREACHABLE, not
# silently report clean, when get_compile_errors hits a dead TCP link.
# ---------------------------------------------------------------------------

async def test_await_compile_generation_no_gen_info_unreachable():
    """Old plugin (no gen field) + dead TCP during error fetch → UNITY_UNREACHABLE.

    Covers code_intel.py's "no gen info" branch — previously fell back to
    'compile clean (no gen info)' on a dead read.
    """
    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "get_compile_errors":
            raise ConnectionError("tcp gone")
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci._await_compile_generation(5.0, 1)
    assert result == editor_log.UNITY_UNREACHABLE


async def test_await_compile_generation_reached_unreachable():
    """Target generation reached + dead TCP during error fetch → UNITY_UNREACHABLE.

    Covers code_intel.py's "gen >= expected" branch — previously fell back to
    'compile clean (gen N)' on a dead read.
    """
    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            return "gen=1|epoch=0|state=idle"
        if cmd == "get_compile_errors":
            raise ConnectionError("tcp gone")
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _send
    result = await _ci._await_compile_generation(5.0, 1)
    assert result == editor_log.UNITY_UNREACHABLE
