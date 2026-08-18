"""Tests for sync_unity tool: poll loop, epoch, reconnect, fail path.

Protocol:
  sync → "sync_ack|epoch=N|will_compile=bool"
  sync_status → "epoch=N|state=ready|compiling|failed|idle"
  get_compile_errors → "" or error text
  editor_log.corroborate() — both-signals gate (MAJOR-3)
"""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock

from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.sync as _sync
from unity_mcp.bridge import DomainReloadError


# ── Helpers ──────────────────────────────────────────────────────────────────

def _make_send(ack_response: str, status_seq, errors_response: str = "",
               recovery_stamp: str = ""):
    """Route sync / sync_status / get_compile_errors / force_refresh commands.

    The first sync_status call is the stamp pre-read (before sync); it returns a neutral
    idle response with no stamp so existing tests are unaffected by the new stamp logic.
    recovery_stamp: if set, sync_status calls AFTER force_refresh return this stamp value.
    diagnose: returns main_mvid=absent so run_ladder escalation short-circuits (REIMPORT-NEEDED).
    """
    status_iter = iter(status_seq)
    synced = False
    refreshed = False

    async def _send(cmd, args=None, **kwargs):
        nonlocal synced, refreshed
        if cmd == "sync":
            synced = True
            return ack_response
        if cmd == "force_refresh":
            refreshed = True
            return "force_refresh triggered"
        if cmd == "sync_status":
            if not synced:
                # pre-stamp read: return neutral, no stamp field
                return "epoch=0|state=idle"
            if refreshed and recovery_stamp:
                return f"epoch=0|state=ready|stamp={recovery_stamp}"
            try:
                val = next(status_iter)
            except StopIteration:
                # recovery polling after sequence exhausted: return no stamp (frozen)
                return "epoch=0|state=ready"
            if isinstance(val, Exception):
                raise val
            return val
        if cmd == "get_compile_errors":
            return errors_response
        if cmd == "diagnose":
            return "main_mvid=absent"
        if cmd == "warm_type_cache":
            return "ok:types=42"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _zero_recovery_timeout(monkeypatch):
    """Default: recovery exits instantly (timeout=0) so existing REIMPORT-NEEDED asserts hold.

    Tests that need real recovery override via monkeypatch(_sync, '_RECOVERY_TIMEOUT', N).
    """
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)


@pytest.fixture(autouse=True)
def _reset_send():
    original = _sync._send
    yield
    _sync._send = original


@pytest.fixture(autouse=True)
def _patch_corroborate():
    """Default: corroborate / get_corroborated_errors are pass-throughs (no Unity in tests).

    P3: _get_errors now calls editor_log.get_corroborated_errors(send) — must be async mock.
    """
    async def _default_get_corroborated(send):
        try:
            csharp = await send("get_compile_errors", {})
        except Exception:
            return ""
        if csharp.strip() == "No compilation errors":
            return ""
        return csharp

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.corroborate = lambda s: s  # kept for back-compat with test_both_signals_required
        mock_el.init_corroboration = MagicMock()
        mock_el.get_corroborated_errors = _default_get_corroborated
        yield mock_el


# ── Tests #21–#30 ─────────────────────────────────────────────────────────────

# #21: will_compile=false → fast path, no poll
@pytest.mark.asyncio
async def test_idempotent_noop_fast_path():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=false",
        status_seq=[],  # never called
    )
    result = await _sync.sync_unity()
    assert "sync clean" in result or "no compile needed" in result


# #22: both signals required — corroborate returns errors even when C# reports clean
@pytest.mark.asyncio
async def test_both_signals_required_for_clean(_patch_corroborate):
    """state=ready+epoch match, but get_corroborated_errors adds stale warning → not clean."""
    stale_warning = "[warn: UnityMCP.Editor.dll may be stale - consider recompiling]"

    async def _stale_get_corroborated(send):
        # C# is empty but dll-stale → corroborator appends warning
        return stale_warning

    _patch_corroborate.get_corroborated_errors = _stale_get_corroborated

    _sync._send = _make_send(
        "sync_ack|epoch=2|will_compile=true",
        status_seq=["epoch=2|state=ready"],
        errors_response="",  # C# reports clean
    )
    result = await _sync.sync_unity(timeout=60.0)
    # The stale warning from editor_log must surface through the both-signals gate
    assert stale_warning in result


