"""Offline tests for sync_unity's tolerance of the C# compile guard on 'sync' (A7-a / PD-1).

Root cause: Unity's own headed auto-refresh or a Package Manager resolve can
already be mid-compile when 'sync' arrives. CommandRouter.CheckGuards rejects
it with the exact SYNC_COMPILE_GUARD_TEXT; _send_raw turns that into a
ToolError. Before this fix, _sync_unity only caught ConnectionError on the
'sync' send, so the ToolError escaped unhandled to the MCP client ("the agent
stops working"). The fix matches that one exact guard string and falls
through to the same _await_sync_completion tail a normal
'sync_ack|...|will_compile=true' response uses -- no synthesized ack, no
retry of 'sync' itself (RW, not retry-safe).
"""
import pathlib
import re
from unittest.mock import MagicMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.sync as _sync
from unity_mcp import editor_log
from unity_mcp.constants import SYNC_COMPILE_GUARD_TEXT

# ── Fixtures ─────────────────────────────────────────────────────────────────

@pytest.fixture(autouse=True)
def _patch_corroborate():
    """Real editor_log.corroborate() reads Editor.log / dll freshness off disk
    once csharp_response has no "error CS" substring -- not hermetic. Replace
    it with a pass-through, matching test_sync.py's established pattern."""
    async def _pass_through(send, *, compile_status=""):
        return await send("get_compile_errors", {})

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.get_corroborated_errors = _pass_through
        mock_el.init_corroboration = MagicMock()
        mock_el.UNITY_UNREACHABLE = editor_log.UNITY_UNREACHABLE
        yield mock_el


def _patch_poll_interval(monkeypatch, value: float) -> None:
    monkeypatch.setattr(_sync, "_POLL_INTERVAL", value)


def _make_guarded_send(status_seq, errors_response: str = ""):
    """'sync' always raises the exact compile-guard ToolError -- never retried
    by us. sync_status returns a neutral pre-stamp reply until the guard is
    hit, then replays status_seq in order and holds on the last entry.
    counts records exactly how many times each command was sent."""
    status_iter = iter(status_seq)
    held = status_seq[-1] if status_seq else "epoch=0|state=idle"
    guard_hit = False
    counts: dict[str, int] = {}

    async def _send(cmd, args=None, **kwargs):
        nonlocal held, guard_hit
        counts[cmd] = counts.get(cmd, 0) + 1
        if cmd == "sync":
            guard_hit = True
            raise ToolError(SYNC_COMPILE_GUARD_TEXT)
        if cmd == "sync_status":
            if not guard_hit:
                return "epoch=0|state=idle"  # pre-stamp read, before 'sync' is even sent
            held = next(status_iter, held)
            return held
        if cmd == "compile_status":
            return "idle|1"
        if cmd == "get_compile_errors":
            return errors_response
        if cmd == "diagnose":
            return "main_mvid=absent"
        if cmd == "warm_type_cache":
            return "ok:types=42"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send, counts


# ── Case 1: guard hit, compile finishes clean ───────────────────────────────

async def test_sync_unity_guard_hit_then_ready_returns_sync_clean(monkeypatch):
    _patch_poll_interval(monkeypatch, 0.001)
    send, counts = _make_guarded_send(
        status_seq=["epoch=1|state=compiling", "epoch=1|state=ready"],
    )
    _sync._send = send
    try:
        result = await _sync.sync_unity(timeout=10)
    finally:
        _sync._send = None

    assert result == "sync clean"
    assert counts["sync"] == 1  # effect spy: no retry storm on the guarded command


# ── Case 2: guard hit, compile finishes with errors ─────────────────────────

async def test_sync_unity_guard_hit_then_errors_returns_same_error_text(monkeypatch):
    _patch_poll_interval(monkeypatch, 0.001)
    send, counts = _make_guarded_send(
        status_seq=["epoch=1|state=compiling", "epoch=1|state=ready"],
        errors_response="error CS1002: ; expected",
    )
    _sync._send = send
    try:
        result = await _sync.sync_unity(timeout=10)
    finally:
        _sync._send = None

    assert result == "error CS1002: ; expected"
    assert counts["sync"] == 1


