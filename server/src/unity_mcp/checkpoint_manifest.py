"""T19: scan_manifest(), current_hash(), detect_conflicts() — pure file I/O, no state."""

import contextlib
from collections.abc import Iterable  # noqa: TC003
from pathlib import Path

from .changeset import ContentRef
from .changeset_store import ContentStore  # noqa: TC001
from .checkpoint import ConflictInfo, FileManifest  # noqa: TC001


def scan_manifest(paths: Iterable[str], store: ContentStore) -> FileManifest:
    """Read each path, store blob, build FileManifest.

    Missing or binary files are silently skipped.
    """
    from .checkpoint import FileManifest

    path_refs: dict[str, ContentRef] = {}
    for path in paths:
        with contextlib.suppress(OSError, UnicodeDecodeError):
            content = Path(path).read_text(encoding="utf-8", errors="strict")
            path_refs[path] = store.put(content)
    return FileManifest.of(path_refs)


def current_hash(path: str) -> str | None:
    """sha256 first 16 hex chars of current file content; None on missing/binary."""
    try:
        content = Path(path).read_text(encoding="utf-8", errors="strict")
        return ContentRef.of(content).hash16
    except (OSError, UnicodeDecodeError):
        return None


def detect_conflicts(
    manifest: FileManifest,
    after_refs: dict[str, str],
    store: ContentStore,
) -> list[ConflictInfo]:
    """Return conflicts where user edited a file after the agent turn.

    Conflict: current != after_ref AND current != before_ref.
    Iterates over after_refs paths only.
    """
    from .checkpoint import ConflictInfo

    conflicts: list[ConflictInfo] = []
    for path, after_hash in after_refs.items():
        current = current_hash(path)
        if current is None:
            continue
        if current == after_hash:
            continue  # unchanged since agent turn
        before_hash = manifest.find(path)
        if current == before_hash:
            continue  # already restored to before-state
        conflicts.append(ConflictInfo(path=path, expected_hash=after_hash, actual_hash=current))
    return conflicts