# #23: epoch race — sync_status returns wrong epoch → keep polling
@pytest.mark.asyncio
async def test_epoch_race_no_premature_idle():
    _sync._send = _make_send(
        "sync_ack|epoch=3|will_compile=true",
        status_seq=[
            "epoch=2|state=ready",   # stale epoch — ignore
            "epoch=3|state=ready",   # correct epoch — accept
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# #24: reconnect after domain reload — DomainReloadError then success
@pytest.mark.asyncio
async def test_reconnect_after_domain_reload():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            DomainReloadError("going_away"),
            "epoch=1|state=ready",
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# #25: compile failed → return errors immediately, no reconnect wait
@pytest.mark.asyncio
async def test_compile_failed_no_reload_wait():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=failed|err=CS0103: bad"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "CS0103" in result or "failed" in result


# #26: compile errors verbatim — state=ready+epoch match, get_compile_errors returns errors
@pytest.mark.asyncio
async def test_compile_errors_verbatim():
    err = "Assets/Bar.cs(5,3): error CS0246: type not found"
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
        errors_response=err,
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert err in result


# #27: stale dll surfaced via get_corroborated_errors
@pytest.mark.asyncio
async def test_stale_dll_blocks_false_clean(_patch_corroborate):
    """get_corroborated_errors() returns stale-dll message — must surface in sync_unity result."""
    stale_msg = "[editor.log - dll stale]\nAssets/Foo.cs(1,1): error CS0001: stale"

    async def _stale(send):
        return stale_msg

    _patch_corroborate.get_corroborated_errors = _stale

    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
        errors_response="",  # C# says clean
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert stale_msg in result


# #28: timeout → return STOP verdict
@pytest.mark.asyncio
async def test_timeout_returns_partial():
    # With sleep mocked and timeout=0, deadline is already past before first poll
    call_count = 0

    async def _stuck(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling"
        return ""

    _sync._send = _stuck
    result = await _sync.sync_unity(timeout=0.0)
    assert result.startswith("STOP:")


# #29: Unity dead — ConnectionError on all calls → fast error
@pytest.mark.asyncio
async def test_unity_dead_fails_fast():
    async def _dead(cmd, args=None, **kwargs):
        raise ConnectionError("Unity not running")

    _sync._send = _dead
    with pytest.raises((ConnectionError, ToolError)):
        await _sync.sync_unity(timeout=60.0)


# #30: no bridge → ToolError
@pytest.mark.asyncio
async def test_standalone_server_degrades():
    _sync._send = None
    with pytest.raises(ToolError):
        await _sync.sync_unity(timeout=60.0)


# #31: backgrounded editor — compile never starts (dur stays 0.0) → focus hint,
# not a blind 120s timeout (macOS/Unity 6 defers compilation while unfocused)
@pytest.mark.asyncio
async def test_focus_hint_when_compile_never_starts(monkeypatch):
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)

    async def _backgrounded(cmd, args=None, **kwargs):
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"
        return ""

    _sync._send = _backgrounded
    result = await _sync.sync_unity(timeout=60.0)
    # G12 retarget: prose surrender → machine-readable REIMPORT-NEEDED or MANUAL-REQUIRED verdict.
    # Note: run_ladder escalation may produce REIMPORT-NEEDED or MANUAL-REQUIRED depending on tier.
    assert ("REIMPORT-NEEDED" in result or "MANUAL-REQUIRED" in result), \
        f"Expected REIMPORT-NEEDED or MANUAL-REQUIRED, got {result!r}"


# #32: real compile in progress (dur > 0) must NOT trigger the focus hint
@pytest.mark.asyncio
async def test_no_focus_hint_when_compile_running(monkeypatch):
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            "epoch=1|state=compiling|dur=2.3",
            "epoch=1|state=ready",
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result


# ── RC-1/2/8 stamp predicate + STOP verdict + bump circuit-breaker ────────────

def _make_send_with_stamp(pre_status: str, ack: str, status_seq, errors_response: str = "",
                          recovery_stamp: str = ""):
    """Route sync/sync_status/get_compile_errors/force_refresh with pre-sync stamp read.

    recovery_stamp: if set, sync_status calls after force_refresh return this stamp.
    diagnose: returns main_mvid=absent so run_ladder escalation short-circuits (REIMPORT-NEEDED).
    """
    status_iter = iter(status_seq)
    pre_called = False
    refreshed = False

    async def _send(cmd, args=None, **kwargs):
        nonlocal pre_called, refreshed
        if cmd == "sync_status":
            if not pre_called:
                pre_called = True
                return pre_status
            if refreshed and recovery_stamp:
                return f"epoch=1|state=ready|stamp={recovery_stamp}"
            try:
                val = next(status_iter)
            except StopIteration:
                # recovery polling: no stamp → frozen
                return "epoch=1|state=ready"
            if isinstance(val, Exception):
                raise val
            return val
        if cmd == "sync":
            return ack
        if cmd == "force_refresh":
            refreshed = True
            return "force_refresh triggered"
        if cmd == "get_compile_errors":
            return errors_response
        if cmd == "diagnose":
            return "main_mvid=absent"
        if cmd == "warm_type_cache":
            return "ok:types=42"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _reset_bump_used():
    _sync._reset_bump_used()
    yield
    _sync._reset_bump_used()


# #33 FLIP (P5): stamp changed = different MVID halves → 'sync clean' (not no-op)
# Uses realistic <guid>:<ticks> format; MVID-only partition comparison
@pytest.mark.asyncio
async def test_stamp_changed_is_sync_clean():
    """P5: different MVID halves → sync clean. Realistic guid:ticks fixtures."""
    pre_mvid  = "60d2de34-f1b2-4c3d-a5e6-789012345678"
    post_mvid = "99aabbcc-0011-2233-4455-667788990011"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={pre_mvid}:639169455305003280",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={post_mvid}:639169455309999999"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
    assert "no-op" not in result


# #34: stamp unchanged (same MVID) with expected compile → REIMPORT-NEEDED (G18)
@pytest.mark.asyncio
async def test_stamp_unchanged_is_noop():
    """MCP091-009: same MVID + will_compile=true + no errors → 'sync clean' (no-op compile).

    A comment/whitespace edit triggers will_compile=true but leaves MVID stable.
    With no compile errors, the domain IS live — returning REIMPORT-NEEDED was a
    false positive. After fix 2.4, this is 'sync clean'.
    """
    mvid = "60d2de34-f1b2-4c3d-a5e6-789012345678"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:639169455305003280",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:639169455309999999"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result, f"No-op compile (MVID stable + no errors) → sync clean, got {result!r}"
    assert "REIMPORT-NEEDED" not in result



# A5: frozen MVID + will_compile=false → fast-path CLEAN (no compile expected)
@pytest.mark.asyncio
async def test_same_mvid_no_compile_is_noop():
    """A5 fast-path: frozen MVID + will_compile=false → NO-OP / CLEAN, not REIMPORT-NEEDED.

    When Unity doesn't expect a compile (will_compile=false) and the MVID hasn't
    changed, there is nothing to reimport. The sync should report clean/no-op.
    Companion to test_same_mvid_with_will_compile_yields_reimport; live-verify in Phase D.
    """
    mvid = "aabbccdd-1122-3344-5566-778899aabbcc"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100000000000",
        ack="sync_ack|epoch=1|will_compile=false",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:999999999999"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "REIMPORT-NEEDED" not in result, (
        f"Frozen MVID + no compile expected must NOT yield REIMPORT-NEEDED, got {result!r}"
    )


# P5 NEW: expected IL change but MVID unchanged → STOP "build no-op suspected"
@pytest.mark.asyncio
async def test_expected_compile_mvid_unchanged_is_stop():
    """MCP091-009: will_compile=true + ready + same MVID + no errors → sync clean (no-op).

    P5 was wrong: REIMPORT-NEEDED fired even for no-IL-change compiles (comment edits).
    After fix 2.4: no errors + compile completed (state=ready) = domain is live.
    """
    mvid = "deadbeef-dead-beef-dead-beefdeadbeef"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:200"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result, (
        f"no-IL-change compile (no errors) must return sync clean, got: {result!r}"
    )


