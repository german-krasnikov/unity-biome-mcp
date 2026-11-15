"""Tests for LessonStore + LessonRecorder."""
import json
import time
from pathlib import Path
from unittest.mock import patch, mock_open
from unity_mcp.lessons import Lesson, LessonStore, LessonRecorder


# ── LessonStore ─────────────────────────────────────────────────────────────

def test_lesson_store_load_missing_file_empty(tmp_path):
    store = LessonStore(tmp_path / "nonexistent.json")
    assert store._lessons == {}


def test_lesson_store_load_corrupt_file_empty(tmp_path):
    p = tmp_path / "lessons.json"
    p.write_text("NOT JSON{{{}", encoding="utf-8")
    store = LessonStore(p)
    assert store._lessons == {}


def test_lesson_store_add_persists(tmp_path):
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    lesson = Lesson("abc123", "set_property", "set_property path=X", "Error", "use correct path", 1, time.time())
    store.add(lesson)
    store.flush()

    store2 = LessonStore(p)
    found = store2.find_by_sig("abc123")
    assert found is not None
    assert found.cmd == "set_property"


def test_lesson_store_add_updates_hits_if_exists(tmp_path):
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    t = time.time()
    l1 = Lesson("sig1", "cmd", "pattern", "err", "fix", 1, t)
    l2 = Lesson("sig1", "cmd", "pattern", "err", "fix", 5, t)
    store.add(l1)
    store.add(l2)
    assert store._lessons["sig1"].hits == 5


def test_lesson_store_flush_uses_per_pid_tmp(tmp_path):
    """flush() must write to a per-PID tmp path, not a shared bare .tmp."""
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    store.add(Lesson("x1", "cmd", "pat", "err", "fix", 1, time.time()))

    written_paths = []
    original_write_text = Path.write_text

    def capturing_write(self, data, *a, **kw):
        written_paths.append(str(self))
        return original_write_text(self, data, *a, **kw)

    with patch("os.getpid", return_value=9999), \
         patch.object(Path, "write_text", capturing_write):
        store.flush()

    # Must use PID suffix, never bare .tmp
    assert any("9999" in p for p in written_paths), f"no PID suffix in {written_paths}"
    assert not any(p.endswith(".tmp") and "9999" not in p for p in written_paths), \
        f"bare .tmp used: {written_paths}"


def test_lesson_store_atomic_write_no_corrupt(tmp_path):
    """flush() writes to tmp then renames — verify final file is valid JSON."""
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    store.add(Lesson("x1", "cmd", "pat", "err", "fix", 1, time.time()))
    store.flush()
    assert json.loads(p.read_text(encoding="utf-8"))  # valid JSON
    # Compact contract: no pretty-print newlines after the opening brace
    content = p.read_text(encoding="utf-8")
    assert '\n' not in content.split('{')[1][:10], "lessons.json must be compact (no indent=2)"


