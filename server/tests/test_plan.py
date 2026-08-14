"""T20: PlanDocument value objects unit tests (plan.py)."""
from __future__ import annotations

import dataclasses

import pytest


def test_plan_step_frozen():
    from unity_mcp.plan import PlanStep

    step = PlanStep(index=1, description="Do thing", tool_hint=None)
    with pytest.raises((AttributeError, dataclasses.FrozenInstanceError)):
        step.index = 2  # type: ignore[misc]


def test_plan_doc_roundtrip():
    from unity_mcp.plan import PlanDocument, PlanStep

    plan = PlanDocument(
        plan_id="abc123",
        session_id="sess1",
        title="My Plan",
        steps=(PlanStep(index=1, description="Step one", tool_hint="create_object"),),
        state="pending_review",
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at=None,
        notes="",
    )
    d = plan.to_dict()
    plan2 = PlanDocument.from_dict(d)
    assert plan == plan2


def test_plan_doc_empty_steps():
    from unity_mcp.plan import PlanDocument

    plan = PlanDocument(
        plan_id="empty",
        session_id="",
        title="Empty",
        steps=(),
        state="pending_review",
        created_at="2026-08-14T00:00:00+00:00",
        reviewed_at=None,
        notes="",
    )
    assert len(plan.steps) == 0
    plan2 = PlanDocument.from_dict(plan.to_dict())
    assert plan == plan2


def test_plan_doc_state_literals():
    from unity_mcp.plan import PlanDocument

    with pytest.raises((ValueError, TypeError)):
        PlanDocument(
            plan_id="x",
            session_id="",
            title="Bad",
            steps=(),
            state="invalid_state",  # type: ignore[arg-type]
            created_at="2026-08-14T00:00:00+00:00",
            reviewed_at=None,
            notes="",
        )
