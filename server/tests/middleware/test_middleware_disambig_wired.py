"""Tests for Cycle 5d Item 1 — Disambiguator wired into resolve_path_live."""
from unittest.mock import AsyncMock, MagicMock
from unity_mcp.middleware import Middleware, wrap_send


def test_disambig_disabled_via_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "0")
    mw = Middleware()
    assert mw._disambig_enabled is False
    # _get_disambig returns None
    assert mw._get_disambig() is None


def test_disambig_enabled_by_default(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_DISAMBIG", raising=False)
    mw = Middleware()
    assert mw._disambig_enabled is True


def test_disambig_lazy_init(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")
    mw = Middleware()
    assert mw._disambig is None

    d = mw._get_disambig()
    assert d is not None

    # Cached on second call
    d2 = mw._get_disambig()
    assert d is d2


def test_disambig_refreshes_snapshots(monkeypatch):
    """Disambiguator's internal state refreshes from Middleware on each access."""
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")
    mw = Middleware()

    mw._recent_focus.append("/A")
    d1 = mw._get_disambig()
    assert "/A" in d1._recent

    mw._recent_focus.append("/B")
    d2 = mw._get_disambig()
    assert "/B" in d2._recent


def test_disambig_resolves_with_recency_marker(monkeypatch):
    """When recent path matches one candidate, auto-resolve + marker."""
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")
    mw = Middleware()

    # Set up: /Game/Player in recent_focus
    mw._recent_focus.append("/Game/Player")

    d = mw._get_disambig()
    decision = d.decide("Player", ["/Game/Player", "/UI/Player"])

    assert decision is not None
    chosen, marker = decision
    assert chosen == "/Game/Player"
    assert "RESOLVED" in marker


def test_disambig_blocks_when_ambiguous(monkeypatch):
    """When margin not met, returns None for block path."""
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")
    mw = Middleware()

    # Both /Game/Player and /UI/Player in recent — margin not met
    mw._recent_focus.append("/Game/Player")
    mw._recent_focus.append("/UI/Player")

    d = mw._get_disambig()
    decision = d.decide("Player", ["/Game/Player", "/UI/Player"])

    assert decision is None


async def test_explicit_path_bypasses_disambig(monkeypatch):
    """Agent passes _explicit_path=True → resolve_path_live SKIPPED, exact path sent to Unity."""
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")

    mw = Middleware()
    mw.known_paths = {"/Game/Player", "/UI/Player"}  # ambiguous

    sent_args = []

    async def fake_send(cmd, args, timeout=30.0):
        sent_args.append((cmd, dict(args)))
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/Player", "type": "Transform", "_explicit_path": True})

    # Bypass succeeded — TCP called with original path
    actual_cmd_args = [a for c, a in sent_args if c == "get_component"]
    assert len(actual_cmd_args) == 1
    assert actual_cmd_args[0]["path"] == "/Player"
    # _explicit_path stripped before bridge
    assert "_explicit_path" not in actual_cmd_args[0]
    # No AMBIGUOUS block
    assert "[AMBIGUOUS:" not in result


async def test_wrap_send_returns_disambig_block_no_tcp(monkeypatch):
    """End-to-end: ambiguous path → AMBIGUOUS block returned, TCP never called for actual cmd.

    known_paths has no suffix match for 'Hero', so resolve_path passes through,
    then resolve_path_live hits search_scene which returns multiple candidates.
    """
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "1")

    mw = Middleware()
    # Populate known_paths with unrelated paths — no suffix match for /Hero
    mw.known_paths = {"/Stage/Boss", "/Stage/Minion"}

    tcp_calls = []

    async def fake_send(cmd, args, timeout=30.0):
        if cmd == "search_scene":
            return "/Game/Hero\n/UI/Hero\n/Other/Hero"
        tcp_calls.append((cmd, args))
        return "should-not-be-called"

    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/Hero", "type": "Transform"})

    # AMBIGUOUS block returned
    assert "[AMBIGUOUS:" in result
    # No actual cmd reached TCP — only search_scene allowed
    actual_cmds = [c for c, _ in tcp_calls]
    assert "get_component" not in actual_cmds, f"get_component should NOT have been called, got: {actual_cmds}"