def test_lesson_store_hint_for_returns_known_pattern(tmp_path):
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    # Compute correct sig for these exact args
    import hashlib
    args = {"path": "/X"}
    sig = hashlib.md5(f"set_property:{json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    lesson = Lesson(sig, "set_property", "set_property on /X", "Error 42", "fix it", 3, time.time())
    store.add(lesson)

    hint = store.hint_for("set_property", args)
    assert hint is not None
    assert "LESSON" in hint
    assert "fix it" in hint


def test_lesson_store_hint_for_low_hits_returns_none(tmp_path):
    p = tmp_path / "lessons.json"
    store = LessonStore(p)
    import hashlib
    args = {"path": "/Y"}
    sig = hashlib.md5(f"set_property:{json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    lesson = Lesson(sig, "set_property", "pattern", "err", "fix", 1, time.time())  # hits=1 < 2
    store.add(lesson)
    assert store.hint_for("set_property", args) is None


# ── LessonRecorder ──────────────────────────────────────────────────────────

def test_lesson_recorder_3_failures_emits_lesson(tmp_path):
    store = LessonStore(tmp_path / "l.json")
    recorder = LessonRecorder(store)
    args = {"path": "/Bad"}
    for _ in range(3):
        recorder.record("set_property", args, "Error: something failed", ok=False)
    import hashlib
    sig = hashlib.md5(f"set_property:{json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    assert store.find_by_sig(sig) is not None


def test_lesson_recorder_success_resets_counter(tmp_path):
    store = LessonStore(tmp_path / "l.json")
    recorder = LessonRecorder(store)
    args = {"path": "/X"}
    recorder.record("set_property", args, "Error", ok=False)
    recorder.record("set_property", args, "Error", ok=False)
    recorder.record("set_property", args, "ok result", ok=True)
    # Only 2 failures before reset — no lesson yet
    import hashlib
    sig = hashlib.md5(f"set_property:{json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    assert store.find_by_sig(sig) is None


def test_lesson_recorder_different_args_separate_counters(tmp_path):
    store = LessonStore(tmp_path / "l.json")
    recorder = LessonRecorder(store)
    args_a = {"path": "/A"}
    args_b = {"path": "/B"}
    recorder.record("cmd", args_a, "Error", ok=False)
    recorder.record("cmd", args_a, "Error", ok=False)
    recorder.record("cmd", args_b, "Error", ok=False)
    # args_a only 2 failures, args_b only 1 — neither threshold
    import hashlib
    sig_a = hashlib.md5(f"cmd:{json.dumps(args_a, sort_keys=True)}".encode()).hexdigest()[:12]
    assert store.find_by_sig(sig_a) is None


# ── Fix 5: LessonStore cap + recent_fails prune ──────────────────────────────

def test_lesson_store_cap_evicts_oldest(tmp_path):
    """add() must evict the oldest lesson when over max_lessons=200."""
    p = tmp_path / "l.json"
    store = LessonStore(p, max_lessons=200)
    # Add 201 lessons with increasing last_seen — lesson 0 is oldest
    for i in range(201):
        store.add(Lesson(f"sig{i:03d}", "cmd", "pat", "err", "fix", 1, float(i)))
    assert len(store._lessons) == 200
    assert "sig000" not in store._lessons  # oldest evicted
    assert "sig200" in store._lessons       # newest kept


def test_lesson_store_cap_update_existing_no_evict(tmp_path):
    """Updating an existing lesson must not cause eviction."""
    p = tmp_path / "l.json"
    store = LessonStore(p, max_lessons=3)
    for i in range(3):
        store.add(Lesson(f"sig{i}", "cmd", "pat", "err", "fix", 1, float(i)))
    # Update sig0 — no new key, no eviction
    store.add(Lesson("sig0", "cmd", "pat", "err", "fix", 5, 99.0))
    assert len(store._lessons) == 3
    assert store._lessons["sig0"].hits == 5


def test_recent_fails_prune_after_1h(tmp_path):
    """Fails older than 1h must be pruned on next record() call."""
    store = LessonStore(tmp_path / "l.json")
    recorder = LessonRecorder(store)
    args = {"path": "/X"}
    now = time.time()
    # Inject stale fail directly (2h ago)
    import hashlib, json as _json
    sig = hashlib.md5(f"set_property:{_json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    recorder._recent_fails[sig] = (2, now - 7201)  # count=2, ts=2h ago
    # Record a new success — prune happens during record()
    recorder.record("set_property", args, "ok", ok=True)
    assert sig not in recorder._recent_fails


def test_recent_fails_not_pruned_within_1h(tmp_path):
    """Fails within 1h must NOT be pruned."""
    store = LessonStore(tmp_path / "l.json")
    recorder = LessonRecorder(store)
    args = {"path": "/Y"}
    import hashlib, json as _json
    sig = hashlib.md5(f"set_property:{_json.dumps(args, sort_keys=True)}".encode()).hexdigest()[:12]
    recorder._recent_fails[sig] = (2, time.time() - 1800)  # 30 min ago
    recorder.record("set_property", {"path": "/other"}, "ok", ok=True)  # unrelated
    assert sig in recorder._recent_fails