# ── Case 3: a different ToolError must propagate unchanged ─────────────────

async def test_sync_unity_other_toolerror_propagates_unchanged():
    """The text mentions 'compiling' but is not the exact guard string -- proves
    a non-identical ToolError is not absorbed by the guard and propagates
    unchanged (the complement of the exact-match check, not a positive proof
    that some other text would be absorbed)."""
    other_text = "Unity is compiling something unrelated to the sync guard"

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync_status":
            return "epoch=0|state=idle"  # pre-stamp read, before 'sync' is even sent
        if cmd == "sync":
            raise ToolError(other_text)
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _sync._send = _send
    try:
        try:
            await _sync.sync_unity(timeout=10)
            raise AssertionError("expected ToolError to propagate")
        except ToolError as exc:
            assert str(exc) == other_text
    finally:
        _sync._send = None


# ── Case 4: guard hit, Unity stays compiling past the deadline ─────────────

async def test_sync_unity_guard_hit_stays_compiling_past_deadline_times_out(monkeypatch):
    """Same 'STOP: reload observation exceeded' verdict the normal
    will_compile=true path produces on timeout. _POLL_INTERVAL is set far
    longer (100s) than the 0.05s deadline -- unambiguous proof that the outer
    asyncio.timeout_at cancels a genuinely in-flight real asyncio.sleep,
    not that the poll interval merely happened to be short enough to elapse
    on its own (the previous 1.0 value equaled the untouched default, so it
    tested nothing about the timeout path itself)."""
    monkeypatch.setattr(_sync, "_POLL_INTERVAL", 100.0)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            raise ToolError(SYNC_COMPILE_GUARD_TEXT)
        if cmd == "sync_status":
            return "epoch=1|state=compiling"  # never reaches ready/failed
        raise AssertionError(f"Unexpected cmd: {cmd}")

    _sync._send = _send
    try:
        result = await _sync.sync_unity(timeout=0.05)
    finally:
        _sync._send = None

    assert result == "STOP: reload observation exceeded 0.05s; Unity operation may still be running"


# ── Parity: Python constant pinned to the C# compile-guard literal ─────────

_COMMAND_ROUTER_CS_PATH = (
    pathlib.Path(__file__).parents[2] / "unity-plugin/Editor/CommandRouter.cs"
)
# Anchored to CheckGuards' IsCompiling() branch specifically -- not the
# "Server initializing. Retry in 2s." CommandRegistry.Ready branch a few
# lines above it, which also calls FormatBusyResponse but is a different
# guard with a different retry contract.
_CS_COMPILE_GUARD_RE = re.compile(
    r'IsCompiling\(\)[^;]*?FormatBusyResponse\([^,]+,\s*"([^"]+)"', re.DOTALL
)


def test_sync_compile_guard_text_matches_csharp_compile_branch():
    """SYNC_COMPILE_GUARD_TEXT (constants.py) must be byte-identical to the
    literal CommandRouter.CheckGuards emits from its IsCompiling() branch --
    the two are independent by necessity (different runtimes/files), and
    tools/sync.py matches this string by exact equality (not substring), so
    any drift here silently breaks the PD-1 guard-catch path this module
    tests above."""
    assert _COMMAND_ROUTER_CS_PATH.exists(), f"C# source not found: {_COMMAND_ROUTER_CS_PATH}"
    cs_text = _COMMAND_ROUTER_CS_PATH.read_text(encoding="utf-8")

    m = _CS_COMPILE_GUARD_RE.search(cs_text)
    assert m, "compile-guard FormatBusyResponse literal not found in CommandRouter.cs"
    assert m.group(1) == SYNC_COMPILE_GUARD_TEXT
