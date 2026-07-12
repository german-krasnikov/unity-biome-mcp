"""TDD tests for ToolHinter — server-side discoverability hints."""
import os
import re
import time
from unittest.mock import AsyncMock, patch

# ToolHinter is in hinter.py — not yet created (Red phase)
from unity_mcp.hinter import ToolHinter
from unity_mcp.metrics import METRICS


# ── Helpers ──────────────────────────────────────────────────────────────────

def make_hinter(**kwargs) -> ToolHinter:
    return ToolHinter(**kwargs)


def gc(path: str, component: str = "Health") -> tuple:
    return ("get_component", {"path": path, "type": component})


def sp(path: str, component: str = "Health", prop: str = "value") -> tuple:
    return ("set_property", {"path": path, "component": component, "prop": prop, "value": "1"})


def observe_seq(h: ToolHinter, calls: list[tuple]) -> list:
    results = []
    for cmd, args in calls:
        results.append(h.observe(cmd, args))
    return results


# ── Test 1: inspect-loop fires ────────────────────────────────────────────────

def test_inspect_loop_fires():
    """3 get_component calls → 3rd returns hint containing 'inspect'."""
    h = make_hinter()
    results = observe_seq(h, [
        gc("/A"), gc("/B"), gc("/C"),
    ])
    assert results[0] is None
    assert results[1] is None
    assert results[2] is not None
    assert "[HINT:" in results[2]
    assert "inspect" in results[2]


# ── Test 2: batch-writes same target fires ────────────────────────────────────

def test_batch_writes_same_target_fires():
    """3 set_property on same (path, component), different props → 3rd returns hint."""
    h = make_hinter()
    results = observe_seq(h, [
        sp("/A", "Health", "value"),
        sp("/A", "Health", "max"),
        sp("/A", "Health", "regen"),
    ])
    assert results[2] is not None
    assert "[HINT:" in results[2]
    # batch or set_property_delta are both legitimate batch-write suggestions
    assert re.search(r"batch|set_property_delta", results[2], re.IGNORECASE), results[2]


# ── Test 3: batch-writes different targets → no hint (FP guard) ───────────────

def test_batch_writes_different_target_no_hint():
    """3 set_property on DIFFERENT paths → no hint."""
    h = make_hinter()
    results = observe_seq(h, [
        sp("/A", "Health", "value"),
        sp("/B", "Health", "value"),
        sp("/C", "Health", "value"),
    ])
    assert all(r is None for r in results)


# ── Test 4: screenshot spam with intervening write → no hint ──────────────────

def test_screenshot_spam_with_intervening_write_no_hint():
    """screenshot, screenshot, set_property, screenshot → no hint at the 3rd screenshot."""
    h = make_hinter()
    results = observe_seq(h, [
        ("screenshot", {}),
        ("screenshot", {}),
        sp("/A", "Health", "value"),
        ("screenshot", {}),
    ])
    # 3rd screenshot (index 3) should NOT fire because write intervened
    assert results[3] is None


# ── Test 5: cooldown prevents repeated emit ───────────────────────────────────

def test_cooldown_prevents_repeated_emit():
    """After emitting, same pattern can't re-emit for 8 calls."""
    h = make_hinter()
    # First emission: calls 0,1,2 → hint on call 2
    first = observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])
    assert first[2] is not None

    # Now 4 more get_component calls — all should be None (still in cooldown)
    second = observe_seq(h, [gc("/D"), gc("/E"), gc("/F"), gc("/G")])
    assert all(r is None for r in second)


# ── Test 6: suppression after two ignores (split into focused tests) ──────────

def test_first_ignore_increments_counter_and_re_emits():
    """After first ignore, ignore counter=1 and hint re-emits.

    Internal state: _ignored, _emitted_at. Acceptable leak — state machine.
    """
    h = make_hinter()
    # Emit once
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])
    assert h._last_hint_pattern == "inspect-loop"

    # 1st ignore: reset cooldown so predicate fires again
    h._emitted_at["inspect-loop"] = -100
    result = h.observe("get_component", {"path": "/X"})

    assert result is not None
    assert h._ignored.get("inspect-loop", 0) == 1


def test_second_ignore_adds_to_suppressed():
    """After second ignore, pattern enters _suppressed set.

    Internal state: _suppressed. Acceptable leak — state machine.
    """
    h = make_hinter()
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])

    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/X"})  # 1st ignore

    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/Y"})  # 2nd ignore

    assert "inspect-loop" in h._suppressed


