"""T19: checkpoint_manifest unit tests."""
from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    pass


def _make_store(tmp_path: Path):
    from unity_mcp.changeset_store import ContentStore

    return ContentStore(str(tmp_path / "blobs"))


def test_scan_manifest_puts_blobs_and_returns_refs(tmp_path):
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint_manifest import scan_manifest

    f = tmp_path / "a.cs"
    f.write_text("class A {}", encoding="utf-8")
    store = _make_store(tmp_path)

    m = scan_manifest([str(f)], store)

    assert m.find(str(f)) == ContentRef.of("class A {}").hash16


def test_scan_manifest_skips_missing_file(tmp_path):
    from unity_mcp.checkpoint_manifest import scan_manifest

    store = _make_store(tmp_path)
    m = scan_manifest([str(tmp_path / "nonexistent.cs")], store)
    assert len(m.entries) == 0


def test_scan_manifest_skips_binary_file(tmp_path):
    from unity_mcp.checkpoint_manifest import scan_manifest

    bin_file = tmp_path / "data.bin"
    bin_file.write_bytes(b"\x80\x81\x82\x83")
    store = _make_store(tmp_path)

    m = scan_manifest([str(bin_file)], store)
    assert len(m.entries) == 0


def test_current_hash_returns_sha256_prefix(tmp_path):
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint_manifest import current_hash

    f = tmp_path / "x.txt"
    f.write_text("hello", encoding="utf-8")
    assert current_hash(str(f)) == ContentRef.of("hello").hash16


def test_current_hash_missing_returns_none(tmp_path):
    from unity_mcp.checkpoint_manifest import current_hash

    assert current_hash(str(tmp_path / "missing.txt")) is None


def test_detect_conflicts_no_change_returns_empty(tmp_path):
    """current == after_ref → no conflict."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_manifest import detect_conflicts

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    f.write_text(after_content, encoding="utf-8")  # current == after

    store = _make_store(tmp_path)
    before_ref = ContentRef.of(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    assert detect_conflicts(manifest, after_refs, store) == []


def test_detect_conflicts_user_edited_returns_conflict(tmp_path):
    """current != after_ref AND current != before_ref → ConflictInfo returned."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_manifest import detect_conflicts

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    user_content = "user_edit"
    f.write_text(user_content, encoding="utf-8")

    store = _make_store(tmp_path)
    before_ref = ContentRef.of(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    conflicts = detect_conflicts(manifest, after_refs, store)
    assert len(conflicts) == 1
    assert conflicts[0].path == str(f)
    assert conflicts[0].expected_hash == after_ref.hash16
    assert conflicts[0].actual_hash == ContentRef.of(user_content).hash16


def test_detect_conflicts_already_restored_is_no_conflict(tmp_path):
    """current == before_ref → already at before-state, no conflict."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_manifest import detect_conflicts

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    f.write_text(before_content, encoding="utf-8")  # already restored

    store = _make_store(tmp_path)
    before_ref = ContentRef.of(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    assert detect_conflicts(manifest, after_refs, store) == []
