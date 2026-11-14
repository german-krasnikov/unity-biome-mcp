"""TDD: CostTracker — USD spend tracking + persistence."""
import json
from pathlib import Path
from unity_mcp.budget.cost_tracker import CostTracker, HAIKU_IN_PER_MTOK, HAIKU_OUT_PER_MTOK, IMAGE_TOKEN_OVERHEAD


def make_tracker(tmp_path, session_cap=1.0, day_cap=5.0):
    return CostTracker(path=tmp_path / "budget.json", session_cap=session_cap, day_cap=day_cap)


def test_record_calculates_usd_correctly(tmp_path):
    t = make_tracker(tmp_path)
    cost = t.record("do_intent", in_tok=1000, out_tok=500)
    expected = 1000 * HAIKU_IN_PER_MTOK / 1e6 + 500 * HAIKU_OUT_PER_MTOK / 1e6
    assert abs(cost - expected) < 1e-10


def test_image_adds_overhead(tmp_path):
    t = make_tracker(tmp_path)
    cost_no_image = t.record("visual_verify", 400, 100, has_image=False)
    t2 = make_tracker(tmp_path / "t2")
    cost_with_image = t2.record("visual_verify", 400, 100, has_image=True)
    overhead = IMAGE_TOKEN_OVERHEAD * HAIKU_IN_PER_MTOK / 1e6
    assert abs(cost_with_image - cost_no_image - overhead) < 1e-10


def test_session_spent_accumulates(tmp_path):
    t = make_tracker(tmp_path)
    c1 = t.record("a", 1000, 100)
    c2 = t.record("b", 500, 200)
    assert abs(t.session_spent() - (c1 + c2)) < 1e-12


def test_day_spent_starts_zero(tmp_path):
    t = make_tracker(tmp_path)
    assert t.day_spent() == 0.0


def test_day_spent_accumulates(tmp_path):
    t = make_tracker(tmp_path)
    c1 = t.record("a", 1000, 100)
    c2 = t.record("b", 500, 50)
    assert abs(t.day_spent() - (c1 + c2)) < 1e-12


def test_day_rollover_resets_daily(tmp_path):
    budget_file = tmp_path / "budget.json"
    # Simulate yesterday's data in file
    budget_file.write_text(json.dumps({"date": "2000-01-01", "spent": 99.99}), encoding="utf-8")
    t = CostTracker(path=budget_file, session_cap=1.0, day_cap=5.0)
    assert t.day_spent() == 0.0


def test_persistence_survives_reload(tmp_path):
    budget_file = tmp_path / "budget.json"
    t1 = CostTracker(path=budget_file)
    t1.record("a", 2000, 300)
    spent = t1.day_spent()

    t2 = CostTracker(path=budget_file)
    assert abs(t2.day_spent() - spent) < 1e-12


def test_corrupt_json_starts_fresh(tmp_path):
    budget_file = tmp_path / "budget.json"
    budget_file.write_text("not valid json{{", encoding="utf-8")
    t = CostTracker(path=budget_file)
    assert t.day_spent() == 0.0
    assert t.session_spent() == 0.0


def test_session_pct_above_one_when_over(tmp_path):
    t = CostTracker(path=tmp_path / "b.json", session_cap=0.000001)
    t.record("a", 1000, 1000)
    assert t.session_pct() > 1.0


def test_record_skip_counts(tmp_path):
    t = make_tracker(tmp_path)
    t.record_skip("watchdog")
    t.record_skip("watchdog")
    t.record_skip("summarize")
    status = t.status()
    assert "watchdog:2" in status
    assert "summarize:1" in status


def test_status_format(tmp_path):
    t = make_tracker(tmp_path, session_cap=1.0)
    t.record("a", 1000, 100)
    s = t.status()
    assert "sess=$" in s
    assert "day=$" in s
    assert "%" in s


def test_load_corrupted_spent_field(tmp_path):
    from datetime import date
    budget_file = tmp_path / "budget.json"
    budget_file.write_text(json.dumps({"date": date.today().isoformat(), "spent": "bad"}), encoding="utf-8")
    t = CostTracker(path=budget_file)
    assert t.day_spent() == 0.0
    cost = t.record("do_intent", 1000, 500)  # must not crash
    assert cost > 0


# ── degrade() wiring: _save() returns bool ────────────────────────────────────

def test_budget_save_failure_emits_event(tmp_path, monkeypatch):
    """#3: os.replace raises PermissionError → METRICS.event called with feature=budget_save."""
    from unity_mcp.metrics import METRICS
    METRICS.reset()

    events = []
    original_event = METRICS.event

    def capture_event(kind, **fields):
        events.append({"kind": kind, **fields})

    METRICS.event = capture_event
    try:
        t = CostTracker(path=tmp_path / "budget.json")
        monkeypatch.setattr("os.replace", lambda *a, **kw: (_ for _ in ()).throw(PermissionError("denied")))

        t.record("test_feat", 1000, 500)

        budget_events = [e for e in events if e.get("feature") == "budget_save"]
        assert len(budget_events) == 1, f"Expected 1 budget_save event, got {events}"
        assert budget_events[0]["kind"] == "degraded"
        assert "reason" in budget_events[0]
        assert "PermissionError" in budget_events[0]["reason"]
    finally:
        METRICS.event = original_event


def test_null_spent_in_file_does_not_crash(tmp_path):
    """P0-4: persisted null spent must not cause TypeError on record()."""
    from datetime import date
    f = tmp_path / "budget.json"
    f.write_text('{"date": "' + date.today().isoformat() + '", "spent": null}', encoding="utf-8")
    tracker = CostTracker(path=f)
    assert tracker.day_spent() == 0.0  # null loaded as 0
    tracker.record("screenshot_describe", in_tok=100, out_tok=50, has_image=False)  # must not crash


def test_budget_save_failure_increments_metric(tmp_path, monkeypatch):
    """os.replace raises OSError → record() completes, degraded.budget_save==1."""
    from unity_mcp.metrics import METRICS
    METRICS.reset()

    t = CostTracker(path=tmp_path / "budget.json")
    monkeypatch.setattr("os.replace", lambda *a, **kw: (_ for _ in ()).throw(OSError("disk full")))

    cost = t.record("test_feat", 1000, 500)
    assert cost > 0  # in-memory state updated
    assert t.session_spent() == cost  # session still tracked
    assert METRICS.snapshot()["counters"].get("degraded.budget_save", 0) == 1