# #35: stamp absent in both pre and post → legacy 'sync clean'
@pytest.mark.asyncio
async def test_stamp_absent_is_legacy_clean():
    _sync._send = _make_send_with_stamp(
        pre_status="epoch=0|state=ready",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
    assert "no-op" not in result


# #36: timeout → STOP verdict
@pytest.mark.asyncio
async def test_timeout_returns_stop_verdict():
    async def _stuck(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            return "epoch=0|state=idle"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        return ""

    _sync._send = _stuck
    result = await _sync.sync_unity(timeout=0.0)
    assert result.startswith("STOP:")


# #37: second bump returns STOP
@pytest.mark.asyncio
async def test_second_bump_returns_stop(monkeypatch):
    monkeypatch.setattr(_sync, "_bump_used", True)
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[],
    )
    result = await _sync.sync_unity(bump=True, timeout=60.0)
    assert result.startswith("STOP: bump already used")


# #38: ConnectionError on pre-read → pre='' → stamp changed path (not no-op)
@pytest.mark.asyncio
async def test_stamp_pre_connection_error_treated_as_changed():
    call_count = [0]

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            call_count[0] += 1
            if call_count[0] == 1:
                raise ConnectionError("gone")  # pre-read fails → stamp_pre=''
            return "epoch=1|state=ready|stamp=xyz"  # post-poll succeeds
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
    assert "no-op" not in result


# #39 (critique-required): bump guard resets on reconnect
def test_bump_guard_resets_on_reconnect():
    _sync._bump_used = True
    _sync._reset_bump_used()
    assert _sync._bump_used is False


# #40 (critique-required): pre-read sync_status returns state=failed with no stamp → pre=''
@pytest.mark.asyncio
async def test_stamp_pre_from_failed_state():
    _sync._send = _make_send_with_stamp(
        pre_status="epoch=0|state=failed",  # no stamp field
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready|stamp=xyz"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
    assert "no-op" not in result


# P3: sentinel-strip — 'No compilation errors' must not leak as error payload in sync path
@pytest.mark.asyncio
async def test_sync_sentinel_stripped(_patch_corroborate):
    """P3: get_corroborated_errors returns '' when C# says 'No compilation errors' → sync clean."""
    # The default _patch_corroborate fixture already strips the sentinel in _default_get_corroborated
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
        errors_response="No compilation errors",
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "No compilation errors" not in result
    assert "sync clean" in result



# G18: ready arm MVID unchanged after expected compile → REIMPORT-NEEDED
@pytest.mark.asyncio
async def test_bump_unchanged_mvid_yields_reimport_needed():
    """MCP091-009: will_compile=true + ready + same MVID + no errors → sync clean.

    G18 was wrong: REIMPORT-NEEDED fired for no-IL-change compiles (false positive).
    After fix 2.4: compile completed (state=ready) + no errors = domain is live → sync clean.

    For stale domain (errors present), see test_mvid_stable_with_errors_is_reimport_needed.
    """
    mvid = "deadbeef-dead-beef-dead-beefdeadbeef"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:200"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result, \
        f"no-IL-change compile (no errors) → sync clean, got {result!r}"


@pytest.mark.asyncio
async def test_mvid_stable_with_errors_is_reimport_needed():
    """MCP091-009 companion: same MVID + will_compile=true + actual errors → REIMPORT-NEEDED."""
    mvid = "deadbeef-dead-beef-dead-beefdeadbeef"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:200"],
        errors_response="Assets/Foo.cs(1,1): error CS0246: type not found",
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert ("REIMPORT-NEEDED" in result or "MANUAL-REQUIRED" in result or "error CS" in result), \
        f"Stale domain (errors present) must not return clean, got {result!r}"


# Item 1 (expected_compile threading / A5): will_compile=false + frozen MVID → NOT REIMPORT-NEEDED
@pytest.mark.asyncio
async def test_no_compile_frozen_mvid_is_not_reimport_needed():
    """Item 1 (A5 expected_compile gate): will_compile=false → NOT REIMPORT-NEEDED.

    Red-precondition: if the ready-arm REIMPORT-NEEDED check ignores will_compile,
    a cache-hit (no compile expected) would incorrectly return REIMPORT-NEEDED.
    Wire: will_compile=false ack + a will_compile=true post-status with frozen MVID
    so the ready-arm IS reachable and tests the expected_compile gate.
    Assert: result is NOT REIMPORT-NEEDED and NOT STALE-DOMAIN.
    A8: feeds REAL stamp wire; inject only _send.
    """
    mvid = "cccccccc-cccc-cccc-cccc-cccccccccccc"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100",
        ack="sync_ack|epoch=1|will_compile=false",
        # will_compile=false → fast-path, poll loop never entered, no status_seq consumed
        status_seq=[],
    )
    result = await _sync.sync_unity(timeout=60.0)
    # Fast-path for will_compile=false: should return clean, NEVER REIMPORT-NEEDED/STALE-DOMAIN
    assert "REIMPORT-NEEDED" not in result, \
        f"Cache-hit (no compile expected) must NOT be REIMPORT-NEEDED, got {result!r}"
    assert "STALE-DOMAIN" not in result, \
        f"Cache-hit must NOT be STALE-DOMAIN, got {result!r}"


# ── R1: _attempt_recovery tests (P1-P5) ──────────────────────────────────────

# P1: MVID changed after force_refresh → _attempt_recovery returns None (healed)
@pytest.mark.asyncio
async def test_recovery_heals_when_mvid_changes(monkeypatch):
    """P1: force_refresh succeeds + MVID delta → None (healed)."""
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 5.0)
    mvid_pre  = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    mvid_post = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"

    async def _send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "force_refresh triggered"
        if cmd == "sync_status":
            return f"epoch=0|state=ready|stamp={mvid_post}:12345"
        raise AssertionError(f"Unexpected: {cmd}")

    result = await _sync._attempt_recovery(_send, mvid_pre)
    assert result is None, f"Expected None (healed), got {result!r}"


