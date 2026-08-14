"""T20: plan_create, plan_approve, plan_reject, plan_edit, plan_status MCP tools."""
from __future__ import annotations

import re
import uuid
from datetime import datetime, timezone

from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None
_active_plan_id: str | None = None

_TOOL_RE = re.compile(r"^tool:(\S+)\s+(.*)")
_NUM_RE = re.compile(r"^\d+\.\s*")


def _get_store():
    from ..plan_store import get_plan_store
    store = get_plan_store()
    if store is None:
        raise RuntimeError("PlanStore not initialized — call init_plan_store() first")
    return store


def _resolve_plan_id(plan_id: str) -> str | None:
    return plan_id if plan_id else _active_plan_id


def _parse_steps(text: str) -> tuple:
    from ..plan import PlanStep
    steps = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            continue
        tool_hint = None
        m = _TOOL_RE.match(line)
        if m:
            tool_hint = m.group(1)
            line = m.group(2)
        line = _NUM_RE.sub("", line).strip()
        if line:
            steps.append(PlanStep(index=len(steps) + 1, description=line, tool_hint=tool_hint))
    return tuple(steps)


async def plan_create(title: str, steps: str, session_id: str = "") -> str:
    """Use when you need user approval before executing a multi-step plan.
    Creates a plan in pending_review state. steps: one step per line,
    optional 'tool:name' prefix. Recommend under 20 steps; split larger plans."""
    global _active_plan_id
    from ..plan import PlanDocument
    parsed = _parse_steps(steps)
    store = _get_store()
    plan = PlanDocument(
        plan_id=str(uuid.uuid4()),
        session_id=session_id,
        title=title,
        steps=parsed,
        state="pending_review",
        created_at=datetime.now(timezone.utc).isoformat(),
        reviewed_at=None,
        notes="",
    )
    store.save(plan)
    _active_plan_id = plan.plan_id
    return f"plan_id={plan.plan_id}  state=pending_review  steps={len(parsed)}"


async def plan_approve(plan_id: str = "") -> str:
    """Use when the user has approved a pending_review plan.
    Transitions plan to approved state. plan_id defaults to the last created plan."""
    resolved = _resolve_plan_id(plan_id)
    if not resolved:
        return "err: no_active_plan"
    store = _get_store()
    plan = store.load(resolved)
    if plan is None:
        return f"err: plan_not_found  plan_id={resolved}"
    if plan.state != "pending_review":
        return f"err: invalid_transition  current={plan.state}  requested=approved"
    updated = store.update_state(resolved, "approved")
    if updated is None:
        return f"err: plan_not_found  plan_id={resolved}"
    return f"plan_id={updated.plan_id}  state=approved  reviewed_at={updated.reviewed_at}"


async def plan_reject(plan_id: str = "", reason: str = "") -> str:
    """Use when the user has rejected a pending_review plan.
    Transitions plan to rejected state. reason is stored in notes."""
    resolved = _resolve_plan_id(plan_id)
    if not resolved:
        return "err: no_active_plan"
    store = _get_store()
    plan = store.load(resolved)
    if plan is None:
        return f"err: plan_not_found  plan_id={resolved}"
    if plan.state != "pending_review":
        return f"err: invalid_transition  current={plan.state}  requested=rejected"
    updated = store.update_state(resolved, "rejected", notes=reason)
    if updated is None:
        return f"err: plan_not_found  plan_id={resolved}"
    return f"plan_id={updated.plan_id}  state=rejected  reviewed_at={updated.reviewed_at}"


async def plan_edit(plan_id: str = "", steps: str = "") -> str:
    """Use when you need to revise a plan after feedback.
    Replaces steps and restores plan to pending_review state."""
    resolved = _resolve_plan_id(plan_id)
    if not resolved:
        return "err: no_active_plan"
    store = _get_store()
    plan = store.load(resolved)
    if plan is None:
        return f"err: plan_not_found  plan_id={resolved}"
    if plan.state not in ("pending_review", "approved"):
        return f"err: invalid_transition  current={plan.state}  requested=pending_review"
    from ..plan import PlanDocument
    parsed = _parse_steps(steps)
    updated = PlanDocument(
        plan_id=plan.plan_id,
        session_id=plan.session_id,
        title=plan.title,
        steps=parsed,
        state="pending_review",
        created_at=plan.created_at,
        reviewed_at=None,
        notes="",
    )
    store.save(updated)
    return f"plan_id={updated.plan_id}  state=pending_review  steps={len(parsed)}"


async def plan_status(plan_id: str = "") -> str:
    """Use to check the current state and steps of a plan before executing.
    Returns state and numbered steps with optional tool hints."""
    resolved = _resolve_plan_id(plan_id)
    if not resolved:
        return "err: no_active_plan"
    store = _get_store()
    plan = store.load(resolved)
    if plan is None:
        return f"err: plan_not_found  plan_id={resolved}"
    lines = [f"plan_id={plan.plan_id}  state={plan.state}  steps={len(plan.steps)}"]
    for step in plan.steps:
        hint = f" [tool:{step.tool_hint}]" if step.tool_hint else ""
        lines.append(f"{step.index}. {step.description}{hint}")
    return "\n".join(lines)


def register(mcp, send, args) -> None:
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(plan_create)
    mcp.tool(annotations=_RW)(plan_approve)
    mcp.tool(annotations=_RW)(plan_reject)
    mcp.tool(annotations=_RW)(plan_edit)
    mcp.tool(annotations=_RO)(plan_status)
