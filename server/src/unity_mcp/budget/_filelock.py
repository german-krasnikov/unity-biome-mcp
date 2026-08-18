"""Cross-platform file lock — hand-rolled, no external deps.

Used by CostTracker to serialize multi-process writes to budget.json.
Blocking exclusive lock held briefly per write. Sentinel .lock file
approach: never locks the data file itself, preventing data corruption.
"""

import sys
from contextlib import contextmanager, suppress
from pathlib import Path  # noqa: TC003


@contextmanager
def locked(path: Path):
    """Acquire exclusive lock on a sentinel `.lock` file next to path.

    Blocking — waits until the lock is available.
    Auto-releases on context exit (including exceptions).
    """
    sentinel = path.with_suffix(path.suffix + ".lock")
    sentinel.parent.mkdir(parents=True, exist_ok=True)
    with open(sentinel, "w", encoding="utf-8") as f:
        if sys.platform == "win32":
            import msvcrt  # deferred: win32-only, functional only on Windows
            f.seek(0)
            msvcrt.locking(f.fileno(), msvcrt.LK_LOCK, 1)
            try:
                yield
            finally:
                try:
                    f.seek(0)
                    msvcrt.locking(f.fileno(), msvcrt.LK_UNLCK, 1)
                except Exception:
                    pass
        else:
            import fcntl
            fcntl.flock(f.fileno(), fcntl.LOCK_EX)
            try:
                yield
            finally:
                with suppress(Exception):
                    fcntl.flock(f.fileno(), fcntl.LOCK_UN)
