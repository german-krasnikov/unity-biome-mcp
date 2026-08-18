"""T16: snapshot_file() — read file, store blob, return ContentRef. Pure, no side effects."""

from pathlib import Path

from .changeset import ContentRef  # noqa: TC001
from .changeset_store import ContentStore  # noqa: TC001


def snapshot_file(path: str, store: ContentStore) -> ContentRef | None:
    """Read file at path, store in ContentStore, return ref. Returns None on missing or binary."""
    try:
        content = Path(path).read_text(encoding="utf-8", errors="strict")
        return store.put(content)
    except (OSError, UnicodeDecodeError):
        return None
