"""T20: plan tool handler unit tests (no Unity, no TCP)."""
from __future__ import annotations

import uuid
from datetime import datetime, timezone

import pytest


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


class MockPlanStore:
    def __init__(self):
        self._plans: dict = {}

    def save(self, plan) -> None:
        self._plans[plan.plan_id] = plan

    def load(self, plan_id: str):
        return self._plans.get(plan_id)

    def update_state(self, plan_id: str, state: str, notes: str = ""):
        plan = self._plans.get(plan_id)
        if plan is None:
            return None
        from unity_mcp.plan import PlanDocument
        reviewed_at = (
            datetime.now(timezone.utc).isoformat()
            if state in ("approved", "rejected")
            else plan.reviewed_at
        )
        updated = PlanDocument(
            plan_id=plan.plan_id,
            session_id=plan.session_id,
            title=plan.title,
            steps=plan.steps,
            state=state,
            created_at=plan.created_at,
            reviewed_at=reviewed_at,
            notes=notes,
        )
        self._plans[plan_id] = updated
        return updated


@pytest.fixture(autouse=True)
def mock_plan_store(monkeypatch):
    """Replace plan_store._plan_store with an in-memory mock; reset active plan id."""
    store = MockPlanStore()
    import unity_mcp.plan_store as ps
    import unity_mcp.tools.plan_tool as pt
    monkeypatch.setattr(ps, "_plan_store", store)
    monkeypatch.setattr(pt, "_active_plan_id", None)
    return store


@pytest.mark.asyncio
async def test_plan_create_output_format():
    from unity_mcp.tools import plan_tool

    result = await plan_tool.plan_create("My Plan", "1. Step one\n2. Step two")
    assert "plan_id=" in result
    assert "state=pending_review" in result
    assert "steps=2" in result


@pytest.mark.asyncio
async def test_plan_create_parses_steps(mock_plan_store):
    from unity_mcp.tools import plan_tool

    await plan_tool.plan_create("Plan", "tool:create_object 1. Create root\n2. Simple step")
    plan_id = plan_tool._active_plan_id
    assert plan_id is not None
    plan = mock_plan_store.load(plan_id)
    assert len(plan.steps) == 2
    assert plan.steps[0].tool_hint == "create_object"
    assert plan.steps[0].description == "Create root"
    assert plan.steps[1].tool_hint is None
    assert plan.steps[1].description == "Simple step"


@pytest.mark.asyncio
async def test_plan_create_sets_active_plan():
    from unity_mcp.tools import plan_tool

    assert plan_tool._active_plan_id is None
    await plan_tool.plan_create("Plan", "1. Step")
    assert plan_tool._active_plan_id is not None


@pytest.mark.asyncio
async def test_plan_approve_transitions_state(mock_plan_store):
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="pending_review")
    mock_plan_store.save(plan)
    result = await plan_tool.plan_approve(plan_id=plan.plan_id)
    assert "state=approved" in result
    assert plan.plan_id in result


@pytest.mark.asyncio
async def test_plan_approve_invalid_transition_returns_err(mock_plan_store):
    from unity_mcp.plan import PlanDocument, PlanStep

    plan = PlanDocument(
        plan_id=str(uuid.uuid4()),
        session_id="",
        title="T",
        steps=(PlanStep(index=1, description="s", tool_hint=None),),
        state="approved",
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at="2026-08-14T01:00:00+00:00",
        notes="",
    )
    mock_plan_store._plans[plan.plan_id] = plan
    from unity_mcp.tools import plan_tool

    result = await plan_tool.plan_approve(plan_id=plan.plan_id)
    assert result.startswith("err:")
    assert "invalid_transition" in result


@pytest.mark.asyncio
async def test_plan_reject_with_reason(mock_plan_store):
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="pending_review")
    mock_plan_store.save(plan)
    result = await plan_tool.plan_reject(plan_id=plan.plan_id, reason="Not ready")
    assert "state=rejected" in result
    stored = mock_plan_store.load(plan.plan_id)
    assert stored.notes == "Not ready"