# P2: MVID frozen after timeout → _attempt_recovery returns REIMPORT-NEEDED
@pytest.mark.asyncio
async def test_recovery_returns_reimport_when_mvid_frozen(monkeypatch):
    """P2: force_refresh + frozen MVID for full timeout → REIMPORT-NEEDED verdict."""
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)  # instant timeout
    mvid = "cccccccc-cccc-cccc-cccc-cccccccccccc"

    async def _send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "force_refresh triggered"
        raise AssertionError(f"Unexpected: {cmd}")  # no sync_status with timeout=0

    result = await _sync._attempt_recovery(_send, mvid)
    assert result is not None
    assert "REIMPORT-NEEDED" in result
    assert mvid in result


# P3: recovery called exactly once, no recursion from sync_unity
@pytest.mark.asyncio
async def test_recovery_called_exactly_once(monkeypatch):
    """P3: sync_unity calls _attempt_recovery exactly once (no self-recursion)."""
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)
    recovery_calls = []

    real_attempt_recovery = _sync._attempt_recovery

    async def _spy_recovery(send, mvid_pre, send_reload=None, **kwargs):
        recovery_calls.append(mvid_pre)
        return await real_attempt_recovery(send, mvid_pre, send_reload, **kwargs)

    monkeypatch.setattr(_sync, "_attempt_recovery", _spy_recovery)

    mvid = "deadbeef-dead-beef-dead-beefdeadbeef"
    _sync._send = _make_send_with_stamp(
        pre_status=f"epoch=0|state=ready|stamp={mvid}:100",
        ack="sync_ack|epoch=1|will_compile=true",
        status_seq=[f"epoch=1|state=ready|stamp={mvid}:200"],
    )
    await _sync.sync_unity(timeout=60.0)
    assert len(recovery_calls) == 1, f"Expected 1 recovery call, got {len(recovery_calls)}"


