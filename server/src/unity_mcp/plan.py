"""T20: PlanStep, PlanDocument, PlanState — immutable value objects. No I/O."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Literal, get_args

PlanState = Literal["pending_review", "approved", "rejected"]
_VALID_STATES: frozenset[str] = frozenset(get_args(PlanState))


@dataclass(frozen=True)
class PlanStep:
    index: int
    description: str
    tool_hint: str | None

    def to_dict(self) -> dict:
        return {"index": self.index, "description": self.description, "tool_hint": self.tool_hint}

    @staticmethod
    def from_dict(d: dict) -> PlanStep:
        return PlanStep(index=d["index"], description=d["description"], tool_hint=d.get("tool_hint"))


@dataclass(frozen=True)
class PlanDocument:
    plan_id: str
    session_id: str
    title: str
    steps: tuple[PlanStep, ...]
    state: PlanState
    created_at: str
    reviewed_at: str | None
    notes: str

    def __post_init__(self) -> None:
        if self.state not in _VALID_STATES:
            raise ValueError(f"Invalid PlanState: {self.state!r}")

    def to_dict(self) -> dict:
        return {
            "plan_id": self.plan_id,
            "session_id": self.session_id,
            "title": self.title,
            "steps": [s.to_dict() for s in self.steps],
            "state": self.state,
            "created_at": self.created_at,
            "reviewed_at": self.reviewed_at,
            "notes": self.notes,
        }

    @staticmethod
    def from_dict(d: dict) -> PlanDocument:
        return PlanDocument(
            plan_id=d["plan_id"],
            session_id=d.get("session_id", ""),
            title=d["title"],
            steps=tuple(PlanStep.from_dict(s) for s in d.get("steps", [])),
            state=d["state"],
            created_at=d["created_at"],
            reviewed_at=d.get("reviewed_at"),
            notes=d.get("notes", ""),
        )
