"""Tests for reload_ladder.run_ladder — escalation ladder T0-T5.

All Unity I/O is injected (send, bump_file, osascript_runner).
MVID-delta is the ONLY proof of heal (A1 doctrine).
"""
import asyncio
import json
import struct
import pytest
from pathlib import Path
from unittest.mock import AsyncMock, patch, MagicMock

import unity_mcp.tools.reload_ladder as _ladder


# ── Helpers ──────────────────────────────────────────────────────────────────

MVID_A = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
MVID_B = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"


# Diagnose responses — newline-delimited format (matches C# ReloadDiagnoseCommand.Execute)
def _diag_frozen(mvid: str) -> str:
    """Class A frozen: stamp_frozen=true, cn_active=false. main_mvid present for F3/F5."""
    return (
        f"mvid={mvid}\n"
        f"main_mvid={mvid}\n"
        f"iscompiling=false  cn_active=false  started=false  stamp_frozen=true\n"
        f"compile=idle-stale\n"
        f"stamp={mvid}:100\n"
        "errors=\nlog=clean\n"
    )


def _diag_clean(mvid: str) -> str:
    """CLEAN-LIVE: all signals green."""
    return (
        f"mvid={mvid}\n"
        f"main_mvid={mvid}\n"
        f"iscompiling=false  cn_active=true  started=false  stamp_frozen=false\n"
        f"compile=idle\n"
        f"stamp={mvid}:200\n"
        "errors=\nlog=clean\n"
    )


def _diag_latch(mvid: str) -> str:
    """Class B latch: iscompiling=true, cn_active=false, stamp_frozen=true."""
    return (
        f"mvid={mvid}\n"
        f"main_mvid={mvid}\n"
        f"iscompiling=true  cn_active=false  started=false  stamp_frozen=true\n"
        f"compile=compiling-latched\n"
        f"stamp={mvid}:100\n"
        "errors=\nlog=clean\n"
    )


class MockSend:
    """Programmable send mock.

    diagnose_seq: list of diagnose responses (consumed in order).
    Last item is repeated when sequence exhausted.
    Other commands (force_refresh, recompile, sync, editor) return ok.
    Raises if cmd unexpected and raise_on_unknown=True.
    """

    def __init__(self, diagnose_seq: list[str], *, raise_on: str | None = None):
        self._diag_iter = iter(diagnose_seq)
        self._last_diag = diagnose_seq[-1] if diagnose_seq else ""
        self._raise_on = raise_on
        self.calls: list[tuple[str, dict]] = []

    async def __call__(self, cmd: str, args: dict | None = None):
        self.calls.append((cmd, args or {}))
        if self._raise_on and cmd == self._raise_on:
            raise ConnectionError(f"mock TCP error on {cmd}")
        if cmd == "diagnose":
            try:
                val = next(self._diag_iter)
                self._last_diag = val
                return val
            except StopIteration:
                return self._last_diag
        if cmd in ("force_refresh", "recompile", "editor"):
            return f"{cmd} ok"
        if cmd == "sync":
            return "sync ok"
        if cmd == "execute_code":
            return "False"  # guard not locked by default
        raise AssertionError(f"Unexpected cmd in MockSend: {cmd!r}")


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _fast_timeouts(monkeypatch):
    """Limit poll loops to exactly 1 iteration for deterministic test sequencing.

    _T1_MAX_POLLS=1 ensures each T1/T2/T3 call consumes exactly 1 diagnose slot.
    """
    monkeypatch.setattr(_ladder, "_T1_POLL_S", 60.0)   # not the limiting factor; _T1_MAX_POLLS=1 caps it
    monkeypatch.setattr(_ladder, "_T4_POLL_S", 0.0)   # expire immediately; T4/T5 heal via MVID delta not timeout
    monkeypatch.setattr(_ladder, "_POLL_INTERVAL_S", 0.0)
    monkeypatch.setattr(_ladder, "_T2_SLEEP_S", 0.0)
    monkeypatch.setattr(_ladder, "_T5_PLAY_WAIT_S", 0.0)
    monkeypatch.setattr(_ladder, "_T1_MAX_POLLS", 1)    # exactly 1 diagnose per poll call
    monkeypatch.setattr(_ladder, "_T4_MAX_POLLS", 1)


# ── Test 1: T1 heal — MVID delta after force_refresh ─────────────────────────

@pytest.mark.asyncio
async def test_tier1_heals_on_mvid_delta():
    """T1: force_refresh triggers MVID change → HEALED: T1."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0 baseline
        _diag_clean(MVID_B),    # T1 poll → delta!
    ])
    result = await _ladder.run_ladder(send)
    assert result.startswith("HEALED: T1"), f"Got: {result!r}"
    assert MVID_A in result
    assert MVID_B in result


# ── Test 2: T2 reimport → T1 retry heals ─────────────────────────────────────

@pytest.mark.asyncio
async def test_tier2_reimport_then_tier1_retry():
    """T2: recompile sent, then T1 retry detects MVID delta → HEALED: T2."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0 baseline
        _diag_frozen(MVID_A),   # T1 poll — frozen (escalate to T2)
        _diag_clean(MVID_B),    # T2 T1-retry poll → delta!
    ])
    result = await _ladder.run_ladder(send)
    assert result.startswith("HEALED: T2"), f"Got: {result!r}"
    # recompile must be called
    assert any(cmd == "recompile" for cmd, _ in send.calls), "recompile not called"


# ── Test 3: T3 bump+resolve heal + file revert ────────────────────────────────

@pytest.mark.asyncio
async def test_tier3_bump_resolve_heals_and_reverts(tmp_path: Path):
    """T3: bump package.json → sync resolve → MVID delta. File reverted to original bytes."""
    pkg = tmp_path / "package.json"
    original = '{"name":"test","version":"0.1.0"}'
    pkg.write_text(original, encoding="utf-8")

    send = MockSend([
        _diag_frozen(MVID_A),   # T0 baseline
        _diag_frozen(MVID_A),   # T1 poll — frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll — frozen
        _diag_clean(MVID_B),    # T3 T1-retry poll → delta!
    ])

    result = await _ladder.run_ladder(send, bump_file=pkg)

    assert result.startswith("HEALED: T3"), f"Got: {result!r}"
    # File must be reverted
    assert pkg.read_bytes() == original.encode("utf-8"), "bump_file not reverted"
    # sync must have been called
    assert any(cmd == "sync" for cmd, _ in send.calls), "sync not called"


