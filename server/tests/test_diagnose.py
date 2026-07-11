"""Tests for tools/diagnose.py — Python wrapper for the C# 'diagnose' command.

All tests use mocked _send (pure Python, no Unity required).
Covers: verdict classifier, wire-format parser, connection error, tool registration.

CANON wire-format is from DiagnoseCommand.cs:
  compile=<CompileNotifier.GetStatus()>  → e.g. "idle|3.2", "idle-failed|8.1", "idle-never|0.0"
  sync=<state>  <epoch=N>               → e.g. "ready  epoch=3" (double space, ExtractSyncSummary)
  iscompiling=false  cn_active=false  started=false  stamp_frozen=false  (double spaces)
  dlls=<name>:<ticks>:fresh,<name>:0:unknown
  errors=<text or empty>
  log=clean|absent|CS####
"""
import pytest
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.diagnose as _d


# ---------------------------------------------------------------------------
# Fixtures — REAL pipe format from DiagnoseCommand.cs
# ---------------------------------------------------------------------------

CLEAN_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
"""

FAILED_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|4.1
sync=failed  epoch=2
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=Assets/Editor/Foo.cs(12,5): error CS0117: 'MyClass' does not contain a definition
log=CS0117
"""

# prev_mvid=aaaaaaaa... → STALE-DOMAIN (MVID unchanged after intended recompile)
STALE_DOMAIN_PAYLOAD = """\
mvid=aaaaaaaa-0000-0000-0000-000000000000
stamp=aaaaaaaa-0000-0000-0000-000000000000:100
compile=idle|3.0
sync=ready  epoch=1
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:100:fresh
errors=
log=clean
"""

WEDGE_ENGINE_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=compiling|0.0
sync=compiling  epoch=1
iscompiling=true  cn_active=false  started=true  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=absent
"""

# sync=compiling but compile=idle|5.0 → WEDGE-STATE
WEDGE_STATE_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|5.0
sync=compiling  epoch=1
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
"""

