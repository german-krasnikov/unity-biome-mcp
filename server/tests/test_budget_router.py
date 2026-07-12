"""TDD: BudgetRouter — adaptive routing decisions."""
import os
import pytest
from pathlib import Path
from unittest.mock import patch
from unity_mcp.budget.cost_tracker import CostTracker
from unity_mcp.budget.router import BudgetRouter, RouteDecision


@pytest.fixture(autouse=True)
def _no_disk_save(monkeypatch):
    """Budget router tests don't need real persistence."""
    monkeypatch.setattr(CostTracker, "_save", lambda self: (True, ""))


def make_tracker(tmp_path, session_cap=1.0, day_cap=5.0):
    return CostTracker(path=tmp_path / "b.json", session_cap=session_cap, day_cap=day_cap)


def make_router(tracker, hit_rate_fn=None):
    return BudgetRouter(tracker, hit_rate_fn)


def exhaust_session(tracker, pct: float):
    """Spend pct fraction of session_cap."""
    usd_needed = tracker._session_cap * pct
    # Use known formula: 1000 in_tok + 0 out = 0.0008 USD
    calls_needed = int(usd_needed / 0.0008) + 1
    for _ in range(calls_needed):
        tracker.record("test", 1000, 0)


def test_disabled_env_always_runs(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 2.0)  # way over budget
    r = make_router(t)
    with patch.dict(os.environ, {"UNITY_MCP_BUDGET_DISABLED": "1"}):
        d = r.should_run("watchdog", 0.3)
    assert d.run is True
    assert d.reason == "ok_disabled"


def test_below_50pct_runs_normally(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.3)
    r = make_router(t)
    d = r.should_run("summarize", 0.2)
    assert d.run is True


def test_50pct_kills_watchdog(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.55)
    r = make_router(t)
    d = r.should_run("watchdog", 0.3)
    assert d.run is False
    assert "50" in d.reason or "low" in d.reason


def test_50pct_kills_summarize(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.55)
    r = make_router(t)
    d = r.should_run("summarize", 0.2)
    assert d.run is False


def test_80pct_kills_low_priority(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.85)
    r = make_router(t)
    d = r.should_run("speculation", 0.4)
    assert d.run is False
    assert "80" in d.reason or "low" in d.reason


def test_95pct_only_critical_runs(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.97)
    r = make_router(t)
    assert r.should_run("scene_brief", 0.4).run is False     # medium
    assert r.should_run("watchdog", 0.3).run is False         # low
    assert r.should_run("do_intent", 0.5).run is True         # critical


def test_day_cap_exceeded_blocks_all(tmp_path):
    t = CostTracker(path=tmp_path / "b.json", session_cap=100.0, day_cap=0.000001)
    t.record("x", 10000, 10000)  # blows past day cap
    r = make_router(t)
    d = r.should_run("do_intent", 0.5)
    assert d.run is False
    assert "day_cap" in d.reason


def test_low_hit_rate_skips_non_critical(tmp_path):
    t = make_tracker(tmp_path)
    r = make_router(t, hit_rate_fn=lambda _: 0.20)
    d = r.should_run("scene_brief", 0.4)
    assert d.run is False
    assert "hit_rate" in d.reason


def test_low_hit_rate_allows_critical(tmp_path):
    t = make_tracker(tmp_path)
    r = make_router(t, hit_rate_fn=lambda _: 0.20)
    d = r.should_run("do_intent", 0.5)  # critical — must pass
    assert d.run is True


def test_critical_at_95_runs(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.97)
    r = make_router(t)
    d = r.should_run("visual_verify", 0.7)  # critical
    assert d.run is True
    assert d.reason == "critical_at_95"


def test_low_difficulty_still_runs(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.35)
    r = make_router(t)
    d = r.should_run("watchdog", 0.3)  # difficulty < 0.5, pct >= 0.30
    assert d.run is True


def test_skip_records_in_tracker(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.97)
    r = make_router(t)
    r.should_run("scene_brief", 0.4)  # medium at 95% — skipped
    assert "scene_brief" in t.status()


def test_unknown_feature_uses_default(tmp_path):
    t = make_tracker(tmp_path)
    r = make_router(t)
    d = r.should_run("totally_unknown_feature", 0.5)
    assert d.run is True  # default=medium, budget fine


def test_decision_reason_descriptive(tmp_path):
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.97)
    r = make_router(t)
    d = r.should_run("summarize", 0.2)
    assert "budget" in d.reason or "skip" in d.reason
    assert d.reason != "ok"


def test_hit_rate_exactly_040_is_not_skipped(tmp_path):
    """P2: hit_rate < 0.40 skips; exactly 0.40 should NOT be skipped."""
    t = make_tracker(tmp_path)
    r = make_router(t, hit_rate_fn=lambda _: 0.40)
    d = r.should_run("scene_brief", 0.4)
    assert d.run is True


def test_reset_session_clears_spent_and_skips(tmp_path):
    """reset_session() zeroes session_spent and clears skipped dict."""
    t = make_tracker(tmp_path)
    exhaust_session(t, 0.97)
    r = make_router(t)
    r.should_run("scene_brief", 0.4)  # medium at 95% → skipped, recorded in tracker

    assert t.session_spent() > 0
    assert "scene_brief" in t.status()

    t.reset_session()

    assert t.session_spent() == 0.0
    assert t.session_pct() == 0.0
    assert "scene_brief" not in t.status()


def test_day_cap_zero_means_no_cap(tmp_path):
    t = CostTracker(path=tmp_path / "b.json", session_cap=100.0, day_cap=0)
    t.record("x", 10000, 10000)  # spent=$100 but day_cap=0 → no cap
    r = make_router(t)
    assert r.should_run("do_intent", 0.5).run is True      # critical
    assert r.should_run("scene_brief", 0.4).run is True    # normal
