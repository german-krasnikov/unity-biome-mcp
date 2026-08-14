"""T23: RetentionPolicy — age/count/size eviction of conversation history."""
from __future__ import annotations

import contextlib
import time
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path

MAX_CONVERSATIONS = 50
MAX_AGE_DAYS      = 30
MAX_BYTES         = 100 * 1024 * 1024  # 100 MB


def evict(history_dir: Path) -> int:
    """Remove oldest conversations by age/count/size. Returns evicted count."""
    if not history_dir.exists():
        return 0

    meta_files = sorted(
        history_dir.glob("*.meta.json"),
        key=lambda f: f.stat().st_mtime,
    )
    cutoff = time.time() - MAX_AGE_DAYS * 86400
    evicted = 0

    # Age-based eviction
    for f in list(meta_files):
        if f.stat().st_mtime < cutoff:
            _remove_pair(history_dir, f.stem.removesuffix(".meta"))
            meta_files.remove(f)
            evicted += 1

    # Count-based eviction (oldest first, already sorted)
    while len(meta_files) > MAX_CONVERSATIONS:
        f = meta_files.pop(0)
        _remove_pair(history_dir, f.stem.removesuffix(".meta"))
        evicted += 1

    # Size-based eviction
    jsonl_files = {f.stem.removesuffix(".meta"): history_dir / f"{f.stem.removesuffix('.meta')}.jsonl"
                   for f in meta_files}
    total = sum(j.stat().st_size for j in jsonl_files.values() if j.exists())
    for f in list(meta_files):
        if total <= MAX_BYTES:
            break
        cid = f.stem.removesuffix(".meta")
        jsonl = jsonl_files.get(cid)
        if jsonl and jsonl.exists():
            try:  # noqa: SIM105 — PERF203 forbids suppress() in loops
                total -= jsonl.stat().st_size
            except OSError:
                pass
        _remove_pair(history_dir, cid)
        meta_files.remove(f)
        evicted += 1

    return evicted


def _remove_pair(history_dir: Path, conv_id: str) -> None:
    """Delete .jsonl and .meta.json together, suppressing errors."""
    for suffix in (".jsonl", ".meta.json"):
        with contextlib.suppress(OSError):
            (history_dir / f"{conv_id}{suffix}").unlink(missing_ok=True)
