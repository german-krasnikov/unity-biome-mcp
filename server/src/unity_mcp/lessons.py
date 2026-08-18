"""Cross-session memory: store error patterns and provide hints.

Enable with UNITY_MCP_LESSONS=1.
Storage: ~/.unity-biome-mcp/lessons.json
"""
import hashlib
import json
import os
import time
from dataclasses import asdict, dataclass
from pathlib import Path  # noqa: TC003


@dataclass
class Lesson:
    sig: str
    cmd: str
    pattern: str
    error: str
    fix: str
    hits: int
    last_seen: float


def _sig(cmd: str, args: dict) -> str:
    return hashlib.md5(f"{cmd}:{json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]


class LessonStore:
    def __init__(self, path: Path, max_lessons: int = 200):
        self.path = path
        self._max = max_lessons
        self._lessons: dict[str, Lesson] = {}
        self.load()

    def load(self) -> None:
        try:
            data = json.loads(self.path.read_text(encoding="utf-8", errors="replace"))
            self._lessons = {k: Lesson(**v) for k, v in data.items()}
        except Exception:
            self._lessons = {}

    def flush(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        tmp = self.path.with_suffix(f".tmp.{os.getpid()}")
        tmp.write_text(json.dumps({k: asdict(v) for k, v in self._lessons.items()}, ensure_ascii=False), encoding="utf-8")
        tmp.replace(self.path)

    def add(self, lesson: Lesson) -> None:
        existing = self._lessons.get(lesson.sig)
        if existing:
            existing.hits = lesson.hits
            existing.last_seen = lesson.last_seen
        else:
            self._lessons[lesson.sig] = lesson
            if len(self._lessons) > self._max:
                victim = min(self._lessons.items(), key=lambda kv: kv[1].last_seen)[0]
                del self._lessons[victim]

    def find_by_sig(self, sig: str) -> Lesson | None:
        return self._lessons.get(sig)

    def hint_for(self, cmd: str, args: dict) -> str | None:
        from .metrics import METRICS
        sig = _sig(cmd, args)
        lesson = self._lessons.get(sig)
        if lesson and lesson.hits >= 2:
            METRICS.inc("lessons.hint_emitted")
            return f"LESSON: {lesson.pattern} → {lesson.error}. Try: {lesson.fix}"
        return None


class LessonRecorder:
    """Detects retry chains: 3+ identical failures → emit lesson."""

    def __init__(self, store: LessonStore):
        self._store = store
        self._recent_fails: dict[str, tuple[int, float]] = {}  # sig → (count, last_ts)

    def record(self, cmd: str, args: dict, result: str, ok: bool) -> None:
        sig = _sig(cmd, args)
        now = time.time()
        # Prune stale fails older than 1h
        self._recent_fails = {k: v for k, v in self._recent_fails.items() if now - v[1] < 3600}
        if not ok:
            count, _ = self._recent_fails.get(sig, (0, 0.0))
            self._recent_fails[sig] = (count + 1, now)
            if self._recent_fails[sig][0] >= 3:
                from .metrics import METRICS
                METRICS.inc("lessons.recorded")
                err = result[:80]
                self._store.add(Lesson(sig, cmd, f"{cmd} {args}", err, "(no fix yet)", 3, now))
                self._store.flush()
        else:
            self._recent_fails.pop(sig, None)
