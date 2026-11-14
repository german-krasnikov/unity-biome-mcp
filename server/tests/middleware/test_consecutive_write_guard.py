"""MCP-GUARD-007: consecutive-write guard must not false-fail on playtest runs."""
import pytest
from unity_mcp.middleware import Middleware
from unity_mcp.tools.runtime import _is_playtest_pass


# ── transition: run_playtest must not count as a consecutive write ────────────

def test_transition_run_playtest_does_not_increment_consecutive_writes():
    """Single run_playtest must not increment the consecutive-write counter."""
    mw = Middleware()
    result = mw.transition("run_playtest", {"script": "ASSERT /A|H == 1"})
    assert result is None
    assert mw._consecutive_writes == 0


def test_transition_5_consecutive_playtests_no_warning():
    """5 consecutive run_playtest calls must never trigger the guard."""
    mw = Middleware()
    for _ in range(5):
        result = mw.transition("run_playtest", {"script": "ASSERT /A|H == 1"})
    assert result is None
    assert mw._consecutive_writes == 0


def test_transition_run_playtest_suite_does_not_increment_consecutive_writes():
    """run_playtest_suite must not increment the consecutive-write counter."""
    mw = Middleware()
    result = mw.transition("run_playtest_suite", {"pattern": "Playtests/*.playtest"})
    assert result is None
    assert mw._consecutive_writes == 0


def test_transition_playtest_resets_write_count_after_real_write():
    """Playtest following a real write must reset the counter to 0."""
    mw = Middleware()
    mw.transition("set_property", {})  # real write → count = 1
    assert mw._consecutive_writes == 1
    mw.transition("run_playtest", {})  # playtest → should reset
    assert mw._consecutive_writes == 0


def test_transition_real_scene_mutations_still_trigger_guard():
    """Actual scene mutations (set_property × 3) must still trigger the guard."""
    mw = Middleware()
    mw.transition("set_property", {})
    mw.transition("set_property", {})
    result = mw.transition("set_property", {})
    assert result is not None
    assert "consecutive writes" in result


# ── _is_playtest_pass: resilient to prepended middleware warnings ─────────────

def test_is_playtest_pass_with_prepended_warning():
    """⚡ warning prepended to a passing playtest result must not flip it to FAIL."""
    raw = (
        "⚡ 3 consecutive writes without reading. Consider verifying state.\n"
        "PLAYTEST: 3/3 passed"
    )
    assert _is_playtest_pass(raw) is True


def test_is_playtest_pass_normal_first_line_pass():
    """Normal passing result without any prepended warning still passes."""
    assert _is_playtest_pass("PLAYTEST: 2/2 passed") is True


def test_is_playtest_pass_false_when_fail_keyword_present():
    """FAIL keyword anywhere in the result must still be detected."""
    raw = "⚡ warning\nPLAYTEST: 2/2\nFAIL: assertion failed"
    assert _is_playtest_pass(raw) is False


def test_is_playtest_pass_false_when_no_playtest_line():
    """Result with no PLAYTEST: X/Y line must return False."""
    assert _is_playtest_pass("some other output") is False


def test_is_playtest_pass_false_when_counts_mismatch():
    """Partial pass (2/3) with warning prepended must still return False."""
    raw = "⚡ warning\nPLAYTEST: 2/3 failed"
    assert _is_playtest_pass(raw) is False