NOOP_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-never|0.0
sync=ready  epoch=0
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:0:unknown
errors=
log=absent
"""

# idle-failed + empty errors + log evidence + same prev_mvid → must be FAILED (not shadowed by STALE-DOMAIN)
FAILED_EMPTY_ERRORS_PAYLOAD = """\
mvid=bbbbbbbb-0000-0000-0000-000000000000
stamp=bbbbbbbb-0000-0000-0000-000000000000:200
compile=idle-failed|6.0
sync=ready  epoch=2
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:200:fresh
errors=
log=CS9999
"""


@pytest.fixture(autouse=True)
def _reset_send():
    original = _d._send
    yield
    _d._send = original


def _make_send(payload: str):
    async def _send(cmd, args=None, **kwargs):
        if cmd == "diagnose":
            return payload
        raise AssertionError(f"Unexpected cmd: {cmd}")
    return _send


# ---------------------------------------------------------------------------
# Verdict tests
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_diagnose_clean_live_verdict():
    """All signals green → CLEAN-LIVE."""
    _d._send = _make_send(CLEAN_PAYLOAD)
    result = await _d.diagnose()
    assert result == "CLEAN-LIVE"


@pytest.mark.asyncio
async def test_diagnose_failed_cs_verdict():
    """idle-failed + error CS0117 → FAILED:CS0117."""
    _d._send = _make_send(FAILED_PAYLOAD)
    result = await _d.diagnose()
    assert result.startswith("FAILED:CS0117"), f"Expected FAILED:CS0117, got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_stale_domain_when_prev_mvid_supplied():
    """prev_mvid supplied + unchanged MVID → STALE-DOMAIN (not NO-OP)."""
    _d._send = _make_send(STALE_DOMAIN_PAYLOAD)
    result = await _d.diagnose(prev_mvid="aaaaaaaa-0000-0000-0000-000000000000")
    assert result == "STALE-DOMAIN", f"Unchanged MVID with prev_mvid → STALE-DOMAIN, got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_noop_when_no_prev_mvid_supplied():
    """No prev_mvid + unchanged MVID → NO-OP (stateless probe, can't detect stale)."""
    _d._send = _make_send(STALE_DOMAIN_PAYLOAD)
    result = await _d.diagnose()  # no prev_mvid
    assert result == "CLEAN-LIVE", f"No prev_mvid → CLEAN-LIVE (not STALE-DOMAIN), got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_wedge_engine_verdict():
    """iscompiling=true + cn_active=false + stamp_frozen=true → WEDGE-ENGINE."""
    _d._send = _make_send(WEDGE_ENGINE_PAYLOAD)
    result = await _d.diagnose()
    assert result == "WEDGE-ENGINE", f"Expected WEDGE-ENGINE, got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_wedge_state_verdict():
    """sync=compiling + compile=idle|5.0 → WEDGE-STATE (pipe format tested)."""
    _d._send = _make_send(WEDGE_STATE_PAYLOAD)
    result = await _d.diagnose()
    assert result == "WEDGE-STATE", f"Expected WEDGE-STATE, got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_noop_verdict_idle_never():
    """compile=idle-never|0.0 → NO-OP (never compiled this session)."""
    _d._send = _make_send(NOOP_PAYLOAD)
    result = await _d.diagnose()
    assert result == "NO-OP", f"Expected NO-OP for idle-never, got: {result!r}"


@pytest.mark.asyncio
async def test_diagnose_unknown_on_connection_error():
    """ConnectionError → UNKNOWN (can't read signal → never 'clean')."""
    async def _failing_send(cmd, args=None, **kwargs):
        raise ConnectionError("Unity unreachable")
    _d._send = _failing_send
    result = await _d.diagnose()
    assert result == "UNKNOWN"


@pytest.mark.asyncio
async def test_diagnose_failed_not_shadowed_by_stale_domain():
    """idle-failed + empty errors + matching prev_mvid → FAILED (idle-failed checked before MVID match)."""
    _d._send = _make_send(FAILED_EMPTY_ERRORS_PAYLOAD)
    result = await _d.diagnose(prev_mvid="bbbbbbbb-0000-0000-0000-000000000000")
    assert result == "FAILED:unknown", f"idle-failed must not be shadowed by STALE-DOMAIN, got: {result!r}"


# ---------------------------------------------------------------------------
# Proof: OLD broken parser would return wrong verdict on WEDGE-STATE
# ---------------------------------------------------------------------------

def test_wedge_state_old_broken_parser_would_fail():
    """Prove the pipe format is the canon — old 'compile == idle' would miss WEDGE-STATE.

    The old parser did fields.compile == "idle" which fails on "idle|5.0".
    With the real pipe format fixture, _parse_diagnose must return compile_state="idle"
    (after pipe-split), and _verdict must return WEDGE-STATE — not UNKNOWN/CLEAN-LIVE.
    """
    f = _d._parse_diagnose(WEDGE_STATE_PAYLOAD)
    # Parser must strip the pipe suffix
    assert f.compile == "idle", f"Parser must split on pipe: got {f.compile!r}"
    assert f.sync_state == "compiling"
    # Verdict must detect the wedge
    v = _d._verdict(f)
    assert v == "WEDGE-STATE", f"Expected WEDGE-STATE, got {v!r}"


# ---------------------------------------------------------------------------
# Wire-format parser unit tests
# ---------------------------------------------------------------------------

def test_parse_clean_payload():
    f = _d._parse_diagnose(CLEAN_PAYLOAD)
    assert f.mvid == "60d2de34-f1b2-4c3d-a5e6-789012345678"
    assert f.compile == "idle"   # pipe stripped
    assert f.sync_state == "ready"
    assert f.iscompiling is False
    assert f.log == "clean"
    assert f.errors == ""


def test_parse_failed_payload():
    f = _d._parse_diagnose(FAILED_PAYLOAD)
    assert f.compile == "idle-failed"  # pipe stripped
    assert "CS0117" in f.errors


def test_parse_noop_payload_pipe():
    """compile=idle-never|0.0 → compile field is 'idle-never' after pipe split."""
    f = _d._parse_diagnose(NOOP_PAYLOAD)
    assert f.compile == "idle-never"


def test_parse_wedge_engine_fields():
    f = _d._parse_diagnose(WEDGE_ENGINE_PAYLOAD)
    assert f.iscompiling is True
    assert f.cn_active is False
    assert f.stamp_frozen is True


def test_parse_undetermined_stamp():
    payload = "mvid=UNDETERMINED\nstamp=UNDETERMINED\ncompile=idle|0.0\n"
    f = _d._parse_diagnose(payload)
    assert f.stamp == "UNDETERMINED"
    assert _d._verdict(f) == "UNKNOWN"


# ---------------------------------------------------------------------------
# _first_cs unit tests
# ---------------------------------------------------------------------------

def test_first_cs_extracts_code():
    assert _d._first_cs("error CS0117: foo") == "CS0117"
    assert _d._first_cs("error CS0535: interface not implemented") == "CS0535"


def test_first_cs_returns_empty_when_not_found():
    assert _d._first_cs("no error here") == ""


# ---------------------------------------------------------------------------
# GROUP 2 — RED tests (must FAIL before implementation)
# ---------------------------------------------------------------------------

# ------ P16 / G10 : dlls= field wired into _verdict ------

# Wire: Tests dll missing → TESTS-INVISIBLE
# Red-precondition: _DiagnoseFields has no `dlls` field and _verdict ignores dlls=
# → today returns CLEAN-LIVE for this wire.
TESTS_MISSING_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh,UnityMCP.Editor.Chat.Tests:0:unknown(missing)
errors=
log=clean
"""

def test_tests_dll_missing_yields_tests_invisible():
    """dlls= has Tests:unknown(missing) → TESTS-INVISIBLE.

    Red-precondition: _verdict ignores dlls= field → returns CLEAN-LIVE today.
    """
    f = _d._parse_diagnose(TESTS_MISSING_WIRE)
    v = _d._verdict(f)
    assert v == "TESTS-INVISIBLE", f"Tests missing should be TESTS-INVISIBLE, got {v!r}"


# Wire: ALL dlls missing → REBUILDING
# Red-precondition: same — _verdict ignores dlls= → returns UNKNOWN/CLEAN-LIVE.
ALL_MISSING_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:0:unknown(missing),UnityMCP.Editor.Chat.Tests:0:unknown(missing)
errors=
log=clean
"""

def test_all_dlls_missing_yields_rebuilding():
    """dlls= has ALL tokens unknown(missing) → REBUILDING.

    Red-precondition: _verdict ignores dlls= → returns CLEAN-LIVE today.
    """
    f = _d._parse_diagnose(ALL_MISSING_WIRE)
    v = _d._verdict(f)
    assert v == "REBUILDING", f"All dlls missing → REBUILDING, got {v!r}"


# Wire: prod dll stale → NOT CLEAN-LIVE
# Red-precondition: _verdict ignores dlls= → CLEAN-LIVE today.
STALE_DLL_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:100:stale
errors=
log=clean
"""

def test_stale_prod_dll_yields_failed_stale():
    """dlls= has prod dll :stale → FAILED:stale-dll (not CLEAN-LIVE).

    Red-precondition: _verdict ignores dlls= → returns CLEAN-LIVE today.
    """
    f = _d._parse_diagnose(STALE_DLL_WIRE)
    v = _d._verdict(f)
    assert v == "FAILED:stale-dll", f"Stale prod dll → FAILED:stale-dll, got {v!r}"


# Bug 2: idle-never + stale-dll must NOT be masked by NO-OP (priority inversion fix)
IDLE_NEVER_STALE_DLL_WIRE = """\
mvid=
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-never|0.0
sync=ready  epoch=1
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:100:stale
errors=
log=clean
"""

def test_idle_never_with_stale_dll_yields_failed_not_noop():
    """idle-never + stale dll → FAILED:stale-dll, not NO-OP.

    Bug 2 fix: stale-dll check must precede idle-never to avoid masking.
    """
    f = _d._parse_diagnose(IDLE_NEVER_STALE_DLL_WIRE)
    v = _d._verdict(f)
    assert v == "FAILED:stale-dll", f"idle-never + stale-dll must be FAILED:stale-dll, got {v!r}"


# Bug 3: iscompiling + idle-never + stale-dlls = package-resolve transient → STALE-TRANSIENT
STALE_TRANSIENT_WIRE = """\
mvid=
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-never|0.0
sync=ready  epoch=1
iscompiling=true  cn_active=true  started=true  stamp_frozen=false
dlls=UnityMCP.Editor:100:stale
errors=
log=clean
"""

def test_iscompiling_idle_never_stale_dll_yields_stale_transient():
    """iscompiling=true + idle-never + stale dll → STALE-TRANSIENT (package-resolve transient).

    Bug 3 fix: transient state must be distinguishable from permanent stale.
    """
    f = _d._parse_diagnose(STALE_TRANSIENT_WIRE)
    v = _d._verdict(f)
    assert v == "STALE-TRANSIENT", f"iscompiling+idle-never+stale → STALE-TRANSIENT, got {v!r}"


# ------ A5/G27 : STALE-DOMAIN gated on expected_compile ------

# Red-precondition: _verdict has no expected_compile param → slot 11 fires STALE-DOMAIN
# on ANY frozen MVID with prev_mvid, even when no compile was expected (cache-hit).
def test_mvid_frozen_cachehit_is_clean_not_stale_domain():
    """Frozen MVID + expected_compile=False → NO-OP/CLEAN-LIVE, never STALE-DOMAIN.

    Red-precondition: _verdict has no expected_compile param; without the gate
    it returns STALE-DOMAIN on any prev_mvid == mvid, even a Bee cache-hit.
    """
    f = _d._parse_diagnose(STALE_DOMAIN_PAYLOAD)  # idle, no errors, frozen mvid
    v = _d._verdict(f, prev_mvid="aaaaaaaa-0000-0000-0000-000000000000",
                    expected_compile=False)
    assert v != "STALE-DOMAIN", f"Cache-hit (no compile expected) must NOT be STALE-DOMAIN, got {v!r}"
    assert v in ("CLEAN-LIVE", "NO-OP"), f"Cache-hit should be CLEAN-LIVE or NO-OP, got {v!r}"


def test_mvid_frozen_with_expected_compile_is_stale_domain():
    """Frozen MVID + expected_compile=True → STALE-DOMAIN (compile WAS expected).

    Red-precondition: _verdict has no expected_compile param; passes vacuously.
    This tests that the EXISTING behavior is PRESERVED when expected_compile=True.
    """
    f = _d._parse_diagnose(STALE_DOMAIN_PAYLOAD)
    v = _d._verdict(f, prev_mvid="aaaaaaaa-0000-0000-0000-000000000000",
                    expected_compile=True)
    assert v == "STALE-DOMAIN", f"Frozen MVID + compile expected → STALE-DOMAIN, got {v!r}"


# ------ G1/BUILD-FAILED-WEDGE : wedge param in _verdict, slot 3 ------

# Red-precondition: _verdict has no `wedge` param and no slot 3.
# Feeding a WedgeReport with kind='build-failed-wedge' + iscompiling=True
# must yield BUILD-FAILED-WEDGE — today it yields WEDGE-ENGINE.
WEDGE_ENGINE_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=compiling|0.0
sync=compiling  epoch=1
iscompiling=true  cn_active=false  started=true  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=absent
"""

def test_build_failed_wedge_slot_fires_before_wedge_engine():
    """WedgeReport(kind='build-failed-wedge') + iscompiling → BUILD-FAILED-WEDGE, not WEDGE-ENGINE.

    Red-precondition: _verdict has no `wedge` param; slot 3 absent; returns WEDGE-ENGINE today.
    The spec mandates BUILD-FAILED-WEDGE intercepts before WEDGE-ENGINE to give the
    correct remedy (reimport, not restart).
    """
    from unity_mcp.editor_log import WedgeReport
    wedge = WedgeReport(kind="build-failed-wedge", cs_errors=["CS0535: foo"])
    f = _d._parse_diagnose(WEDGE_ENGINE_WIRE)
    v = _d._verdict(f, wedge=wedge)
    assert v.startswith("BUILD-FAILED-WEDGE"), f"Expected BUILD-FAILED-WEDGE, got {v!r}"
    # A9: must not suggest killing/relaunching Unity — "do NOT restart" in the message is fine
    # Negative-lookahead: "do NOT restart" is fine; bare "restart" is not.
    import re
    assert not re.search(r'(?<!NOT )[Rr]estart', v), \
        f"BUILD-FAILED-WEDGE must not suggest restart (got: {v!r})"


# Wire: guard_rejected signal — Unity busy text instead of normal wire format
# Red-precondition: guard_rejected field absent from _DiagnoseFields;
# parse defaults → empty stamp → UNKNOWN today.
GUARD_REJECT_WIRE = "Unity is compiling. Retry in 5s."
GUARD_REJECT_FINGERPRINT = """\
iscompiling=true  cn_active=false  started=true  stamp_frozen=true
stamp=UNDETERMINED
"""

def test_guard_reject_field_is_parsed():
    """'Unity is compiling. Retry in 5s.' wire → guard_rejected=True.

    Red-precondition: _DiagnoseFields has no guard_rejected; field absent today.
    """
    f = _d._parse_diagnose(GUARD_REJECT_WIRE)
    assert hasattr(f, "guard_rejected"), "_DiagnoseFields must have guard_rejected field"
    assert f.guard_rejected is True, f"Guard-reject wire must parse guard_rejected=True, got {f.guard_rejected!r}"


# ------ G9b : slot-1 FAILED:<CS> wins even when wedge is also present ------

# Red-precondition: slot-1 already exists, but need to confirm it still wins
# AFTER the new wedge slot is wired (regression guard / hallucination lock).
LIVE_CS_ERROR_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|4.1
sync=failed  epoch=2
iscompiling=true  cn_active=false  started=true  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=Assets/Editor/Foo.cs(12,5): error CS0103: 'Foo' does not exist
log=CS0103
"""

def test_diagnose_live_cs_error_wins_over_wedge():
    """errors= has CS0103 AND a build-failed wedge report → FAILED:CS0103 (slot-1 wins).

    Red-precondition: once slot 3 (BUILD-FAILED-WEDGE) is wired, it could shadow
    slot-1 if the order is wrong. This test locks the invariant.
    """
    from unity_mcp.editor_log import WedgeReport
    wedge = WedgeReport(kind="build-failed-wedge", cs_errors=["CS0103: foo"])
    f = _d._parse_diagnose(LIVE_CS_ERROR_WIRE)
    v = _d._verdict(f, wedge=wedge)
    assert v == "FAILED:CS0103", f"Slot-1 FAILED:<CS> must win over wedge, got {v!r}"


# ------ C10: reload_failed= in-process signal ------
# Red-precondition (C10a): _DiagnoseFields has no reload_failed field and _verdict ignores
# reload_failed=true → returns CLEAN-LIVE or NO-OP today instead of BUILD-FAILED-WEDGE.

RELOAD_FAILED_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|4.1
sync=failed  epoch=2
iscompiling=false  cn_active=false  started=true  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
reload_failed=true
log=clean
"""

RELOAD_FAILED_WITH_CS_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|4.1
sync=failed  epoch=2
iscompiling=false  cn_active=false  started=true  stamp_frozen=true
dlls=UnityMCP.Editor:639169455305003280:fresh
reload_failed=true
errors=Assets/Editor/Foo.cs(12,5): error CS0103: 'Foo' does not exist
log=CS0103
"""

RELOAD_FAILED_FALSE_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=true  started=true  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
reload_failed=false
log=clean
"""


def test_diagnose_reload_failed_true_no_log_evidence_yields_build_failed_wedge():
    """C10a: reload_failed=true + wedge=None (log rolled/stale) + frozen domain
    → BUILD-FAILED-WEDGE (in-process signal alone fires).

    Red-precondition: _DiagnoseFields has no reload_failed field; _verdict ignores it
    → returns FAILED:unknown today (idle-failed slot 9) instead of BUILD-FAILED-WEDGE.
    """
    f = _d._parse_diagnose(RELOAD_FAILED_WIRE)
    v = _d._verdict(f, wedge=None)
    assert v.startswith("BUILD-FAILED-WEDGE"), (
        f"reload_failed=true with no log evidence must yield BUILD-FAILED-WEDGE, got {v!r}"
    )


def test_diagnose_reload_failed_true_cs_error_present_slot1_wins():
    """C10b: reload_failed=true BUT errors= has CS0103 → slot-1 FAILED:CS0103 wins.

    Red-precondition: slot-1 already works; this is a regression guard to confirm
    reload_failed wiring does NOT shadow the ground-truth CS error.
    """
    f = _d._parse_diagnose(RELOAD_FAILED_WITH_CS_WIRE)
    v = _d._verdict(f, wedge=None)
    assert v == "FAILED:CS0103", (
        f"Slot-1 FAILED:<CS> must win over reload_failed=true, got {v!r}"
    )


def test_diagnose_reload_failed_false_clean_wire_yields_clean_live():
    """C10c: reload_failed=false + otherwise-clean wire → CLEAN-LIVE (no false-fire).

    Red-precondition: passes vacuously today (field absent, verdict falls to CLEAN-LIVE
    on other grounds). Locks the negative invariant after reload_failed is wired.
    """
    f = _d._parse_diagnose(RELOAD_FAILED_FALSE_WIRE)
    v = _d._verdict(f, wedge=None)
    assert v == "CLEAN-LIVE", (
        f"reload_failed=false + clean wire must yield CLEAN-LIVE, got {v!r}"
    )


# ------ FIX-1 : all_errors= cross-asmdef compile error detection ------

ALL_ERRORS_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=No compilation errors
log=clean
main_mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
reload_failed=false
all_errors=UnityMCP.Editor.Tests:CS0246:Assets/Editor/Tests/Foo.cs:12: error CS0246: The type or namespace name 'Foo' could not be found
"""

ALL_ERRORS_EMPTY_PAYLOAD = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle|8.2
sync=ready  epoch=3
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
main_mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
reload_failed=false
all_errors=
"""


def test_all_errors_yields_failed_cs():
    """FIX-1: all_errors= with CS0246 + clean errors= → FAILED:CS0246."""
    f = _d._parse_diagnose(ALL_ERRORS_PAYLOAD)
    assert "CS0246" in f.all_errors, f"all_errors field must contain CS0246, got: {f.all_errors!r}"
    v = _d._verdict(f)
    assert v == "FAILED:CS0246", f"all_errors with CS0246 must yield FAILED:CS0246, got {v!r}"


def test_all_errors_empty_no_impact():
    """FIX-1: all_errors= empty + clean errors= → CLEAN-LIVE (no false-fire)."""
    f = _d._parse_diagnose(ALL_ERRORS_EMPTY_PAYLOAD)
    assert f.all_errors == "", f"Empty all_errors must parse to empty string, got: {f.all_errors!r}"
    v = _d._verdict(f)
    assert v == "CLEAN-LIVE", f"Empty all_errors + clean wire must yield CLEAN-LIVE, got {v!r}"


# ------ Slot 9 corroboration: idle-failed + empty errors + no evidence ------

IDLE_FAILED_NO_EVIDENCE_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|6.0
sync=ready  epoch=2
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
all_errors=
"""

IDLE_FAILED_STALE_DLL_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|6.0
sync=ready  epoch=2
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:stale
errors=
log=clean
all_errors=
"""

IDLE_FAILED_ALL_ERRORS_CS_WIRE = """\
mvid=60d2de34-f1b2-4c3d-a5e6-789012345678
stamp=60d2de34-f1b2-4c3d-a5e6-789012345678:639169455305003280
compile=idle-failed|6.0
sync=ready  epoch=2
iscompiling=false  cn_active=false  started=false  stamp_frozen=false
dlls=UnityMCP.Editor:639169455305003280:fresh
errors=
log=clean
all_errors=SomeAsm:CS0117:Foo.cs:1: error CS0117: blah
"""


def test_slot9_no_evidence_falls_through():
    """idle-failed + empty errors + fresh dlls + clean log → NOT FAILED:unknown.

    Corroboration: no stale dlls, no log errors, no reload_failed → stale flag,
    fall through to remaining slots (CLEAN-LIVE in this wire).
    """
    f = _d._parse_diagnose(IDLE_FAILED_NO_EVIDENCE_WIRE)
    v = _d._verdict(f)
    assert v != "FAILED:unknown", f"No corroborating evidence → must not be FAILED:unknown, got {v!r}"


def test_slot9_stale_dll_yields_failed_unknown():
    """idle-failed + empty errors + stale dll → FAILED:unknown (corroborated)."""
    f = _d._parse_diagnose(IDLE_FAILED_STALE_DLL_WIRE)
    v = _d._verdict(f)
    assert v == "FAILED:unknown", f"Stale dll corroborates idle-failed → FAILED:unknown, got {v!r}"


def test_slot9_all_errors_cs_code():
    """idle-failed + empty errors= + all_errors has CS0117 → FAILED:CS0117."""
    f = _d._parse_diagnose(IDLE_FAILED_ALL_ERRORS_CS_WIRE)
    v = _d._verdict(f)
    assert v == "FAILED:CS0117", f"all_errors CS code should be extracted, got {v!r}"


# ---------------------------------------------------------------------------
# isReallyCompiling field — Fix 2
# ---------------------------------------------------------------------------

def test_parse_is_really_compiling_true():
    """isReallyCompiling=true line is parsed into is_really_compiling=True."""
    wire = (
        "mvid=abc\nstamp=abc:100\ncompile=idle\nsync=ready  epoch=1\n"
        "iscompiling=false  cn_active=true  started=false  stamp_frozen=false\n"
        "isReallyCompiling=true\n"
        "dlls=UnityMCP.Editor:100:fresh\nerrors=\nlog=clean\n"
    )
    f = _d._parse_diagnose(wire)
    assert f.is_really_compiling is True


def test_parse_is_really_compiling_false_by_default():
    """isReallyCompiling absent → is_really_compiling defaults to False."""
    f = _d._parse_diagnose(CLEAN_PAYLOAD)
    assert f.is_really_compiling is False


def test_verdict_stale_latch_is_wedge_engine():
    """iscompiling=true + is_really_compiling=false + cn_active=true + stamp_frozen
    → WEDGE-ENGINE via is_really_compiling signal (cn_active=true bypasses old check)."""
    wire = (
        "mvid=abc\nstamp=abc:100\ncompile=compiling\nsync=ready  epoch=1\n"
        "iscompiling=true  cn_active=true  started=false  stamp_frozen=true\n"
        "isReallyCompiling=false\n"
        "dlls=UnityMCP.Editor:100:fresh\nerrors=\nlog=clean\n"
    )
    f = _d._parse_diagnose(wire)
    v = _d._verdict(f)
    assert v == "WEDGE-ENGINE", (
        f"Stale latch (iscompiling=true + IsReallyCompiling=false + cn_active=true) "
        f"must be WEDGE-ENGINE, got {v!r}"
    )