# P4: force_refresh sent with empty args {}
@pytest.mark.asyncio
async def test_recovery_sends_force_refresh_with_correct_args(monkeypatch):
    """P4: _attempt_recovery sends force_refresh with args={}."""
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)
    captured = {}

    async def _send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            captured["cmd"] = cmd
            captured["args"] = args
            return "force_refresh triggered"
        raise AssertionError(f"Unexpected: {cmd}")

    await _sync._attempt_recovery(_send, "some-mvid")
    assert captured.get("cmd") == "force_refresh"
    assert captured.get("args") == {}


# P5: sync_status polled during recovery (MVID check happens)
@pytest.mark.asyncio
async def test_recovery_polls_sync_status(monkeypatch):
    """P5: after force_refresh, recovery polls sync_status to check MVID."""
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 5.0)
    mvid_pre  = "11111111-1111-1111-1111-111111111111"
    mvid_post = "22222222-2222-2222-2222-222222222222"
    status_calls = []

    async def _send(cmd, args=None, **kwargs):
        if cmd == "force_refresh":
            return "force_refresh triggered"
        if cmd == "sync_status":
            status_calls.append(1)
            return f"epoch=0|state=ready|stamp={mvid_post}:99"
        raise AssertionError(f"Unexpected: {cmd}")

    result = await _sync._attempt_recovery(_send, mvid_pre)
    assert result is None, "Expected healed (None)"
    assert len(status_calls) >= 1, "sync_status must be polled during recovery"