def test_suppressed_pattern_skips_predicate():
    """Once suppressed, observe() returns None even if predicate would fire.

    Internal state: _suppressed. Acceptable leak — state machine.
    """
    h = make_hinter()
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])

    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/X"})

    h._emitted_at["inspect-loop"] = -100
    result2 = h.observe("get_component", {"path": "/Y"})

    assert result2 is None


# ── Test 7: adoption increments metric ───────────────────────────────────────

def test_adoption_increments_metric():
    """After inspect-loop hint, calling 'inspect' increments adopted counter."""
    METRICS.reset()
    h = make_hinter()
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])  # emit hint
    # Next call is the suggested cmd
    h.observe("inspect", {"paths": ["/A", "/B", "/C"]})
    assert METRICS._counters.get("hint.adopted.inspect-loop", 0) == 1


# ── Test 8: integration — middleware appends hint ─────────────────────────────

async def test_middleware_appends_hint_string():
    """Drive 3 get_component through wrap_send → last result contains [HINT:."""
    from unity_mcp.middleware import wrap_send, Middleware

    call_count = 0

    async def mock_send(cmd, args, timeout=30.0):
        nonlocal call_count
        call_count += 1
        return f"result_{call_count}"

    mw = Middleware()
    mw.hinter = ToolHinter(enabled=True)
    wrapped = wrap_send(mock_send, mw)

    # Disable noisy middleware features
    with patch.dict(os.environ, {"UNITY_MCP_REFLECT": "0"}):
        r1 = await wrapped("get_component", {"path": "/A", "type": "Health"})
        r2 = await wrapped("get_component", {"path": "/B", "type": "Health"})
        r3 = await wrapped("get_component", {"path": "/C", "type": "Health"})

    assert "[HINT:" in r3


# ── Test 9: disabled via env / constructor ────────────────────────────────────

def test_disabled_via_env():
    """ToolHinter(enabled=False) never returns hints."""
    h = ToolHinter(enabled=False)
    results = observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])
    assert all(r is None for r in results)


# ── Test 10: skip on DEGRADED marker ─────────────────────────────────────────

async def test_skip_on_degraded_marker():
    """Result starting with [DEGRADED: → middleware does NOT append hint."""
    from unity_mcp.middleware import wrap_send, Middleware

    async def mock_send(cmd, args, timeout=30.0):
        return "[DEGRADED: Unity disconnected]"

    mw = Middleware()
    mw.hinter = ToolHinter(enabled=True)
    wrapped = wrap_send(mock_send, mw)

    with patch.dict(os.environ, {"UNITY_MCP_REFLECT": "0"}):
        r1 = await wrapped("get_component", {"path": "/A", "type": "Health"})
        r2 = await wrapped("get_component", {"path": "/B", "type": "Health"})
        r3 = await wrapped("get_component", {"path": "/C", "type": "Health"})

    assert "[HINT:" not in r3


# ── Test 11: console-poll pattern ────────────────────────────────────────────

def test_console_poll_pattern():
    """recompile, get_console, get_console, get_console → hint on last."""
    h = make_hinter()
    results = observe_seq(h, [
        ("recompile", {}),
        ("get_console", {}),
        ("get_console", {}),
        ("get_console", {}),
    ])
    assert results[3] is not None
    assert "[HINT:" in results[3]
    # wait_until or get_compile_errors are both valid suggestions after recompile+poll
    assert re.search(r"wait_until|get_compile_errors", results[3], re.IGNORECASE), results[3]


# ── Test 12: redundant verify read ────────────────────────────────────────────

def test_redundant_verify_read():
    """set_property /A Health, then get_component /A Health → hint."""
    h = make_hinter()
    with patch.dict(os.environ, {"UNITY_MCP_REFLECT": "1"}):
        h.observe("set_property", {"path": "/A", "component": "Health", "prop": "value", "value": "100"})
        result = h.observe("get_component", {"path": "/A", "type": "Health"})
    assert result is not None
    assert "[HINT:" in result
    # reflect/snapshot/skip are all valid suggestions for redundant verify-read pattern
    assert re.search(r"reflect|snapshot|skip", result, re.IGNORECASE), result


# ── Test 13: predicate exception caught ──────────────────────────────────────

