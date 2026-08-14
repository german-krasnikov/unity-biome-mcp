"""T19: CheckpointStore — save, load, list, and evict checkpoint metadata."""
from __future__ import annotations

import contextlib
import json
import os
import time
from datetime import datetime, timezone
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

    from .changeset_store import ContentStore
    from .checkpoint import Checkpoint, CheckpointState


class CheckpointStore:
    def __init__(
        self,
        fingerprint: str,
        content_store: ContentStore,
        max_age_days: int = 7,
        max_bytes: int = 500 * 1024 * 1024,
        _dir: Path | None = None,
    ) -> None:
        self._fingerprint = fingerprint
        self._content_store = content_store
        self._max_age_days = max_age_days
        self._max_bytes = max_bytes
        if _dir is not None:
            self._dir = _dir
        else:
            from .paths import checkpoints_dir
            self._dir = checkpoints_dir(fingerprint)

    def _path(self, checkpoint_id: str) -> Path:
        if not checkpoint_id.replace("-", "").replace("_", "").isalnum():
            raise ValueError(f"Invalid checkpoint_id: {checkpoint_id!r}")
        return self._dir / f"{checkpoint_id}.json"

    def save(self, cp: Checkpoint) -> None:
        """Write <checkpoint_id>.json atomically (tmp + os.replace)."""
        self._dir.mkdir(parents=True, exist_ok=True)
        dest = self._path(cp.checkpoint_id)
        tmp = self._dir / f".tmp-{cp.checkpoint_id}"
        try:
            tmp.write_text(json.dumps(cp.to_dict(), separators=(",", ":")), encoding="utf-8")
            os.replace(tmp, dest)
        except OSError:
            with contextlib.suppress(OSError):
                tmp.unlink()
            raise

    def update_state(self, checkpoint_id: str, state: CheckpointState) -> None:
        """Read-modify-write state field atomically."""
        cp = self.load(checkpoint_id)
        if cp is None:
            return
        from .checkpoint import Checkpoint
        updated = Checkpoint(
            checkpoint_id=cp.checkpoint_id,
            turn_id=cp.turn_id,
            state=state,
            fingerprint=cp.fingerprint,
            manifest=cp.manifest,
            undo_group_id=cp.undo_group_id,
            domain_stamp=cp.domain_stamp,
            created_at=cp.created_at,
            finalized_at=cp.finalized_at if state not in ("restored", "failed", "expired")
                         else datetime.now(timezone.utc).isoformat(),
        )
        self.save(updated)

    def load(self, checkpoint_id: str) -> Checkpoint | None:
        """Load by id; None if missing or corrupt."""
        p = self._path(checkpoint_id)
        if not p.exists():
            return None
        try:
            from .checkpoint import Checkpoint
            return Checkpoint.from_dict(json.loads(p.read_text(encoding="utf-8")))
        except Exception:
            return None

    def list_ready(self) -> list[Checkpoint]:
        """All checkpoints in 'ready' state, newest first."""
        if not self._dir.exists():
            return []
        result = []
        for f in self._dir.glob("*.json"):
            cp = self.load(f.stem)
            if cp is not None and cp.state == "ready":
                result.append(cp)
        result.sort(key=lambda c: c.created_at, reverse=True)
        return result

    @staticmethod
    def _safe_mtime(f: Path) -> float:
        try:
            return f.stat().st_mtime
        except OSError:
            return 0.0

    @staticmethod
    def _safe_size(f: Path) -> int:
        try:
            return f.stat().st_size
        except OSError:
            return 0

    def evict(self) -> int:
        """Remove checkpoints by age or total size. Returns count evicted."""
        if not self._dir.exists():
            return 0
        files = sorted(
            (f for f in self._dir.glob("*.json")),
            key=self._safe_mtime,
        )
        cutoff = time.time() - self._max_age_days * 86400
        evicted = 0

        # Age-based eviction
        for f in list(files):
            try:
                mtime = f.stat().st_mtime
            except OSError:
                continue
            if mtime < cutoff:
                with contextlib.suppress(OSError):
                    f.unlink(missing_ok=True)
                files.remove(f)
                evicted += 1

        # Size-based eviction (oldest first, already sorted)
        total = sum(self._safe_size(f) for f in files)
        for f in files:
            if total <= self._max_bytes:
                break
            try:
                size = f.stat().st_size
                f.unlink(missing_ok=True)
                total -= size
                evicted += 1
            except OSError:
                pass

        return evicted
