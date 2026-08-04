"""Track Haiku spend per session + per day. Persist daily total to disk."""
import asyncio
import json
import os
from datetime import date
from pathlib import Path

from ..metrics import HAIKU_IN_PER_MTOK, HAIKU_OUT_PER_MTOK
from ..paths import unity_mcp_dir
from ._filelock import locked as _filelocked

IMAGE_TOKEN_OVERHEAD = 1500


class CostTracker:
    def __init__(self, path: Path | None = None,
                 session_cap: float = 0.50, day_cap: float = 5.00):
        self._path = Path(path) if path else unity_mcp_dir() / "budget.json"
        self._session_cap = session_cap
        self._day_cap = day_cap
        self._session_spent = 0.0
        self._skipped: dict[str, int] = {}
        self._async_lock = asyncio.Lock()
        self._daily_state = self._load()

    def _load(self) -> dict:
        """Read fresh from disk under fcntl. Returns parsed state or {}."""
        try:
            with _filelocked(self._path):
                if not self._path.exists():
                    return {}
                text = self._path.read_text(encoding="utf-8")
            data = json.loads(text)
            if not isinstance(data.get("spent"), (int, float, type(None))):
                return {}
            return data
        except (OSError, json.JSONDecodeError, UnicodeDecodeError):
            return {}

    def _save(self) -> "tuple[bool, str]":
        """Atomic write with per-PID tmp + fcntl. Returns (success, reason_if_failed)."""
        try:
            self._path.parent.mkdir(parents=True, exist_ok=True)
            with _filelocked(self._path):
                # Per-PID tmp eliminates multi-writer collision on shared .tmp filename.
                # NOTE: if process crashes between write and os.replace, ~/.unity-biome-mcp/budget.tmp.<PID>
                # orphans. Cleanup intentionally omitted — files are small and rare. Manual cleanup OK.
                tmp = self._path.with_suffix(f".tmp.{os.getpid()}")
                tmp.write_text(json.dumps(self._daily_state, ensure_ascii=False), encoding="utf-8")
                os.replace(tmp, self._path)
            return (True, "")
        except OSError as e:
            return (False, f"raised:{type(e).__name__}")

    def _today(self) -> str:
        return date.today().isoformat()

    def record(self, feature: str, in_tok: int, out_tok: int,
               has_image: bool = False) -> float:
        """Record spend, return USD cost. Sync — safe without event loop."""
        actual_in = in_tok + (IMAGE_TOKEN_OVERHEAD if has_image else 0)
        usd = actual_in * HAIKU_IN_PER_MTOK / 1e6 + out_tok * HAIKU_OUT_PER_MTOK / 1e6
        self._session_spent += usd
        today = self._today()
        if self._daily_state.get("date") != today:
            self._daily_state = {"date": today, "spent": 0.0}
        self._daily_state["spent"] = (self._daily_state.get("spent") or 0.0) + usd
        success, reason = self._save()
        if not success:
            from ..metrics import METRICS
            METRICS.inc("degraded.budget_save")
            METRICS.event("degraded", feature="budget_save", step="save", reason=reason)
        return usd

    async def record_async(self, feature: str, in_tok: int, out_tok: int,
                           has_image: bool = False) -> float:
        """Async-safe record. Serializes concurrent calls via asyncio.Lock.

        Re-loads state from disk inside the lock so concurrent processes and
        day-rollover restarts see a fresh baseline (race-1 + race-2 fix).
        """
        async with self._async_lock:
            fresh = self._load()
            if fresh:
                self._daily_state = fresh
            return self.record(feature, in_tok, out_tok, has_image)

    def record_skip(self, feature: str) -> None:
        self._skipped[feature] = self._skipped.get(feature, 0) + 1

    def session_spent(self) -> float:
        return self._session_spent

    def day_spent(self) -> float:
        if self._daily_state.get("date") != self._today():
            return 0.0
        return self._daily_state.get("spent") or 0.0

    def day_cap_exceeded(self) -> bool:
        return self._day_cap > 0 and self.day_spent() >= self._day_cap

    def session_pct(self) -> float:
        return self._session_spent / self._session_cap if self._session_cap > 0 else 0.0

    def reset_session(self) -> None:
        self._session_spent = 0.0
        self._skipped.clear()

    def status(self) -> str:
        skipped_str = " ".join(f"{k}:{v}" for k, v in self._skipped.items())
        return (f"sess=${self._session_spent:.4f}/{self._session_cap:.2f} "
                f"({self.session_pct()*100:.0f}%) "
                f"day=${self.day_spent():.4f}"
                + (f" skipped={skipped_str}" if skipped_str else ""))