# A1: ConnectionError on force_refresh → sentinel string, NOT None (A1 violation guard)
@pytest.mark.asyncio
async def test_recovery_connection_error_returns_sentinel_not_none():
    """A1: TCP unreachable during force_refresh must NOT be mistaken for healed (None).

    Red-precondition: before fix, ConnectionError returned None → caller returned
    'sync clean' — false heal without MVID delta (A1 violation).
    After fix: must return a REIMPORT-NEEDED sentinel string.
    """
    async def _tcp_dead(cmd, args=None, **kwargs):
        raise ConnectionError("Unity unreachable")

    result = await _sync._attempt_recovery(_tcp_dead, "some-mvid")
    assert result is not None, "ConnectionError must NOT return None (false heal)"
    assert "REIMPORT-NEEDED" in result, f"Expected REIMPORT-NEEDED sentinel, got {result!r}"


# B1: REIMPORT-NEEDED from _attempt_recovery → escalates to run_ladder T2-T5
@pytest.mark.asyncio
async def test_sync_unity_escalates_to_run_ladder_on_reimport(monkeypatch):
    """B1: when _attempt_recovery returns REIMPORT-NEEDED, sync_unity must call run_ladder T2+.

    Scenario: compiling+dur=0.0 (backgrounded) → _attempt_recovery returns REIMPORT-NEEDED
    → run_ladder called with start_tier=2 → returns HEALED.
    DoD: result contains 'HEALED' (not REIMPORT-NEEDED from _attempt_recovery alone).
    """
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)  # _attempt_recovery exits immediately

    ladder_called = []

    async def _mock_run_ladder(send, *, send_reload=None, bump_file=None,
                               osascript_runner=None, play_stop_consent=False,
                               start_tier=1):
        ladder_called.append(start_tier)
        return "HEALED: T2 mvid aaa->bbb"

    monkeypatch.setattr(_sync, "_run_ladder", _mock_run_ladder)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"
        if cmd == "force_refresh":
            return "ok"  # _attempt_recovery fires but MVID unchanged → REIMPORT-NEEDED
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)

    assert ladder_called, "run_ladder must be called when _attempt_recovery returns REIMPORT-NEEDED"
    assert ladder_called[0] == 2, f"run_ladder must start at T2 (skip T1 already done), got start_tier={ladder_called[0]}"
    assert "HEALED" in result, f"Expected HEALED from run_ladder, got {result!r}"


