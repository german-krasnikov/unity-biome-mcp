"""History-First Disambiguator — silent path resolution from session history.

When fuzzy resolution finds >1 candidate, score by recency/taint/edit-dist/mutation,
auto-resolve if margin >= 2. Else block with candidate listing.

# TODO(Cycle 5b): wire to resolve_path_live in middleware.py — when candidates > 1,
# call Disambiguator.decide(query, candidates); on None block with format_block().
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import TYPE_CHECKING

from .utils import _levenshtein

if TYPE_CHECKING:
    from collections import deque


@dataclass
class Candidate:
    path: str
    score: int = 0
    reasons: list[str] = field(default_factory=list)


class Disambiguator:
    MARGIN_THRESHOLD = 2

    def __init__(self, recent_paths: list[str], clean_paths: set[str], mutation_log: deque):
        self._recent = list(recent_paths)
        self._clean = set(clean_paths)
        self._mutations = list(mutation_log)

    def rank(self, query: str, candidates: list[str]) -> list[Candidate]:
        """Score candidates. Higher = better match."""
        ranked = []
        for c in candidates:
            cand = Candidate(path=c)
            if c in self._recent:
                cand.score += 3
                cand.reasons.append("recency")
            if c in self._clean:
                cand.score += 2
                cand.reasons.append("taint")
            leaf = c.rsplit("/", 1)[-1] if "/" in c else c
            dist = _levenshtein(query, leaf)
            if dist <= 2:
                cand.score += 1
                cand.reasons.append(f"lev<={dist}")
            if c in self._mutations:
                cand.score += 1
                cand.reasons.append("mutation")
            ranked.append(cand)
        ranked.sort(key=lambda x: -x.score)
        return ranked

    def decide(self, query: str, candidates: list[str]) -> tuple[str, str] | None:
        """Returns (chosen_path, marker) on auto-resolve, None on block."""
        if not candidates:
            return None
        if len(candidates) == 1:
            return candidates[0], ""
        ranked = self.rank(query, candidates)
        if len(ranked) >= 2 and ranked[0].score >= ranked[1].score + self.MARGIN_THRESHOLD:
            top = ranked[0]
            top_reason = top.reasons[0] if top.reasons else "score"
            return top.path, f"[RESOLVED: {query}→{top.path} via {top_reason}]"
        return None  # block

    def format_block(self, query: str, candidates: list[str]) -> str:
        """Format candidate list when no auto-resolve possible."""
        ranked = self.rank(query, candidates)[:3]
        lines = [f"[AMBIGUOUS: '{query}' matches {len(candidates)} paths]"]
        for c in ranked:
            reasons = ", ".join(c.reasons) if c.reasons else "no signal"
            lines.append(f"  - {c.path} (score={c.score}, {reasons})")
        lines.append("[BYPASS: pass _explicit_path=true to skip]")
        return "\n".join(lines)
