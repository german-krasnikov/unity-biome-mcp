"""T16: ContentStore — content-addressed blob store, capped at max_bytes."""
from __future__ import annotations

from pathlib import Path

from .changeset import ContentRef


class ContentStore:
    def __init__(self, fingerprint_or_dir: str, max_bytes: int = 50 * 1024 * 1024) -> None:
        # Accept either a fingerprint string OR an absolute path (for tests).
        p = Path(fingerprint_or_dir)
        if p.is_absolute():
            self._dir = p
        else:
            from .paths import blobs_dir
            self._dir = blobs_dir(fingerprint_or_dir)
        self._max_bytes = max_bytes

    def put(self, content: str) -> ContentRef:
        """Hash content, skip write if blob exists, evict oldest if over limit."""
        ref = ContentRef.of(content)
        blob = self._dir / ref.hash16
        if not blob.exists():
            self._dir.mkdir(parents=True, exist_ok=True)
            blob.write_text(content, encoding="utf-8")
            self._evict_if_needed()
        return ref

    def get(self, ref: ContentRef) -> str | None:
        blob = self._dir / ref.hash16
        return blob.read_text(encoding="utf-8") if blob.exists() else None

    def has(self, ref: ContentRef) -> bool:
        return (self._dir / ref.hash16).exists()

    def _evict_if_needed(self) -> None:
        blobs = sorted(self._dir.glob("*"), key=lambda p: p.stat().st_mtime)
        total = sum(p.stat().st_size for p in blobs)
        for blob in blobs:
            if total <= self._max_bytes:
                break
            total -= blob.stat().st_size
            blob.unlink(missing_ok=True)


# Module-level singleton — initialized in server lifespan
_store: ContentStore | None = None
_fingerprint: str | None = None


def get_store() -> ContentStore | None:
    return _store


def get_fingerprint() -> str | None:
    """Return the fingerprint used to initialize the store, or None."""
    return _fingerprint


def init_store(fingerprint: str) -> ContentStore:
    global _store, _fingerprint
    _fingerprint = fingerprint
    _store = ContentStore(fingerprint)
    return _store
