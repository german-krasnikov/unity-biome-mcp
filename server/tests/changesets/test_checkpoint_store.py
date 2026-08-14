"""T19: CheckpointStore unit tests."""
from __future__ import annotations

import json
import os
import time
import uuid
from pathlib import Path


def _make_cp(state: str = "ready", fingerprint: str = "fp1234567890ab"):
    from unity_mcp.checkpoint import Checkpoint, FileManifest

    return Checkpoint(
        checkpoint_id=str(uuid.uuid4()),
        turn_id=1,
        state=state,
        fingerprint=fingerprint,
        manifest=FileManifest(entries=()),
        undo_group_id=5,
        domain_stamp="d1s2",
        created_at="2026-08-14T00:00:00+00:00",
        finalized_at=None,
    )


def _make_store(tmp_path: Path, **kwargs):
    from unity_mcp.changeset_store import ContentStore
    from unity_mcp.checkpoint_store import CheckpointStore

    content_store = ContentStore(str(tmp_path / "blobs"))
    return CheckpointStore(
        fingerprint="fp1234567890ab",
        content_store=content_store,
        _dir=tmp_path / "checkpoints",
        **kwargs,
    )


def test_checkpoint_store_roundtrip(tmp_path):
    store = _make_store(tmp_path)
    cp = _make_cp()
    store.save(cp)
    loaded = store.load(cp.checkpoint_id)
    assert loaded == cp


def test_checkpoint_store_update_state(tmp_path):
    store = _make_store(tmp_path)
    cp = _make_cp(state="preparing")
    store.save(cp)
    store.update_state(cp.checkpoint_id, "ready")
    loaded = store.load(cp.checkpoint_id)
    assert loaded.state == "ready"


def test_checkpoint_store_list_ready_excludes_non_ready(tmp_path):
    store = _make_store(tmp_path)
    cp_ready = _make_cp(state="ready")
    cp_preparing = _make_cp(state="preparing")
    store.save(cp_ready)
    store.save(cp_preparing)

    ready_ids = {cp.checkpoint_id for cp in store.list_ready()}
    assert cp_ready.checkpoint_id in ready_ids
    assert cp_preparing.checkpoint_id not in ready_ids


def test_checkpoint_store_load_missing_returns_none(tmp_path):
    store = _make_store(tmp_path)
    assert store.load("nonexistent-id") is None


def test_checkpoint_store_load_corrupt_returns_none(tmp_path):
    store = _make_store(tmp_path)
    cp_dir = tmp_path / "checkpoints"
    cp_dir.mkdir(parents=True, exist_ok=True)
    (cp_dir / "bad-id.json").write_text("{corrupt json", encoding="utf-8")
    assert store.load("bad-id") is None


def test_checkpoint_store_evict_by_age(tmp_path):
    """Checkpoint with mtime older than max_age_days is removed."""
    store = _make_store(tmp_path, max_age_days=7)
    cp = _make_cp(state="ready")
    store.save(cp)

    cp_file = tmp_path / "checkpoints" / f"{cp.checkpoint_id}.json"
    old_mtime = time.time() - 8 * 86400
    os.utime(cp_file, (old_mtime, old_mtime))

    evicted = store.evict()
    assert evicted == 1
    assert store.load(cp.checkpoint_id) is None


def test_checkpoint_store_evict_by_size(tmp_path):
    """When total .json size exceeds max_bytes, oldest files are removed."""
    store = _make_store(tmp_path, max_bytes=1)  # 1 byte → any json exceeds
    cp1 = _make_cp(state="ready")
    store.save(cp1)
    time.sleep(0.01)
    cp2 = _make_cp(state="ready")
    store.save(cp2)

    evicted = store.evict()
    assert evicted >= 1
