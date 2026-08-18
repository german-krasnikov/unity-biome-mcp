"""T21: AttachmentKind, AttachmentSlot, ContextBrief — immutable value objects. No I/O."""

import hashlib
from dataclasses import dataclass
from typing import Literal

AttachmentKind = Literal["console", "hierarchy", "compile_errors", "selection", "profiler"]
Priority = Literal["critical", "medium", "low"]

PRIORITY_RANK: dict[str, int] = {"critical": 0, "medium": 1, "low": 2}

_KIND_PRIORITY: dict[str, str] = {
    "compile_errors": "critical",
    "console": "critical",
    "hierarchy": "medium",
    "selection": "low",
    "profiler": "medium",
}

_SECTION_LABELS: dict[str, str] = {
    "compile_errors": "Compile",
    "console": "Console",
    "hierarchy": "Hierarchy",
    "selection": "Selection",
    "profiler": "Profiler",
}


def _estimate_tokens(text: str) -> int:
    return len(text) // 4 + 1


def _truncate(text: str, budget_tokens: int) -> tuple[str, bool]:
    if budget_tokens <= 0:
        return ("", False)
    max_chars = budget_tokens * 4
    if len(text) <= max_chars:
        return text, False
    cut = text[:max_chars]
    last_nl = cut.rfind("\n")
    if last_nl > 0:
        cut = cut[:last_nl]
    return cut + "\n…(truncated)", True


@dataclass(frozen=True)
class AttachmentSlot:
    kind: AttachmentKind
    content: str
    used_tokens: int
    truncated: bool

    @staticmethod
    def of(kind: AttachmentKind, content: str, budget_tokens: int) -> AttachmentSlot:
        truncated_content, was_truncated = _truncate(content, budget_tokens)
        return AttachmentSlot(
            kind=kind,
            content=truncated_content,
            used_tokens=_estimate_tokens(truncated_content),
            truncated=was_truncated,
        )


@dataclass(frozen=True)
class ContextBrief:
    slots: tuple[AttachmentSlot, ...]
    total_tokens: int
    budget: int
    content_hash: str  # sha256(joined_non_empty)[:12]

    def to_text(self) -> str:
        header = f"[Project Brief]  hash={self.content_hash}  tokens={self.total_tokens}/{self.budget}"
        sorted_slots = sorted(
            self.slots,
            key=lambda s: (PRIORITY_RANK.get(_KIND_PRIORITY.get(s.kind, "low"), 2), s.kind),
        )
        lines = [header, ""]
        for slot in sorted_slots:
            if not slot.content:
                continue
            label = _SECTION_LABELS.get(slot.kind, slot.kind.title())
            lines.append(f"[{label}]")
            lines.append(slot.content)
            lines.append("")
        return "\n".join(lines).rstrip()

    @staticmethod
    def of(slots: list[AttachmentSlot], budget: int) -> ContextBrief:
        non_empty = [s for s in slots if s.content]
        total = sum(s.used_tokens for s in non_empty)
        joined = "\n".join(s.content for s in non_empty)
        content_hash = hashlib.sha256(joined.encode()).hexdigest()[:12]
        return ContextBrief(
            slots=tuple(slots),
            total_tokens=total,
            budget=budget,
            content_hash=content_hash,
        )
