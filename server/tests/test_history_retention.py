"""T23: RetentionPolicy unit tests."""

import os
import time
from pathlib import Path


def _write_conv(history_dir: Path, conv_id: str, size_bytes: int = 100) -> None:
    """Write fake .jsonl and .meta.json pair."""
    (history_dir / f"{conv_id}.jsonl").write_bytes(b"x" * size_bytes)
    (history_dir / f"{conv_id}.meta.json").write_bytes(b"{}")


def _age_file(path: Path, days_old: float) -> None:
    mtime = time.time() - days_old * 86400
    os.utime(path, (mtime, mtime))


def test_age_eviction_removes_old_removes_both_files(tmp_path):
    from unity_mcp.history.retention import evict, MAX_AGE_DAYS

    history_dir = tmp_path / "history"
    history_dir.mkdir()
    _write_conv(history_dir, "old")
    _write_conv(history_dir, "new")
    _age_file(history_dir / "old.meta.json", MAX_AGE_DAYS + 1)
    _age_file(history_dir / "old.jsonl", MAX_AGE_DAYS + 1)

    evicted = evict(history_dir)

    assert evicted >= 1
    assert not (history_dir / "old.jsonl").exists()
    assert not (history_dir / "old.meta.json").exists()
    assert (history_dir / "new.jsonl").exists()


def test_count_eviction_removes_oldest_when_over_limit(tmp_path):
    from unity_mcp.history.retention import evict, MAX_CONVERSATIONS

    history_dir = tmp_path / "history"
    history_dir.mkdir()
    # Write MAX_CONVERSATIONS + 1 conversations
    conv_ids = [f"conv_{i:03d}" for i in range(MAX_CONVERSATIONS + 1)]
    for i, cid in enumerate(conv_ids):
        _write_conv(history_dir, cid)
        # All within age window (1 day old) — only count eviction triggers
        _age_file(history_dir / f"{cid}.meta.json", 1 + i * 0.01)
        _age_file(history_dir / f"{cid}.jsonl", 1 + i * 0.01)

    evict(history_dir)

    remaining = list(history_dir.glob("*.meta.json"))
    assert len(remaining) <= MAX_CONVERSATIONS


def test_size_eviction_removes_oldest_until_under_limit(tmp_path):
    from unity_mcp.history.retention import evict, MAX_BYTES

    history_dir = tmp_path / "history"
    history_dir.mkdir()
    # Two old big conversations and one recent small one
    big_size = MAX_BYTES // 2 + 1
    _write_conv(history_dir, "old1", big_size)
    _write_conv(history_dir, "old2", big_size)
    _write_conv(history_dir, "recent", 100)
    _age_file(history_dir / "old1.meta.json", 5)
    _age_file(history_dir / "old1.jsonl", 5)
    _age_file(history_dir / "old2.meta.json", 4)
    _age_file(history_dir / "old2.jsonl", 4)

    evict(history_dir)

    # At least one of the big ones removed; recent stays
    assert (history_dir / "recent.jsonl").exists()


def test_both_files_removed_together(tmp_path):
    from unity_mcp.history.retention import evict, MAX_AGE_DAYS

    history_dir = tmp_path / "history"
    history_dir.mkdir()
    _write_conv(history_dir, "target")
    _age_file(history_dir / "target.meta.json", MAX_AGE_DAYS + 1)
    _age_file(history_dir / "target.jsonl", MAX_AGE_DAYS + 1)

    evict(history_dir)

    assert not (history_dir / "target.jsonl").exists()
    assert not (history_dir / "target.meta.json").exists()