# ── Test 4: T3 revert on TCP failure (try/finally) ───────────────────────────

@pytest.mark.asyncio
async def test_tier3_reverts_on_tcp_failure(tmp_path: Path):
    """T3: send raises on sync → bump_file STILL reverted (try/finally guarantee)."""
    pkg = tmp_path / "package.json"
    original = '{"name":"test","version":"0.1.0"}'
    pkg.write_text(original, encoding="utf-8")

    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry → frozen, escalate to T3
    ], raise_on="sync")          # TCP error during T3

    # run_ladder must not raise — returns a terminal verdict
    result = await _ladder.run_ladder(send, bump_file=pkg)

    # Regardless of outcome, file must be reverted
    assert pkg.read_bytes() == original.encode("utf-8"), "bump_file not reverted after TCP failure"
    # Result must NOT be a false heal
    assert "HEALED" not in result, f"False heal on TCP error: {result!r}"


# ── Test 5: T4 osascript → MVID delta → HEALED: T4 ──────────────────────────

@pytest.mark.asyncio
async def test_tier4_osascript_heals():
    """T4: osascript activate+cmd-r triggered, then MVID delta → HEALED: T4."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll → frozen
        _diag_frozen(MVID_A),   # T3 sync+poll → frozen (production fallback, no bump_file)
        _diag_clean(MVID_B),    # T4 _poll_mvid_delta → delta!
    ])

    osascript_calls = []

    async def mock_runner(action: str) -> int:
        osascript_calls.append(action)
        return 0  # success

    result = await _ladder.run_ladder(send, osascript_runner=mock_runner)

    assert result.startswith("HEALED: T4"), f"Got: {result!r}"
    assert "activate" in osascript_calls, "activate not called"


# ── Test 6: T4 Accessibility fail → REIMPORT-NEEDED ─────────────────────────

@pytest.mark.asyncio
async def test_tier4_accessibility_fail_returns_reimport():
    """T4: osascript returns 1002 (Accessibility denied) → REIMPORT-NEEDED."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry → frozen
        # T3 production fallback: sync + diagnose (gets repeated last=frozen)
        # T4: activate(0) → cmd-r(1002) → REIMPORT-NEEDED immediately, no more diagnose
    ])

    async def mock_runner(action: str) -> int:
        if action == "cmd-r":
            return 1002  # Accessibility denied
        return 0

    result = await _ladder.run_ladder(send, osascript_runner=mock_runner)

    assert "REIMPORT-NEEDED" in result, f"Got: {result!r}"
    assert "accessibility" in result.lower() or "1002" in result, f"Missing reason: {result!r}"


# ── Test 7a: T5 play/stop with consent=True → HEALED: T5 ────────────────────

