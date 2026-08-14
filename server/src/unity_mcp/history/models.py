"""T23: ConversationHeader value object + EXCLUDED_KINDS."""
from __future__ import annotations

from dataclasses import dataclass

EXCLUDED_KINDS: frozenset[str] = frozenset({"heartbeat", "cost_update"})


@dataclass(frozen=True)
class ConversationHeader:
    conv_id:     str
    title:       str
    created_at:  str
    updated_at:  str
    backend:     str
    session_id:  str
    turn_count:  int
    fingerprint: str

    def to_dict(self) -> dict:
        return {
            "v":           1,
            "id":          self.conv_id,
            "title":       self.title,
            "created_at":  self.created_at,
            "updated_at":  self.updated_at,
            "backend":     self.backend,
            "session_id":  self.session_id,
            "turn_count":  self.turn_count,
            "fingerprint": self.fingerprint,
        }

    @staticmethod
    def from_dict(d: dict) -> ConversationHeader:
        return ConversationHeader(
            conv_id     = d["id"],
            title       = d.get("title", ""),
            created_at  = d.get("created_at", ""),
            updated_at  = d.get("updated_at", ""),
            backend     = d.get("backend", ""),
            session_id  = d.get("session_id", ""),
            turn_count  = int(d.get("turn_count", 0)),
            fingerprint = d.get("fingerprint", ""),
        )
