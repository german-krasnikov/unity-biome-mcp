"""T19: Checkpoint, FileManifest, ConflictInfo — immutable value objects. No I/O."""

from dataclasses import dataclass
from typing import Literal

from .changeset import ContentRef  # noqa: TC001

CheckpointState = Literal["preparing", "ready", "restored", "failed", "expired"]


@dataclass(frozen=True)
class FileManifest:
    """Ordered snapshot of (path, hash16) pairs — deterministic for same file set."""

    entries: tuple[tuple[str, str], ...]

    def find(self, path: str) -> str | None:
        """Return hash16 for path, or None if not in manifest."""
        for p, h in self.entries:
            if p == path:
                return h
        return None

    def paths(self) -> list[str]:
        """Sorted list of all paths in manifest."""
        return [p for p, _ in self.entries]

    @staticmethod
    def of(path_refs: dict[str, ContentRef]) -> FileManifest:
        """Build FileManifest from {path: ContentRef}; sorted by path."""
        entries = tuple(sorted((p, ref.hash16) for p, ref in path_refs.items()))
        return FileManifest(entries=entries)


@dataclass(frozen=True)
class Checkpoint:
    checkpoint_id: str         # uuid4
    turn_id: int
    state: CheckpointState
    fingerprint: str           # sha256(project_id)[:12]
    manifest: FileManifest     # path → before_ref hash16
    undo_group_id: int         # -1 if no Unity Undo group
    domain_stamp: str          # SyncHelper.CurrentDomainStamp at create time
    created_at: str            # ISO 8601 UTC
    finalized_at: str | None

    def to_dict(self) -> dict:
        return {
            "checkpoint_id": self.checkpoint_id,
            "turn_id": self.turn_id,
            "state": self.state,
            "fingerprint": self.fingerprint,
            "manifest": [list(e) for e in self.manifest.entries],
            "undo_group_id": self.undo_group_id,
            "domain_stamp": self.domain_stamp,
            "created_at": self.created_at,
            "finalized_at": self.finalized_at,
        }

    @staticmethod
    def from_dict(d: dict) -> Checkpoint:
        return Checkpoint(
            checkpoint_id=d["checkpoint_id"],
            turn_id=d["turn_id"],
            state=d["state"],
            fingerprint=d["fingerprint"],
            manifest=FileManifest(entries=tuple(tuple(e) for e in d["manifest"])),
            undo_group_id=d["undo_group_id"],
            domain_stamp=d["domain_stamp"],
            created_at=d["created_at"],
            finalized_at=d.get("finalized_at"),
        )


@dataclass(frozen=True)
class ConflictInfo:
    path: str
    expected_hash: str   # after_ref — what we expect post-agent-turn
    actual_hash: str     # current on-disk hash