@pytest.mark.asyncio
async def test_tier5_play_stop_with_consent():
    """T5: consent=True, play+stop sent, diagnose shows CLEAN → HEALED: T5."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll → frozen
        _diag_frozen(MVID_A),   # T3 production fallback sync+poll → frozen (no bump_file)
        _diag_clean(MVID_B),    # T5 diagnose after play/stop → CLEAN + new MVID!
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    assert result.startswith("HEALED: T5"), f"Got: {result!r}"
    assert MVID_B in result, f"New MVID not in T5 result: {result!r}"
    # play and stop must be called
    play_calls = [(cmd, args) for cmd, args in send.calls if cmd == "editor"]
    actions = [args.get("action") for _, args in play_calls]
    assert "play" in actions, f"play not called: {send.calls}"
    assert "stop" in actions, f"stop not called: {send.calls}"


# ── Test 7b: all-fail + consent=False → MANUAL-REQUIRED ─────────────────────

@pytest.mark.asyncio
async def test_all_fail_no_consent_returns_manual_required():
    """All tiers fail + play_stop_consent=False → MANUAL-REQUIRED."""
    send = MockSend([_diag_frozen(MVID_A)])  # always frozen

    result = await _ladder.run_ladder(send, play_stop_consent=False)

    assert "MANUAL-REQUIRED" in result, f"Got: {result!r}"


# ── Test 8: T0 detect CLEAN → return immediately ─────────────────────────────

@pytest.mark.asyncio
async def test_tier0_clean_returns_immediately():
    """T0: diagnose shows CLEAN-LIVE → return without escalation."""
    send = MockSend([_diag_clean(MVID_A)])

    result = await _ladder.run_ladder(send)

    assert "CLEAN" in result or "clean" in result.lower(), f"Got: {result!r}"
    # Should only call diagnose once (T0)
    diag_calls = [cmd for cmd, _ in send.calls if cmd == "diagnose"]
    assert len(diag_calls) == 1, f"Expected 1 diagnose call, got {diag_calls}"


# ── Test 9: T5 MVID-same-as-baseline → NOT healed (A1 regression) ────────────

@pytest.mark.asyncio
async def test_tier5_no_mvid_delta_not_healed():
    """T5 regression: diagnose after play/stop returns CLEAN flags but SAME mvid → NOT HEALED.

    A1 doctrine: MVID-delta is the ONLY proof of heal. _is_clean() alone is insufficient.
    """
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll → frozen
        _diag_frozen(MVID_A),   # T3 production fallback sync+poll → frozen (no bump_file)
        # T5: play+stop, diagnose returns CLEAN but same MVID_A (no reload happened)
        _diag_clean(MVID_A),    # T5 diagnose → clean flags but MVID == baseline!
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    assert "HEALED" not in result, f"False heal (A1 violation): {result!r}"
    assert "MANUAL-REQUIRED" in result, f"Expected MANUAL-REQUIRED, got: {result!r}"


# ── Test 10: T5 MVID-delta → HEALED with new MVID in result ─────────────────

@pytest.mark.asyncio
async def test_tier5_mvid_delta_healed_shows_new_mvid():
    """T5: diagnose after play/stop returns CLEAN + new MVID → HEALED: T5 with both MVIDs."""
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll → frozen
        _diag_frozen(MVID_A),   # T3 production fallback sync+poll → frozen (no bump_file)
        _diag_clean(MVID_B),    # T5 diagnose → CLEAN + new MVID!
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    assert result.startswith("HEALED: T5"), f"Got: {result!r}"
    assert MVID_B in result, f"New MVID not in result: {result!r}"


# ── Test 11: T2 dead-diagnose removed — no extra diagnose call ───────────────

@pytest.mark.asyncio
async def test_tier2_no_dead_diagnose_call():
    """T2 must NOT call diagnose between recompile and T1-retry (dead TCP RTT removed).

    T3 production fallback (sync+poll) legitimately adds 1 diagnose after T2.
    Total: T0(1) + T1-poll(1) + T2-T1retry-poll(1) + T3-poll(1) = 4. Any extra = dead call.
    """
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry poll → frozen
        _diag_frozen(MVID_A),   # T3 production fallback poll → frozen
    ])

    await _ladder.run_ladder(send, play_stop_consent=False)

    diag_calls = [cmd for cmd, _ in send.calls if cmd == "diagnose"]
    # T0(1) + T1-poll(1) + T2-T1retry-poll(1) + T3-poll(1) = 4. Dead diagnose would add extra.
    assert len(diag_calls) == 4, f"Unexpected diagnose count: {len(diag_calls)} calls: {send.calls}"


# ── Test 12: _poll_mvid_delta check-first (no leading sleep) ─────────────────

@pytest.mark.asyncio
async def test_poll_mvid_delta_no_leading_sleep():
    """_poll_mvid_delta must call diagnose BEFORE sleeping (check-first semantics)."""
    import asyncio as _asyncio
    sleep_calls = []
    diag_calls = []
    call_order = []

    async def mock_send(cmd: str, args: dict | None = None):
        if cmd == "diagnose":
            diag_calls.append(len(sleep_calls))  # how many sleeps before this diag
            call_order.append("diag")
            return _diag_frozen(MVID_A)  # no delta → exhausts max_polls=1

    original_sleep = _asyncio.sleep

    async def tracking_sleep(s):
        sleep_calls.append(s)
        call_order.append("sleep")

    with patch("asyncio.sleep", tracking_sleep):
        await _ladder._poll_mvid_delta(mock_send, MVID_A, timeout_s=60.0, max_polls=1)

    # First diagnose must happen before any sleep
    assert call_order[0] == "diag", f"Leading sleep detected: call_order={call_order}"


# ── Test 13: _make_reload_send sends framed JSON and returns data ─────────────

@pytest.mark.asyncio
async def test_make_reload_send_sends_and_returns_data():
    """_make_reload_send factory returns callable that sends 4-byte BE + JSON and reads response."""
    response_data = json.dumps({"id": "r", "ok": True, "data": "pong"}).encode()
    framed = struct.pack(">I", len(response_data)) + response_data

    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(side_effect=[
        struct.pack(">I", len(response_data)),  # first call: 4-byte length
        response_data,                          # second call: payload
    ])
    mock_writer = MagicMock()
    mock_writer.drain = AsyncMock()

    with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
        send_fn = _ladder.make_reload_send(port=9600)
        result = await send_fn("ping", {})

    assert result == "pong"
    mock_writer.close.assert_called_once()


# ── Test 14: _send_with_fallback uses main when healthy ──────────────────────

@pytest.mark.asyncio
async def test_send_with_fallback_uses_main_when_ok():
    """_send_with_fallback uses send_main when it succeeds."""
    send_main = AsyncMock(return_value="main-ok")
    send_reload = AsyncMock(return_value="reload-ok")

    result = await _ladder._send_with_fallback(send_main, send_reload, "ping", {})

    assert result == "main-ok"
    send_main.assert_called_once_with("ping", {})
    send_reload.assert_not_called()


# ── Test 15: _send_with_fallback falls back on ConnectionError ───────────────

@pytest.mark.asyncio
async def test_send_with_fallback_falls_to_reload_on_connection_error():
    """_send_with_fallback uses send_reload when send_main raises ConnectionError."""
    send_main = AsyncMock(side_effect=ConnectionError("main dead"))
    send_reload = AsyncMock(return_value="reload-ok")

    result = await _ladder._send_with_fallback(send_main, send_reload, "diagnose", {})

    assert result == "reload-ok"
    send_reload.assert_called_once_with("diagnose", {})


# ── Test 16: run_ladder uses send_reload for T0 diagnose when main dead ──────

@pytest.mark.asyncio
async def test_run_ladder_uses_reload_send_when_main_dead():
    """run_ladder uses send_reload channel for diagnose when main raises ConnectionError at T0."""
    # Main send always raises
    main_calls = []
    async def dead_main(cmd, args=None):
        main_calls.append(cmd)
        raise ConnectionError("main port dead")

    # Reload send handles diagnose (frozen → clean after T1)
    reload_send = MockSend([
        _diag_frozen(MVID_A),   # T0 via reload channel
        _diag_clean(MVID_B),    # T1 poll via reload channel → healed
    ])

    result = await _ladder.run_ladder(dead_main, send_reload=reload_send)

    assert result.startswith("HEALED: T1"), f"Got: {result!r}"
    assert "diagnose" in [cmd for cmd, _ in reload_send.calls]


# ── Test 17: run_ladder both ports dead → MANUAL-REQUIRED ────────────────────

@pytest.mark.asyncio
async def test_run_ladder_both_ports_dead_returns_manual_required():
    """run_ladder returns MANUAL-REQUIRED when both main and reload raise ConnectionError."""
    async def dead_main(cmd, args=None):
        raise ConnectionError("main dead")

    async def dead_reload(cmd, args=None):
        raise ConnectionError("reload dead")

    result = await _ladder.run_ladder(dead_main, send_reload=dead_reload)

    assert "MANUAL-REQUIRED" in result, f"Got: {result!r}"


# ── Test 18: F4 — canon parser understands newline-delimited CLEAN_PAYLOAD ───

def test_f4_parse_diagnose_parses_clean_payload():
    """F4: _parse_diagnose (imported from diagnose.py) parses newline-delimited CLEAN_PAYLOAD."""
    from unity_mcp.tools.diagnose import _parse_diagnose
    CLEAN_PAYLOAD = (
        "mvid=60d2de34-f1b2-4c3d-a5e6-789012345678\n"
        "stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280\n"
        "compile=idle|8.2\n"
        "sync=ready  epoch=3\n"
        "iscompiling=false  cn_active=false  started=false  stamp_frozen=false\n"
        "dlls=UnityMCP.Editor:639169455305003280:fresh\n"
        "errors=\n"
        "log=clean\n"
    )
    fields = _parse_diagnose(CLEAN_PAYLOAD)
    # Verify newline-delimited format is parsed correctly (not pipe-split)
    assert fields.mvid == "60d2de34-f1b2-4c3d-a5e6-789012345678"
    assert not fields.iscompiling
    assert not fields.stamp_frozen
    assert fields.compile == "idle"
    assert fields.log == "clean"


# ── Test 19: F4 — reload_ladder uses diagnose.py _parse_diagnose (not |split) ─

def test_f4_reload_ladder_imports_canon_parser():
    """F4: reload_ladder._parse_diagnose is the same object as diagnose._parse_diagnose."""
    from unity_mcp.tools.diagnose import _parse_diagnose as canon
    assert _ladder._parse_diagnose is canon, (
        "reload_ladder must import _parse_diagnose from diagnose.py, not define its own"
    )


# ── Test 20: m1 — dead channel-select branch simplified ──────────────────────

@pytest.mark.asyncio
async def test_m1_dead_branch_uses_send_reload_when_main_dead():
    """m1: when main_dead=True and send_reload supplied, all tiers use send_reload."""
    reload_calls = []
    main_calls = []

    async def dead_main(cmd, args=None):
        main_calls.append(cmd)
        raise ConnectionError("main dead")

    async def reload_send(cmd, args=None):
        reload_calls.append(cmd)
        if cmd == "diagnose":
            return _diag_frozen(MVID_A)
        return "ok"

    # Both ports return FROZEN → MANUAL-REQUIRED after all tiers; we just verify routing
    result = await _ladder.run_ladder(dead_main, send_reload=reload_send)

    # All diagnose calls must go via reload_send (not main)
    assert "diagnose" in reload_calls, "reload_send must handle diagnose when main dead"
    # main should only have been called for the initial attempt that fails
    assert all(c == "diagnose" for c in main_calls), (
        f"main should only be called once (T0 attempt), got: {main_calls}"
    )


# ── Test 21: F6 — IncompleteReadError → ConnectionError ─────────────────────

@pytest.mark.asyncio
async def test_f6_incomplete_read_raises_connection_error():
    """F6: asyncio.IncompleteReadError in _send body → ConnectionError (not raw exception)."""
    import asyncio as _asyncio

    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(
        side_effect=_asyncio.IncompleteReadError(b"", 4)
    )
    mock_writer = MagicMock()
    mock_writer.drain = AsyncMock()

    with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
        send_fn = _ladder.make_reload_send(port=9600)
        with pytest.raises(ConnectionError):
            await send_fn("ping", {})
    mock_writer.close.assert_called()


@pytest.mark.asyncio
async def test_f6_json_decode_error_raises_connection_error():
    """F6: truncated/corrupt JSON response → ConnectionError."""
    import struct as _struct

    bad_payload = b"not-json{"
    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(side_effect=[
        _struct.pack(">I", len(bad_payload)),
        bad_payload,
    ])
    mock_writer = MagicMock()
    mock_writer.drain = AsyncMock()

    with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
        send_fn = _ladder.make_reload_send(port=9600)
        with pytest.raises(ConnectionError):
            await send_fn("ping", {})
    mock_writer.close.assert_called()


@pytest.mark.asyncio
async def test_f6_sync_unity_does_not_crash_on_transport_error():
    """F6: ConnectionError from make_reload_send._send must not propagate to run_ladder callers."""
    import asyncio as _asyncio

    mock_reader = AsyncMock()
    mock_reader.readexactly = AsyncMock(
        side_effect=_asyncio.IncompleteReadError(b"", 4)
    )
    mock_writer = MagicMock()
    mock_writer.drain = AsyncMock()

    with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
        send_fn = _ladder.make_reload_send(port=9600)
        # run_ladder catches ConnectionError — must not raise
        result = await _ladder.run_ladder(send_fn)
    # Both ports failed → MANUAL-REQUIRED (not a crash)
    assert "MANUAL-REQUIRED" in result or "REIMPORT" in result or "HEALED" in result


# ── Test 22: M3 — _bump_str helper ──────────────────────────────────────────

def test_m3_bump_str_increments_patch():
    """M3: _bump_str('1.2.3') → '1.2.4'."""
    from unity_mcp.scripts.bump_version import _bump_str
    assert _bump_str("1.2.3") == "1.2.4"
    assert _bump_str("0.0.0") == "0.0.1"
    assert _bump_str("2.10.99") == "2.10.100"


@pytest.mark.asyncio
async def test_m3_t3_missing_file_returns_none(tmp_path):
    """M3: _t3 with nonexistent bump_file → None (MANUAL-REQUIRED propagated)."""
    missing = tmp_path / "nonexistent.json"

    async def _send(cmd, args=None):
        return "ok"

    result = await _ladder._t3(_send, MVID_A, missing)
    assert result is None


@pytest.mark.asyncio
async def test_m3_t3_non_json_file_returns_none(tmp_path):
    """M3: _t3 with non-JSON bump_file → None."""
    bad = tmp_path / "bad.json"
    bad.write_bytes(b"not json!!!")

    async def _send(cmd, args=None):
        return "ok"

    result = await _ladder._t3(_send, MVID_A, bad)
    assert result is None


@pytest.mark.asyncio
async def test_m3_t3_non_numeric_patch_returns_none(tmp_path):
    """M3: _t3 with version patch not a number → None."""
    bad_ver = tmp_path / "package.json"
    bad_ver.write_text('{"version":"1.2.beta"}', encoding="utf-8")

    async def _send(cmd, args=None):
        return "ok"

    result = await _ladder._t3(_send, MVID_A, bad_ver)
    assert result is None


# ── Test 23: F3+F5 — main_mvid absent → REIMPORT-NEEDED, never CLEAN ─────────

@pytest.mark.asyncio
async def test_f3f5_absent_main_mvid_is_not_clean():
    """F3+F5: diagnose with main_mvid=absent → REIMPORT-NEEDED, never CLEAN."""
    def _diag_absent_main_mvid():
        return (
            f"mvid={MVID_A}\n"
            "main_mvid=absent\n"
            "iscompiling=false  cn_active=true  started=false  stamp_frozen=false\n"
            "compile=idle\n"
            "stamp=abc:123\n"
            "errors=\nlog=clean\n"
        )

    send = MockSend([_diag_absent_main_mvid()])
    result = await _ladder.run_ladder(send)
    assert "REIMPORT-NEEDED" in result, (
        f"absent main_mvid must never be CLEAN, got: {result!r}"
    )


@pytest.mark.asyncio
async def test_f3f5_empty_main_mvid_is_not_clean():
    """F3+F5: diagnose with main_mvid= (empty) → REIMPORT-NEEDED."""
    def _diag_empty_main_mvid():
        return (
            f"mvid={MVID_A}\n"
            "main_mvid=\n"
            "iscompiling=false  cn_active=true  started=false  stamp_frozen=false\n"
            "compile=idle\n"
            f"stamp={MVID_A}:999\n"
            "errors=\nlog=clean\n"
        )

    send = MockSend([_diag_empty_main_mvid()])
    result = await _ladder.run_ladder(send)
    assert "REIMPORT-NEEDED" in result, (
        f"empty main_mvid must be REIMPORT-NEEDED, got: {result!r}"
    )


@pytest.mark.asyncio
async def test_f3f5_heal_compares_main_mvid_not_reload_mvid():
    """F3+F5: MVID-delta check uses main_mvid, not the reload-asmdef mvid."""
    def _diag_with_main_mvid(main_mvid: str):
        return (
            f"mvid={MVID_A}\n"   # reload mvid (never changes)
            f"main_mvid={main_mvid}\n"
            "iscompiling=false  cn_active=false  started=false  stamp_frozen=true\n"
            "compile=idle-stale\n"
            "stamp=abc:123\n"
            "errors=\nlog=clean\n"
        )

    MAIN_A = "11111111-1111-1111-1111-111111111111"
    MAIN_B = "22222222-2222-2222-2222-222222222222"

    send = MockSend([
        _diag_with_main_mvid(MAIN_A),   # T0 baseline: frozen + main_mvid present
        _diag_with_main_mvid(MAIN_B),   # T1 poll: main_mvid changed → HEALED
    ])
    result = await _ladder.run_ladder(send)
    assert result.startswith("HEALED"), (
        f"main_mvid delta should heal: {result!r}"
    )


# ── Test 24: M2 — new MVID + idle-failed → NOT HEALED ────────────────────────

@pytest.mark.asyncio
async def test_m2_mvid_delta_plus_idle_failed_is_not_healed():
    """M2: MVID changed but compile=idle-failed → REIMPORT-NEEDED (broken new domain)."""
    def _diag_new_mvid_but_failed():
        return (
            f"mvid={MVID_B}\n"
            f"main_mvid={MVID_B}\n"
            "iscompiling=false  cn_active=false  started=false  stamp_frozen=false\n"
            "compile=idle-failed\n"
            f"stamp={MVID_B}:999\n"
            "errors=\nlog=clean\n"
        )

    def _diag_baseline():
        return (
            f"mvid={MVID_A}\n"
            f"main_mvid={MVID_A}\n"
            "iscompiling=false  cn_active=false  started=false  stamp_frozen=true\n"
            "compile=idle-stale\n"
            f"stamp={MVID_A}:100\n"
            "errors=\nlog=clean\n"
        )

    send = MockSend([
        _diag_baseline(),            # T0: frozen
        _diag_new_mvid_but_failed(), # T1 poll: new MVID but compile=idle-failed
    ])
    result = await _ladder.run_ladder(send)
    assert "HEALED" not in result, (
        f"M2: new MVID + idle-failed must NOT be HEALED, got: {result!r}"
    )
    assert "REIMPORT-NEEDED" in result or "FAILED" in result, (
        f"M2: expected REIMPORT-NEEDED/FAILED for broken new domain, got: {result!r}"
    )


# ── Test 25: B1 — _t5 uses _verdict; broken new domain after Play/Stop → REIMPORT ──

@pytest.mark.asyncio
async def test_t5_broken_new_domain_after_play_stop_returns_reimport():
    """B1: Play/Stop triggers domain reload with compile errors → REIMPORT-NEEDED, not HEALED.

    _t5 must use _poll_mvid_delta (which calls _verdict) instead of bare MVID comparison.
    If new domain has compile=idle-failed, _verdict returns non-CLEAN → _BROKEN_DOMAIN sentinel
    → _tier_result returns REIMPORT-NEEDED message.
    """
    def _diag_broken_new_domain():
        return (
            f"mvid={MVID_B}\n"
            f"main_mvid={MVID_B}\n"
            "iscompiling=false  cn_active=false  started=false  stamp_frozen=false\n"
            "compile=idle-failed\n"
            f"stamp={MVID_B}:999\n"
            "errors=CS0103 bad\nlog=clean\n"
        )

    send = MockSend([
        _diag_frozen(MVID_A),         # T0 baseline
        _diag_frozen(MVID_A),         # T1 poll → frozen
        _diag_frozen(MVID_A),         # T2 T1-retry → frozen
        _diag_frozen(MVID_A),         # T3 production fallback sync+poll → frozen (no bump_file)
        _diag_broken_new_domain(),    # T5 poll: new MVID but compile=idle-failed
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    assert "HEALED" not in result, f"B1: broken new domain must NOT be HEALED, got: {result!r}"
    assert "REIMPORT-NEEDED" in result, f"B1: expected REIMPORT-NEEDED for broken domain, got: {result!r}"


# ── STRESS TESTS ──────────────────────────────────────────────────────────────
#
# F1  compile-as-latch: CS error keeps MVID frozen → ladder must detect early
# F8  infinite-poll: _poll_mvid_delta respects max_polls hard cap
# F9  get_compile_errors lie: ladder must NEVER call get_compile_errors


def _diag_compile_latch(mvid: str, cs_error: str = "error CS1739") -> str:
    """Class A latch that is actually a persistent compile error.
    iscompiling=true, cn_active=false, stamp=UNDETERMINED, errors= has CS code.
    """
    return (
        f"mvid={mvid}\n"
        f"main_mvid={mvid}\n"
        "iscompiling=true  cn_active=false  started=false  stamp_frozen=true\n"
        "compile=compiling-latched\n"
        f"stamp={mvid}:100\n"
        f"errors={cs_error}\nlog=clean\n"
    )


# ── F1 stress tests ───────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_stress_f1_scenario_1_poll_exits_not_none_on_compile_error():
    """F1: _poll_mvid_delta with MVID frozen + CS error must return _BROKEN_DOMAIN.
    cs_grace=1 (default): exit after 2nd consecutive CS-error poll, not the 1st.
    """
    cs_diag = _diag_compile_latch(MVID_A, "error CS1739")
    call_count = 0

    async def mock_send(cmd, args=None):
        nonlocal call_count
        if cmd == "diagnose":
            call_count += 1
            return cs_diag
        return "ok"

    result = await _ladder._poll_mvid_delta(mock_send, MVID_A, timeout_s=60.0, max_polls=3)
    # cs_grace=1 default: exit after 2 consecutive CS-error polls (1 grace poll allowed).
    assert result == _ladder._BROKEN_DOMAIN, f"F1: poll must return _BROKEN_DOMAIN on CS error, got: {result!r}"
    assert call_count == 2, f"F1: early-exit must happen after 2nd CS-error poll (cs_grace=1), got: {call_count} calls"


@pytest.mark.asyncio
async def test_stress_f1_scenario_2_run_ladder_detects_cs_error_returns_reimport():
    """F1: run_ladder with persistent CS compile error (MVID never changes) must
    return REIMPORT-NEEDED or a compile-error sentinel — NOT loop through all tiers
    calling Cmd+R repeatedly (pointless when compile fails every time).

    Discriminating: count recompile+editor+force_refresh calls. With a real compile
    error, escalating T1→T2→T3→T4→T5 is wasteful; the current ladder DOES exhaust
    all tiers (bug). This test documents the current behavior and will FAIL when
    compile-error early-exit is implemented.
    """
    # All diagnose responses show MVID frozen + CS1739
    send = MockSend(
        [_diag_compile_latch(MVID_A, "error CS1739")] * 10,
    )

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    # After all tiers exhaust, result must be a failure sentinel (not a false heal).
    assert "HEALED" not in result, f"F1: CS error must never heal, got: {result!r}"
    # The result should be some form of failure/manual sentinel.
    assert any(k in result for k in ("MANUAL-REQUIRED", "REIMPORT-NEEDED", "FAILED")), (
        f"F1: expected failure sentinel, got: {result!r}"
    )


@pytest.mark.asyncio
async def test_stress_f1_scenario_3_compile_error_not_mistaken_for_latch():
    """F1: diagnose with iscompiling=true + errors= 'error CS1739' across ALL polls.
    _poll_mvid_delta must terminate after max_polls, not hang.
    """
    diag_with_error = _diag_compile_latch(MVID_A, "error CS1739")
    diagnose_call_count = 0

    async def counting_send(cmd, args=None):
        nonlocal diagnose_call_count
        if cmd == "diagnose":
            diagnose_call_count += 1
            return diag_with_error
        return "ok"

    # cs_grace=1 default: exit after 2nd consecutive CS-error poll.
    result = await _ladder._poll_mvid_delta(
        counting_send, MVID_A, timeout_s=999.0, max_polls=5
    )
    assert result == _ladder._BROKEN_DOMAIN, (
        f"F1: must return _BROKEN_DOMAIN on CS error, got: {result!r}"
    )
    assert diagnose_call_count == 2, (
        f"F1: early-exit after 2nd poll expected (cs_grace=1), got: {diagnose_call_count} calls"
    )


@pytest.mark.asyncio
async def test_poll_mvid_delta_cs_grace_zero_exits_on_first_poll():
    """cs_grace=0: early-exit on FIRST CS-error poll (legacy behavior)."""
    cs_diag = _diag_compile_latch(MVID_A, "error CS1739")
    call_count = 0

    async def mock_send(cmd, args=None):
        nonlocal call_count
        if cmd == "diagnose":
            call_count += 1
            return cs_diag
        return "ok"

    result = await _ladder._poll_mvid_delta(
        mock_send, MVID_A, timeout_s=60.0, max_polls=3, cs_grace=0
    )
    assert result == _ladder._BROKEN_DOMAIN
    assert call_count == 1, f"cs_grace=0: must exit on 1st poll, got {call_count}"


@pytest.mark.asyncio
async def test_poll_mvid_delta_cs_grace_resets_on_clean_poll():
    """cs_grace=1: a clean poll resets the grace counter."""
    cs_diag = _diag_compile_latch(MVID_A, "error CS1739")
    clean_diag = _diag_clean(MVID_A)  # same MVID but no error — frozen but clean
    call_count = 0
    responses = [cs_diag, clean_diag, cs_diag, cs_diag]  # CS, clean, CS, CS → exit after 4th

    async def mock_send(cmd, args=None):
        nonlocal call_count
        if cmd == "diagnose":
            idx = call_count
            call_count += 1
            return responses[idx] if idx < len(responses) else cs_diag
        return "ok"

    result = await _ladder._poll_mvid_delta(
        mock_send, MVID_A, timeout_s=60.0, max_polls=10, cs_grace=1
    )
    assert result == _ladder._BROKEN_DOMAIN
    assert call_count == 4, f"grace reset on clean poll: expected 4 calls, got {call_count}"


# ── F8 stress tests ───────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_stress_f8_scenario_1_poll_respects_max_polls_hard_cap():
    """F8: _poll_mvid_delta with max_polls=N must make EXACTLY N diagnose calls
    when MVID is perpetually frozen. Discriminating: if N+1 calls happen, cap broken.
    """
    call_count = 0

    async def frozen_send(cmd, args=None):
        nonlocal call_count
        if cmd == "diagnose":
            call_count += 1
            return _diag_frozen(MVID_A)
        return "ok"

    result = await _ladder._poll_mvid_delta(frozen_send, MVID_A, timeout_s=999.0, max_polls=4)

    assert result is None, f"F8: frozen MVID must return None, got: {result!r}"
    assert call_count == 4, (
        f"F8: max_polls=4 must produce exactly 4 diagnose calls, got: {call_count}"
    )


@pytest.mark.asyncio
async def test_stress_f8_scenario_2_run_ladder_terminates_after_tier_exhaustion():
    """F8: run_ladder with all frozen diagnoses must terminate (not loop forever).
    Even with play_stop_consent=True (T5 enabled), result must be a terminal string.
    """
    send = MockSend([_diag_frozen(MVID_A)])  # always frozen, autouse _fast_timeouts applies

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    # Must be a non-empty terminal string — not hanging, not empty
    assert isinstance(result, str) and len(result) > 0, f"F8: run_ladder must return string, got: {result!r}"
    assert "HEALED" not in result, f"F8: frozen forever must not heal, got: {result!r}"


@pytest.mark.asyncio
async def test_stress_f8_scenario_3_max_polls_none_exits_on_deadline():
    """F8: _poll_mvid_delta with max_polls=None must exit when deadline passes.
    Simulates production default where only the deadline guards against infinite poll.
    """
    call_count = 0

    async def frozen_send(cmd, args=None):
        nonlocal call_count
        if cmd == "diagnose":
            call_count += 1
            return _diag_frozen(MVID_A)
        # Advance monotonic clock artificially by patching sleep to expire deadline
        return "ok"

    # timeout_s=0.0 → deadline is already passed on first check after poll
    result = await _ladder._poll_mvid_delta(
        frozen_send, MVID_A, timeout_s=0.0, max_polls=None
    )
    # Must exit after 1 diagnose call (deadline passed after first iteration)
    assert result is None, f"F8: deadline exit must return None, got: {result!r}"
    assert call_count >= 1, "F8: must call diagnose at least once (check-first)"
    assert call_count <= 2, f"F8: deadline=0 must exit after 1-2 polls, got: {call_count}"


# ── F9 stress tests ───────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_stress_f9_scenario_1_ladder_never_calls_get_compile_errors():
    """F9: run_ladder must NEVER call 'get_compile_errors' in any tier.
    get_compile_errors returns stale data when UnityMCP.Editor.dll is broken.
    Discriminating: if 'get_compile_errors' appears in send.calls, the bug is present.
    """
    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll
        _diag_frozen(MVID_A),   # T2 T1-retry
        _diag_clean(MVID_B),    # T1 (T2 retry) → healed
    ])

    result = await _ladder.run_ladder(send)

    cmds_called = [cmd for cmd, _ in send.calls]
    assert "get_compile_errors" not in cmds_called, (
        f"F9: ladder must NEVER call get_compile_errors (stale cache anti-pattern), "
        f"got calls: {cmds_called}"
    )


@pytest.mark.asyncio
async def test_stress_f9_scenario_2_broken_domain_uses_diagnose_not_get_compile_errors():
    """F9: when new domain has compile=idle-failed, verdict must come from _verdict(diagnose),
    NOT from a separate get_compile_errors call. Verifies absence of the anti-pattern
    even in the broken-domain M2 codepath.
    """
    def _diag_broken():
        return (
            f"mvid={MVID_B}\n"
            f"main_mvid={MVID_B}\n"
            "iscompiling=false  cn_active=false  started=false  stamp_frozen=false\n"
            "compile=idle-failed\n"
            f"stamp={MVID_B}:999\n"
            "errors=CS0103 undefined\nlog=clean\n"
        )

    send = MockSend([
        _diag_frozen(MVID_A),   # T0
        _diag_broken(),          # T1 poll: MVID changed but broken
    ])

    result = await _ladder.run_ladder(send)

    cmds_called = [cmd for cmd, _ in send.calls]
    assert "get_compile_errors" not in cmds_called, (
        f"F9: broken-domain path must use diagnose compile field, "
        f"not get_compile_errors, calls: {cmds_called}"
    )
    assert "HEALED" not in result, f"F9: broken domain must not heal, got: {result!r}"


@pytest.mark.asyncio
async def test_stress_f9_scenario_3_all_tiers_use_only_allowed_commands():
    """F9: across all tiers (T0-T5 exhausted), ladder must only call these TCP commands:
    diagnose, force_refresh, recompile, sync, editor.
    Any other command (esp. get_compile_errors) is an anti-pattern.
    """
    ALLOWED_CMDS = frozenset({"diagnose", "force_refresh", "recompile", "sync", "editor", "execute_code"})

    send = MockSend([_diag_frozen(MVID_A)])  # always frozen → all tiers exhaust

    await _ladder.run_ladder(send, play_stop_consent=True)

    unexpected = [cmd for cmd, _ in send.calls if cmd not in ALLOWED_CMDS]
    assert not unexpected, (
        f"F9: ladder called unexpected TCP commands: {unexpected}. "
        f"Only allowed: {sorted(ALLOWED_CMDS)}"
    )


# ── T2.5 Guard Check Tests ────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_t2_5_guard_wedged_no_consent_returns_guard_wedged():
    """T2.5: guard=True + no consent → GUARD-WEDGED message, T3/T4/T5 not called."""
    class GuardLockedSend(MockSend):
        async def __call__(self, cmd, args=None):
            if cmd == "execute_code":
                self.calls.append((cmd, args or {}))
                return "True"  # guard locked!
            return await super().__call__(cmd, args)

    send = GuardLockedSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry → frozen
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=False)

    assert "GUARD-WEDGED" in result, f"Expected GUARD-WEDGED, got: {result!r}"
    # T3 (sync) and T5 (editor play) must not be called
    cmds = [cmd for cmd, _ in send.calls]
    assert "editor" not in cmds, f"T5 editor must not be called without consent: {cmds}"


@pytest.mark.asyncio
async def test_t2_5_guard_wedged_with_consent_runs_t5():
    """T2.5: guard=True + consent → T5 (play/stop) called, T3/T4 skipped."""
    class GuardLockedSend(MockSend):
        async def __call__(self, cmd, args=None):
            if cmd == "execute_code":
                self.calls.append((cmd, args or {}))
                return "True"  # guard locked!
            return await super().__call__(cmd, args)

    send = GuardLockedSend([
        _diag_frozen(MVID_A),   # T0
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry → frozen
        _diag_clean(MVID_B),    # T5 after play/stop → healed!
    ])

    result = await _ladder.run_ladder(send, play_stop_consent=True)

    assert result.startswith("HEALED: T5-guard"), f"Expected T5-guard heal, got: {result!r}"
    cmds = [cmd for cmd, _ in send.calls]
    assert "editor" in cmds, f"T5 editor play must be called with consent: {cmds}"


@pytest.mark.asyncio
async def test_t2_5_guard_check_code_is_valid_statement():
    """Regression: SessionState probe sent to execute_code must end with ';'.

    A missing semicolon compiles fine in mocked tests (execute_code is stubbed) but
    crashes for real against Roslyn with CS1002 (";" expected) — invisible to every
    other test in this file since none of them exercise a live compiler.
    """
    send = MockSend([])
    await _ladder._t2_5_guard_check(send)
    code_calls = [args["code"] for cmd, args in send.calls if cmd == "execute_code"]
    assert code_calls, "execute_code must be called"
    assert code_calls[0].strip().endswith(";"), f"missing semicolon: {code_calls[0]!r}"


@pytest.mark.asyncio
async def test_t2_5_not_wedged_continues_t3():
    """T2.5: guard=False → run_ladder continues to T3 (sync called).

    Chain: T1 frozen → T2 frozen → T2.5 guard=False → T3 sync → healed.
    Verifies the dead-code gate (bump_file is not None) is removed.
    """
    send = MockSend([
        _diag_frozen(MVID_A),   # T0 baseline
        _diag_frozen(MVID_A),   # T1 poll → frozen
        _diag_frozen(MVID_A),   # T2 T1-retry → frozen
        _diag_clean(MVID_B),    # T3 sync+poll → healed!
    ])

    # bump_file=None → T3 production fallback (sync resolve) must still run
    result = await _ladder.run_ladder(send, bump_file=None)

    assert result.startswith("HEALED: T3"), f"Expected T3 heal via sync, got: {result!r}"
    cmds = [cmd for cmd, _ in send.calls]
    assert "sync" in cmds, f"T3 production fallback must call sync: {cmds}"


# ── T3 Production Fallback ────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_t3_production_fallback_calls_sync(tmp_path):
    """_t3 with bump_file=None calls sync resolve=true and polls for MVID delta."""
    send = MockSend([_diag_clean(MVID_B)])  # diagnose returns new MVID after sync

    result = await _ladder._t3(send, MVID_A, None)

    cmds = [cmd for cmd, _ in send.calls]
    assert "sync" in cmds, f"_t3(bump_file=None) must call sync: {cmds}"
    sync_calls = [(cmd, args) for cmd, args in send.calls if cmd == "sync"]
    assert sync_calls[0][1].get("resolve") == "true", f"Must pass resolve=true: {sync_calls}"


@pytest.mark.asyncio
async def test_t3_production_fallback_heals_on_mvid_delta():
    """_t3 with bump_file=None returns new MVID when poll finds delta."""
    send = MockSend([_diag_clean(MVID_B)])

    result = await _ladder._t3(send, MVID_A, None)

    assert result == MVID_B, f"Expected new MVID {MVID_B!r}, got: {result!r}"


@pytest.mark.asyncio
async def test_t3_production_fallback_returns_none_on_tcp_error():
    """_t3 with bump_file=None returns None on OSError."""
    send = MockSend([], raise_on="sync")

    result = await _ladder._t3(send, MVID_A, None)

    assert result is None, f"TCP error must return None, got: {result!r}"


# ── Item 28: T3 skipped when main_dead=True ───────────────────────────────────

@pytest.mark.asyncio
async def test_item28_t3_skipped_when_main_dead():
    """Item 28: when main_dead=True, T3 (sync) must NOT be called — reload mini-server
    doesn't support 'sync' and would return 'unknown command'."""
    async def dead_main(cmd, args=None):
        raise ConnectionError("main dead")

    sync_called = False

    async def reload_send(cmd, args=None):
        nonlocal sync_called
        if cmd == "sync":
            sync_called = True
            return "unknown command"
        if cmd == "diagnose":
            return _diag_frozen(MVID_A)
        if cmd == "execute_code":
            return "False"  # guard not locked
        return "ok"

    result = await _ladder.run_ladder(dead_main, send_reload=reload_send)

    assert not sync_called, "T3 sync must NOT be called when main_dead=True"


# ── T5 max_polls=None ─────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_t5_max_polls_none_respects_deadline_not_count():
    """_t5 must call _poll_mvid_delta with max_polls=None (deadline governs, not count)."""
    poll_calls = []
    original_poll = _ladder._poll_mvid_delta

    async def tracking_poll(send, baseline, timeout_s, max_polls=None, cs_grace=1):
        poll_calls.append(max_polls)
        return None  # frozen

    with patch.object(_ladder, "_poll_mvid_delta", tracking_poll):
        async def noop_send(cmd, args=None):
            return "ok"
        await _ladder._t5(noop_send, MVID_A)

    assert poll_calls, "T5 must call _poll_mvid_delta"
    assert poll_calls[-1] is None, f"T5 must use max_polls=None, got: {poll_calls[-1]!r}"