# M4: integration with valid main_mvid — run_ladder T2+ triggered (not hollow T0 exit)
@pytest.mark.asyncio
async def test_sync_unity_run_ladder_starts_at_t2_with_valid_mvid(monkeypatch):
    """M4: frozen MVID + valid main_mvid → run_ladder called with start_tier=2.

    Distinguishes from hollow test where main_mvid=absent causes T0 short-circuit.
    Verifies the escalation path is real (T1 already attempted via _attempt_recovery).
    """
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)

    MAIN_MVID = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    ladder_calls = []

    async def _mock_run_ladder(send, *, send_reload=None, bump_file=None,
                               osascript_runner=None, play_stop_consent=False,
                               start_tier=1):
        ladder_calls.append(start_tier)
        return f"HEALED: T2 mvid {MAIN_MVID}->bbbbbbbb"

    monkeypatch.setattr(_sync, "_run_ladder", _mock_run_ladder)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"
        if cmd == "force_refresh":
            return "ok"
        if cmd == "diagnose":
            # valid main_mvid — ladder proceeds past T0 (no absent short-circuit)
            return (
                f"mvid={MAIN_MVID}\n"
                f"main_mvid={MAIN_MVID}\n"
                "iscompiling=false  cn_active=false  started=false  stamp_frozen=true\n"
                "compile=idle-stale\n"
                f"stamp={MAIN_MVID}:100\n"
                "errors=\nlog=clean\n"
            )
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)

    assert ladder_calls, "run_ladder must be called when _attempt_recovery returns REIMPORT-NEEDED"
    assert ladder_calls[0] == 2, f"run_ladder must start at T2 (T1 done via _attempt_recovery), got {ladder_calls[0]}"
    assert "HEALED" in result, f"Expected HEALED from run_ladder, got {result!r}"


# ── False-backgrounded regression tests ──────────────────────────────────────

# FB-1: dur=0.0 on first poll but compile completes → NOT backgrounded (no skip)
@pytest.mark.asyncio
async def test_sync_no_false_backgrounded_on_early_dur_zero():
    """dur=0.0 on first poll then state=ready: must complete, not trigger backgrounded hint.

    Scenario: fast compile where dur resets to 0.0 before first poll (or compile
    finishes before dur is updated). _FOCUS_HINT_AFTER default (15s) is NOT reached
    because asyncio.sleep is mocked → timeline doesn't advance → hint never fires.
    """
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            "epoch=1|state=compiling|dur=0.0",  # first poll: dur=0.0
            "epoch=1|state=ready",               # compile done fast
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# FB-2: dur progresses 0.0 → nonzero → ready: must complete normally
@pytest.mark.asyncio
async def test_sync_no_false_backgrounded_when_dur_progresses():
    """dur=0.0 → dur=1.5 → state=ready: compile is live, must not trigger backgrounded.

    Even with _FOCUS_HINT_AFTER=0 (instant threshold), once dur becomes nonzero
    the backgrounded condition (dur=0.0) is no longer met, so the hint must not fire.
    The sequence exits cleanly on state=ready.
    """
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            "epoch=1|state=compiling|dur=0.0",  # compile not yet ticked
            "epoch=1|state=compiling|dur=1.5",  # compile in progress
            "epoch=1|state=ready",               # done
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# A1 integration: sync_unity emits REIMPORT-NEEDED (not 'sync clean') when force_refresh TCP-dead
@pytest.mark.asyncio
async def test_sync_unity_emits_reimport_when_recovery_tcp_dead(monkeypatch):
    """A1 integration: backgrounded-compile path with TCP-dead force_refresh.

    sync_unity must emit REIMPORT-NEEDED, not 'sync clean', when force_refresh
    raises ConnectionError (TCP unreachable during recovery).
    """
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"
        if cmd == "force_refresh":
            raise ConnectionError("TCP gone")
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)
    assert "REIMPORT-NEEDED" in result, f"Expected REIMPORT-NEEDED, got {result!r}"
    assert "sync clean" not in result, f"Must NOT be sync clean when TCP dead, got {result!r}"


# ── resolve boolean encoding (Pattern A) ─────────────────────────────────────

@pytest.mark.asyncio
async def test_sync_resolve_false_omits_key():
    """resolve=False (default) omits 'resolve' key from sync command (Pattern A)."""
    captured: dict = {}

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            captured.update(args or {})
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=1|state=idle"
        return ""

    _sync._send = _send
    await _sync.sync_unity(resolve=False)
    assert "resolve" not in captured, (
        f"'resolve' should be omitted when False, got {captured.get('resolve')!r}"
    )


