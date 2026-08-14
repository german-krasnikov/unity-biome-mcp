"""T16: snapshot_file() — read file, store blob, return ContentRef. Pure, no side effects."""
from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .changeset import ContentRef
    from .changeset_store import ContentStore


def snapshot_file(path: str, store: ContentStore) -> ContentRef | None:
    """Read file at path, store in ContentStore, return ref. Returns None on missing or binary."""
    try:
        content = Path(path).read_text(encoding="utf-8", errors="strict")
        return store.put(content)
    except (OSError, UnicodeDecodeError):
        return None
