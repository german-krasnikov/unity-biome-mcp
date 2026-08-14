"""T20: PlanStore — save, load, list, and evict plan documents."""
from __future__ import annotations

import contextlib
import json
import os
import time
from datetime import datetime, timezone
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

    from .plan import PlanDocument, PlanState


class PlanStore:
    def __init__(self, fingerprint: str, _dir: Path | None = None) -> None:
        self._fingerprint = fingerprint
        if _dir is not None:
            self._dir = _dir
        else:
            from .paths import plans_dir
            self._dir = plans_dir(fingerprint)

    def _path(self, plan_id: str) -> Path:
        return self._dir / f"{plan_id}.json"

    def save(self, plan: PlanDocument) -> None:
        """Write <plan_id>.json atomically (tmp + os.replace)."""
        self._dir.mkdir(parents=True, exist_ok=True)
        dest = self._path(plan.plan_id)
        tmp = self._dir / f".tmp-{plan.plan_id}"
        try:
            tmp.write_text(json.dumps(plan.to_dict(), separators=(",", ":")), encoding="utf-8")
            os.replace(tmp, dest)
        except OSError:
            with contextlib.suppress(OSError):
                tmp.unlink()
            raise

    def load(self, plan_id: str) -> PlanDocument | None:
        """Load by id; None if missing or corrupt."""
        p = self._path(plan_id)
        if not p.exists():
            return None
        try:
            from .plan import PlanDocument
            return PlanDocument.from_dict(json.loads(p.read_text(encoding="utf-8")))
        except Exception:
            return None

    def update_state(
        self, plan_id: str, state: PlanState, notes: str = ""
    ) -> PlanDocument | None:
        """Read-modify-write state and notes; returns updated plan or None if missing."""
        plan = self.load(plan_id)
        if plan is None:
            return None
        from .plan import PlanDocument
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
        self.save(updated)
        return updated

    def list_active(self) -> list[PlanDocument]:
        """Plans in pending_review or approved state, newest first."""
        if not self._dir.exists():
            return []
        result = []
        for f in self._dir.glob("*.json"):
            plan = self.load(f.stem)
            if plan is not None and plan.state in ("pending_review", "approved"):
                result.append(plan)
        result.sort(key=lambda p: p.created_at, reverse=True)
        return result

    def evict(self, max_age_days: int = 7) -> int:
        """Remove plan files older than max_age_days. Returns count evicted."""
        if not self._dir.exists():
            return 0
        cutoff = time.time() - max_age_days * 86400
        evicted = 0
        for f in self._dir.glob("*.json"):
            with contextlib.suppress(OSError):
                if f.stat().st_mtime < cutoff:
                    f.unlink(missing_ok=True)
                    evicted += 1
        return evicted


_plan_store: PlanStore | None = None


def get_plan_store() -> PlanStore | None:
    return _plan_store


def init_plan_store(fingerprint: str) -> PlanStore:
    global _plan_store
    _plan_store = PlanStore(fingerprint)
    return _plan_store
