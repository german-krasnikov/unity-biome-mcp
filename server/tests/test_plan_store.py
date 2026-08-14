"""T20: PlanStore unit tests (plan_store.py)."""
from __future__ import annotations

import json
import os
import time
import uuid
from pathlib import Path


def _make_plan(state: str = "pending_review", plan_id: str | None = None):
    from unity_mcp.plan import PlanDocument, PlanStep

    return PlanDocument(
        plan_id=plan_id or str(uuid.uuid4()),
        session_id="s1",
        title="Test Plan",
        steps=(PlanStep(index=1, description="Do thing", tool_hint=None),),
        state=state,
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at=None,
        notes="",
    )


def _make_store(tmp_path: Path):
    from unity_mcp.plan_store import PlanStore

    return PlanStore(fingerprint="fp1234", _dir=tmp_path / "plans")


def test_save_and_load_roundtrip(tmp_path):
    store = _make_store(tmp_path)
    plan = _make_plan()
    store.save(plan)
    loaded = store.load(plan.plan_id)
    assert loaded == plan


def test_load_missing_returns_none(tmp_path):
    store = _make_store(tmp_path)
    assert store.load("nonexistent") is None


def test_load_corrupt_returns_none(tmp_path):
    store = _make_store(tmp_path)
    plans_dir = tmp_path / "plans"
    plans_dir.mkdir(parents=True, exist_ok=True)
    (plans_dir / "bad-id.json").write_text("{corrupt json", encoding="utf-8")
    assert store.load("bad-id") is None


def test_update_state_sets_reviewed_at(tmp_path):
    store = _make_store(tmp_path)
    plan = _make_plan(state="pending_review")
    store.save(plan)
    updated = store.update_state(plan.plan_id, "approved")
    assert updated is not None
    assert updated.state == "approved"
    assert updated.reviewed_at is not None


def test_update_state_missing_returns_none(tmp_path):
    store = _make_store(tmp_path)
    result = store.update_state("nonexistent", "approved")
    assert result is None


def test_list_active_filters_by_state(tmp_path):
    store = _make_store(tmp_path)
    pending = _make_plan(state="pending_review")
    approved = _make_plan(state="approved")
    rejected = _make_plan(state="rejected")
    for p in [pending, approved, rejected]:
        store.save(p)
    active = store.list_active()
    ids = {p.plan_id for p in active}
    assert pending.plan_id in ids
    assert approved.plan_id in ids
    assert rejected.plan_id not in ids


def test_evict_by_age(tmp_path):
    store = _make_store(tmp_path)
    plan = _make_plan()
    store.save(plan)
    plan_file = tmp_path / "plans" / f"{plan.plan_id}.json"
    old_mtime = time.time() - 8 * 86400
    os.utime(plan_file, (old_mtime, old_mtime))
    evicted = store.evict(max_age_days=7)
    assert evicted == 1
    assert store.load(plan.plan_id) is None
