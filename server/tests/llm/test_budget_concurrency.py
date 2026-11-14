"""Concurrency tests for CostTracker — race 1 (asyncio), race 2 (rollover), race 3 (multi-proc)."""
import asyncio
import json
import os
import subprocess
import sys
from datetime import date
from pathlib import Path

def _make_fake_date(target):
    """Return fake `date` class where today() returns target."""
    class FakeDate(date):
        @classmethod
        def today(cls):
            return target
    return FakeDate


async def test_concurrent_record_async_no_lost_update(tmp_path):
    """Race 1: 10 parallel record_async calls — all charges accumulate."""
    from unity_mcp.budget.cost_tracker import CostTracker

    tracker = CostTracker(path=tmp_path / "budget.json", day_cap=100.0, session_cap=100.0)

    # 10 parallel records, each $0.001
    await asyncio.gather(*[
        tracker.record_async("test", 1000, 200, has_image=False)
        for _ in range(10)
    ])

    spent = tracker.day_spent()
    # (1000 in × $0.80/Mtok) + (200 out × $4/Mtok) = $0.0008 + $0.0008 = $0.0016 each
    expected = 10 * (1000 * 0.80 / 1_000_000 + 200 * 4.0 / 1_000_000)
    assert abs(spent - expected) < 1e-6, f"lost-update: spent={spent}, expected={expected}"


def test_multi_process_no_json_corruption(tmp_path):
    """Race 3 — fcntl + per-PID tmp prevents JSON file corruption.

    NOTE: this does NOT prevent cross-process lost updates. Each subprocess has
    its own in-memory CostTracker state, so concurrent writes lose data
    relative to a single coordinator. fcntl only guarantees that the resulting
    JSON file remains valid — never partial/corrupted.

    For no-lost-update across processes, callers must use a single coordinator
    (record_async + asyncio.Lock within one process).
    """
    budget_path = tmp_path / "budget.json"
    src_path = Path(__file__).parent.parent.parent / "src"

    code = f"""
import sys
sys.path.insert(0, {str(src_path)!r})
from unity_mcp.budget.cost_tracker import CostTracker
t = CostTracker(path={str(budget_path)!r}, day_cap=100.0, session_cap=100.0)
for _ in range(20):
    t.record('test', 100, 50, has_image=False)
"""

    procs = [
        subprocess.Popen([sys.executable, "-c", code])
        for _ in range(5)
    ]
    for p in procs:
        p.wait(timeout=30)
        assert p.returncode == 0, f"subprocess {p.pid} failed"

    text = budget_path.read_text(encoding="utf-8")
    data = json.loads(text)  # must not raise — file integrity guaranteed
    assert isinstance(data.get("spent"), (int, float)), \
        f"corrupted: spent={data.get('spent')!r}"
    assert data["spent"] > 0  # atomicity only, not no-lost-update


def test_persistence_survives_process_restart(tmp_path):
    """Spent persists across CostTracker instance recreation (process restart simulation)."""
    from unity_mcp.budget.cost_tracker import CostTracker

    tracker_path = tmp_path / "budget.json"
    tracker = CostTracker(path=tracker_path, day_cap=100.0, session_cap=100.0)

    tracker.record("test", 1000, 200, has_image=False)
    spent_before = tracker.day_spent()
    assert spent_before > 0

    # Simulate process restart — new instance reads from disk
    tracker2 = CostTracker(path=tracker_path, day_cap=100.0, session_cap=100.0)
    spent_after = tracker2.day_spent()

    assert abs(spent_after - spent_before) < 1e-6, \
        f"persistence broken: before={spent_before}, after={spent_after}"


def test_day_rollover_resets_spent(tmp_path, monkeypatch):
    """Race 2 — long-running process crossing midnight: spent resets to 0 on new day."""
    from unity_mcp.budget.cost_tracker import CostTracker

    tracker = CostTracker(path=tmp_path / "budget.json", day_cap=100.0, session_cap=100.0)

    # Day N: record some spend
    fake_today = date(2026, 5, 4)
    monkeypatch.setattr("unity_mcp.budget.cost_tracker.date", _make_fake_date(fake_today))
    tracker.record("test", 1000, 200)
    spent_n = tracker.day_spent()
    assert spent_n > 0

    # Day N+1: new day → spent should reset on next record
    fake_tomorrow = date(2026, 5, 5)
    monkeypatch.setattr("unity_mcp.budget.cost_tracker.date", _make_fake_date(fake_tomorrow))
    tracker.record("test", 1000, 200)
    spent_n1 = tracker.day_spent()

    # New day's spent should be just THIS call's cost, not accumulated from yesterday
    assert spent_n1 < spent_n + 1e-6, \
        f"day rollover failed: prev_spent={spent_n}, new_day_spent={spent_n1}"
    # New day spent ≈ 1 call cost (same as spent_n, since both calls have same tokens)
    assert abs(spent_n1 - spent_n) < 1e-6, \
        f"new day should have 1 call worth, has {spent_n1} (prev was {spent_n})"


async def test_record_async_save_failure_no_exception(tmp_path, monkeypatch):
    """If _save returns (False, reason), record_async still updates in-memory state — no exception."""
    from unity_mcp.budget.cost_tracker import CostTracker

    tracker = CostTracker(path=tmp_path / "budget.json", day_cap=100.0, session_cap=100.0)
    monkeypatch.setattr(tracker, "_save", lambda: (False, "raised:OSError"))

    # Must not raise
    await tracker.record_async("test", 1000, 200, has_image=False)

    # In-memory state IS updated
    assert tracker.day_spent() > 0
