"""Timing contract tests — pin domain reload recovery constants.

Covers: backoff bounds, hard deadline, startup grace, stall threshold,
and the absence of an age-based C# result transition. No Unity required.

Evidence: 141 monkey experiments in Plans/Reload/Monkey/ + RC analysis in
Plans/Reload/V2/tcp-churn-heartbeat-fix-2026-07.md.
"""
from pathlib import Path

import unity_mcp.bridge as _bridge
from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S, BACKOFF_MAX_S, HARD_DEADLINE_S

_PROJECT = Path(__file__).parents[2]
_PLUGIN  = _PROJECT / "unity-plugin"
_BH      = Path(__file__).parents[1] / "src/unity_mcp"


# ===========================================================================
# Group A: Backoff bounds (bridge_heartbeat.py)
# ===========================================================================

def test_backoff_min_is_5s():
    """BACKOFF_MIN_S == 5.0 — first reconnect no sooner than 5s after TCP drop."""
    assert BACKOFF_MIN_S == 5.0


def test_backoff_max_is_60s():
    """BACKOFF_MAX_S == 60.0 — exponential backoff caps at 60s."""
    assert BACKOFF_MAX_S == 60.0


# ===========================================================================
# Group B: Grace / hard-deadline (bridge.py + bridge_heartbeat.py)
# ===========================================================================

def test_startup_grace_default_is_90s():
    """STARTUP_GRACE_S default == 90.0 when env not overridden."""
    assert _bridge.STARTUP_GRACE_S == 90.0


def test_hard_deadline_is_450s():
    """HARD_DEADLINE_S == 450.0 — absolute ceiling for reconnect loop (P7)."""
    assert HARD_DEADLINE_S == 450.0


def test_hard_deadline_is_5x_startup_grace():
    """HARD_DEADLINE_S == 5 × STARTUP_GRACE_S — documented invariant in bridge_heartbeat.py:45."""
    assert HARD_DEADLINE_S == 5.0 * _bridge.STARTUP_GRACE_S


def test_backoff_max_less_than_startup_grace():
    """BACKOFF_MAX_S < STARTUP_GRACE_S — one retry window must fit inside grace window.

    If BACKOFF_MAX >= STARTUP_GRACE the grace timer could expire during a single
    backoff sleep, causing premature session death on first retry failure.
    """
    assert BACKOFF_MAX_S < _bridge.STARTUP_GRACE_S


# ===========================================================================
# Group C: C# structural (text search, no Unity)
# ===========================================================================

def test_testrunner_has_no_age_based_terminal_transition():
    """Run age is never used to erase or fabricate durable lifecycle state."""
    src = (_PLUGIN / "Editor/TestRunner.cs").read_text(encoding="utf-8")
    assert "StaleTimeoutSec" not in src
    assert "KeyStartTime" not in src


def test_ping_stall_threshold_is_6():
    """Stall close fires at 6 failures × 15s interval = 90s tolerance (RC3 fix)."""
    src = (_BH / "bridge_heartbeat.py").read_text(encoding="utf-8")
    assert "_ping_stall_failures >= 6" in src