@pytest.mark.asyncio
async def test_plan_edit_replaces_steps(mock_plan_store):
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="pending_review")
    mock_plan_store.save(plan)
    result = await plan_tool.plan_edit(plan_id=plan.plan_id, steps="1. New step A\n2. New step B")
    assert "state=pending_review" in result
    assert "steps=2" in result


@pytest.mark.asyncio
async def test_plan_edit_wrong_state_returns_err(mock_plan_store):
    from unity_mcp.plan import PlanDocument, PlanStep

    plan = PlanDocument(
        plan_id=str(uuid.uuid4()),
        session_id="",
        title="T",
        steps=(PlanStep(index=1, description="s", tool_hint=None),),
        state="rejected",
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at="2026-08-14T01:00:00+00:00",
        notes="",
    )
    mock_plan_store._plans[plan.plan_id] = plan
    from unity_mcp.tools import plan_tool

    result = await plan_tool.plan_edit(plan_id=plan.plan_id, steps="1. New step")
    assert result.startswith("err:")
    assert "invalid_transition" in result


@pytest.mark.asyncio
async def test_plan_status_by_explicit_id(mock_plan_store):
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="approved")
    mock_plan_store.save(plan)
    result = await plan_tool.plan_status(plan_id=plan.plan_id)
    assert f"plan_id={plan.plan_id}" in result
    assert "state=approved" in result


@pytest.mark.asyncio
async def test_plan_status_uses_active_plan_id(mock_plan_store):
    from unity_mcp.tools import plan_tool

    await plan_tool.plan_create("Plan", "1. step")
    result = await plan_tool.plan_status(plan_id="")
    assert "plan_id=" in result
    assert "state=pending_review" in result


@pytest.mark.asyncio
async def test_plan_status_no_active_plan_returns_err():
    from unity_mcp.tools import plan_tool

    result = await plan_tool.plan_status(plan_id="")
    assert result.startswith("err:")


@pytest.mark.asyncio
async def test_plan_status_includes_steps_in_output(mock_plan_store):
    from unity_mcp.plan import PlanDocument, PlanStep

    plan = PlanDocument(
        plan_id=str(uuid.uuid4()),
        session_id="",
        title="T",
        steps=(
            PlanStep(index=1, description="Audit hierarchy", tool_hint=None),
            PlanStep(index=2, description="Create object", tool_hint="create_object"),
        ),
        state="pending_review",
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at=None,
        notes="",
    )
    mock_plan_store._plans[plan.plan_id] = plan
    from unity_mcp.tools import plan_tool

    result = await plan_tool.plan_status(plan_id=plan.plan_id)
    assert "Audit hierarchy" in result
    assert "Create object" in result
    assert "tool:create_object" in result


@pytest.mark.asyncio
async def test_plan_approve_race_deletion_returns_err(mock_plan_store):
    """If plan is deleted between load() and update_state(), returns err."""
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="pending_review")
    mock_plan_store.save(plan)

    orig_update = mock_plan_store.update_state

    def _update_returns_none(plan_id, state, notes=""):
        del mock_plan_store._plans[plan_id]
        return orig_update(plan_id, state, notes)

    mock_plan_store.update_state = _update_returns_none
    result = await plan_tool.plan_approve(plan_id=plan.plan_id)
    assert result.startswith("err:")
    assert "plan_not_found" in result


@pytest.mark.asyncio
async def test_plan_reject_race_deletion_returns_err(mock_plan_store):
    """If plan is deleted between load() and update_state(), returns err."""
    from unity_mcp.tools import plan_tool

    plan = _make_plan(state="pending_review")
    mock_plan_store.save(plan)

    orig_update = mock_plan_store.update_state

    def _update_returns_none(plan_id, state, notes=""):
        del mock_plan_store._plans[plan_id]
        return orig_update(plan_id, state, notes)

    mock_plan_store.update_state = _update_returns_none
    result = await plan_tool.plan_reject(plan_id=plan.plan_id, reason="nope")
    assert result.startswith("err:")
    assert "plan_not_found" in result