@pytest.mark.asyncio
async def test_sync_resolve_true_sends_string():
    """resolve=True sends 'resolve': 'true' string to sync command (Pattern A)."""
    captured: dict = {}

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            captured.update(args or {})
            return "sync_ack|epoch=1|will_compile=false"
        if cmd == "sync_status":
            return "epoch=1|state=idle"
        return ""

    _sync._send = _send
    await _sync.sync_unity(resolve=True)
    assert captured.get("resolve") == "true", (
        f"Expected 'true' (str), got {captured.get('resolve')!r}"
    )


# ---------------------------------------------------------------------------
# Sub-task B: file:UPM trap test (T6)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_sync_unity_backgrounded_dispatches_force_refresh_not_recompile(monkeypatch):
    """Backgrounded editor (state=compiling, dur=0.0) → sync_unity fires force_refresh.

    Regression guard: must NOT dispatch recompile (it misses the .mvfrm nuke step).
    Recovery is triggered immediately via _FOCUS_HINT_AFTER=0.0.
    """
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)  # recovery exits fast

    dispatched: list[str] = []

    async def _send(cmd, args=None, **kwargs):
        dispatched.append(cmd)
        if cmd == "sync_status":
            if "sync" not in dispatched:  # pre-stamp read (before sync dispatched)
                return "epoch=0|state=idle"
            return "epoch=1|state=compiling|dur=0.0"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "force_refresh":
            return "force_refresh triggered"
        if cmd == "diagnose":
            return "main_mvid=absent"  # run_ladder short-circuit
        return ""

    _sync._send = _send
    await _sync.sync_unity(timeout=60.0)

    assert "force_refresh" in dispatched, (
        "sync_unity must dispatch force_refresh for backgrounded editor"
    )
    assert "recompile" not in dispatched, (
        "must NOT dispatch recompile (it misses the .mvfrm nuke step)"
    )


# ---------------------------------------------------------------------------
# IF-1: timed_send deadline propagation
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_timed_send_raises_when_deadline_past():
    """Expired deadline raises TimeoutError without calling send."""
    import asyncio
    import time
    from unity_mcp.tools.sync import _timed_send as timed_send

    send = AsyncMock()
    with pytest.raises(asyncio.TimeoutError):
        await timed_send(send, "sync_status", {}, time.monotonic() - 1.0)
    send.assert_not_called()


@pytest.mark.asyncio
async def test_timed_send_cancels_slow_send():
    """Send that exceeds deadline is cancelled."""
    import asyncio
    import time
    from unity_mcp.tools.sync import _timed_send as timed_send

    async def slow_send(cmd, args):
        fut = asyncio.get_running_loop().create_future()
        await fut  # never resolves

    t0 = time.monotonic()
    with pytest.raises(asyncio.TimeoutError):
        await timed_send(slow_send, "sync_status", {}, time.monotonic() + 0.05)
    elapsed = time.monotonic() - t0
    assert elapsed < 1.0


# ---------------------------------------------------------------------------
# IF-3: warm_type_cache after sync_unity
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_sync_warms_type_cache_after_ready():
    """sync_unity calls warm_type_cache after reaching ready state."""
    calls = []

    async def _send(cmd, args=None, **kw):
        calls.append(cmd)
        if cmd == "sync_status":
            return "epoch=1|state=ready|stamp=mvid1:tick1"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "get_compile_errors":
            return ""
        if cmd == "warm_type_cache":
            return "ok:types=42"
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
    assert "warm_type_cache" in calls


@pytest.mark.asyncio
async def test_sync_survives_warm_cache_connection_error():
    """sync_unity returns 'sync clean' even if warm_type_cache raises."""
    calls = []

    async def _send(cmd, args=None, **kw):
        calls.append(cmd)
        if cmd == "sync_status":
            return "epoch=1|state=ready|stamp=mvid1:tick1"
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "get_compile_errors":
            return ""
        if cmd == "warm_type_cache":
            raise ConnectionError("TCP gone")
        return ""

    _sync._send = _send
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
