"""T16: ContentStore unit tests."""
from __future__ import annotations

import os
import time
from pathlib import Path
from typing import TYPE_CHECKING

import pytest


def make_store(tmp_path: Path, max_bytes: int = 50 * 1024 * 1024):
    from unity_mcp.changeset_store import ContentStore
    return ContentStore(str(tmp_path), max_bytes=max_bytes)


def test_put_returns_content_ref(tmp_path):
    store = make_store(tmp_path)
    ref = store.put("hello world")
    assert ref is not None
    assert len(ref.hash16) == 16
    assert all(c in "0123456789abcdef" for c in ref.hash16)


def test_put_dedup_no_second_write(tmp_path):
    store = make_store(tmp_path)
    ref1 = store.put("same content")
    blob_path = store._dir / ref1.hash16
    mtime_after_first = blob_path.stat().st_mtime

    os.utime(blob_path, (mtime_after_first - 10, mtime_after_first - 10))
    anchored_mtime = blob_path.stat().st_mtime
    ref2 = store.put("same content")

    assert ref1 == ref2
    assert blob_path.stat().st_mtime == anchored_mtime  # not rewritten
    blobs = list(store._dir.glob("*"))
    assert len(blobs) == 1


def test_get_returns_stored_content(tmp_path):
    store = make_store(tmp_path)
    content = "class Foo {}"
    ref = store.put(content)
    assert store.get(ref) == content


def test_get_missing_ref_returns_none(tmp_path):
    from unity_mcp.changeset import ContentRef
    store = make_store(tmp_path)
    fake_ref = ContentRef("deadbeef12345678")
    assert store.get(fake_ref) is None


def test_put_creates_dirs_if_missing(tmp_path):
    blob_dir = tmp_path / "nested" / "deeply" / "blobs"
    from unity_mcp.changeset_store import ContentStore
    store = ContentStore(str(blob_dir), max_bytes=50 * 1024 * 1024)
    store.put("auto-create dirs")
    assert store._dir.exists()


def test_evict_oldest_when_over_limit(tmp_path):
    # max_bytes = 50 bytes; each blob is ~20+ bytes
    store = make_store(tmp_path, max_bytes=50)

    store.put("content_aaaa_one")
    time.sleep(0.02)
    store.put("content_bbbb_two")
    time.sleep(0.02)
    ref_c = store.put("content_cccc_three")

    # After eviction, oldest blob(s) should be gone; newest should survive
    assert store.has(ref_c)
    # At least one older blob was evicted
    remaining = list(store._dir.glob("*"))
    total_size = sum(p.stat().st_size for p in remaining)
    assert total_size <= 50


def test_separate_fingerprints_isolated(tmp_path):
    from unity_mcp.changeset_store import ContentStore
    dir_a = tmp_path / "fp_aaa"
    dir_b = tmp_path / "fp_bbb"
    store_a = ContentStore(str(dir_a))
    store_b = ContentStore(str(dir_b))

    ref = store_a.put("shared content")
    assert not store_b.has(ref)
    assert store_a.has(ref)


# ── M1: atomic write ──────────────────────────────────────────────────────────

def test_put_no_partial_blob_on_write_failure(tmp_path, monkeypatch):
    """Atomic write: blob path stays absent when write_text crashes mid-write."""
    from unity_mcp.changeset import ContentRef

    store = make_store(tmp_path)
    content = "complete content"
    ref = ContentRef.of(content)
    blob_path = tmp_path / ref.hash16

    def crash_on_write(self, data, encoding=None, errors=None):
        self.write_bytes(b"garbage")  # leave partial at whatever path self is
        raise OSError("simulated disk failure")

    monkeypatch.setattr(Path, "write_text", crash_on_write)

    with pytest.raises(OSError):
        store.put(content)

    assert not blob_path.exists()


# ── M6: pre-eviction guard ────────────────────────────────────────────────────

def test_put_newly_written_blob_never_evicted(tmp_path):
    """Newly written blob is retrievable even when it alone exceeds max_bytes."""
    store = make_store(tmp_path, max_bytes=1)  # 1-byte limit
    ref = store.put("hello world")  # 11 bytes > 1 byte
    assert store.get(ref) == "hello world"