async def test_predicate_exception_caught():
    """Malicious pattern that raises → middleware catches, hinter.error incremented, no exception."""
    from unity_mcp.middleware import wrap_send, Middleware
    from unity_mcp.hinter import Pattern
    from collections import deque

    METRICS.reset()

    async def mock_send(cmd, args, timeout=30.0):
        return "ok"

    mw = Middleware()
    h = ToolHinter(enabled=True)

    # Inject a broken pattern
    from unity_mcp.hinter import Call
    bad_pattern = Pattern(
        id="bad-pattern",
        predicate=lambda recent, call: 1 / 0,  # always raises
        suggested_cmd="",
        hint="[HINT: should never appear]",
        trigger_cmd="",
    )
    h._patterns.append(bad_pattern)
    mw.hinter = h

    wrapped = wrap_send(mock_send, mw)
    with patch.dict(os.environ, {"UNITY_MCP_REFLECT": "0"}):
        result = await wrapped("get_component", {"path": "/A", "type": "Health"})

    # Should not raise, counter incremented
    assert METRICS._counters.get("hinter.error", 0) >= 1
    assert "[HINT: should never appear]" not in result


# ── Test 14: find-then-read pattern ──────────────────────────────────────────

def test_find_then_read_pattern():
    """find_objects followed by get_component → hint suggesting inspect."""
    h = make_hinter()
    h.observe("find_objects", {"name": "Enemy"})
    result = h.observe("get_component", {"path": "/Enemy", "type": "Health"})
    assert result is not None
    assert "[HINT:" in result
    assert "inspect" in result


# ── Test 15: screenshot-spam no false-positive when write is animation ────────

def test_screenshot_animation_screenshot_no_hint():
    """screenshot → animation → screenshot → screenshot — no spam hint at last.

    animation is a write cmd; write between screenshots must suppress spam hint.
    """
    h = make_hinter()
    results = observe_seq(h, [
        ("screenshot", {}),
        ("animation", {"path": "/A"}),
        ("screenshot", {}),
        ("screenshot", {}),
    ])
    # last screenshot has a write (animation) between the first two screenshots
    assert results[3] is None


# ── Test 16: suppression metric recorded ─────────────────────────────────────

def test_suppression_metric_recorded():
    """After 2 ignores suppression triggers → hint.suppressed.<id> == 1."""
    METRICS.reset()
    h = make_hinter()
    # emit
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])
    # 1st ignore
    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/X"})
    # 2nd ignore → suppression
    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/Y"})
    assert METRICS._counters.get("hint.suppressed.inspect-loop", 0) == 1


# ── Test 17: format_report includes suppressed count ─────────────────────────

def test_format_report_includes_suppressed():
    """format_report shows suppressed= in [Hinter] section."""
    METRICS.reset()
    h = make_hinter()
    observe_seq(h, [gc("/A"), gc("/B"), gc("/C")])
    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/X"})
    h._emitted_at["inspect-loop"] = -100
    h.observe("get_component", {"path": "/Y"})
    report = METRICS.format_report()
    assert "suppressed=1" in report


# ── Test 18: screenshot-spam fires on third consecutive ──────────────────────

def test_screenshot_spam_fires():
    """3 consecutive screenshots with no writes → third returns hint containing 'fingerprint'."""
    h = make_hinter()
    r1 = h.observe("screenshot", {})
    r2 = h.observe("screenshot", {})
    r3 = h.observe("screenshot", {})
    assert r1 is None
    assert r2 is None
    assert r3 is not None
    assert "[HINT:" in r3
    assert "fingerprint" in r3


# ── Zone #29 gap tests ────────────────────────────────────────────────────────

def test_console_poll_no_hint_without_recompile():
    """get_console × 3 WITHOUT prior recompile → NO console-poll hint."""
    h = make_hinter()
    results = observe_seq(h, [
        ("get_console", {}),
        ("get_console", {}),
        ("get_console", {}),
    ])
    assert all(r is None for r in results), f"Expected no hint but got: {results}"


def test_redundant_verify_no_hint_when_reflect_off():
    """set_property → get_component should NOT fire hint when UNITY_MCP_REFLECT=0."""
    import os
    from unittest.mock import patch
    h = make_hinter()
    with patch.dict(os.environ, {"UNITY_MCP_REFLECT": "0"}):
        h.observe("set_property", {"path": "/A", "component": "Health", "prop": "value", "value": "100"})
        result = h.observe("get_component", {"path": "/A", "type": "Health"})
    assert result is None, f"Expected no hint with REFLECT=0, got: {result}"
