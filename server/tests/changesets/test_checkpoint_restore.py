"""T19: checkpoint_restore unit tests."""

from pathlib import Path


def _make_store(tmp_path: Path):
    from unity_mcp.changeset_store import ContentStore

    return ContentStore(str(tmp_path / "blobs"))


def test_restore_files_writes_before_content(tmp_path):
    """Restore overwrites file with before-state content when no conflict."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_restore import restore_files

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    f.write_text(after_content, encoding="utf-8")  # current == after (no conflict)

    store = _make_store(tmp_path)
    before_ref = store.put(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    result = restore_files(manifest, after_refs, store)

    assert str(f) in result.restored
    assert f.read_text(encoding="utf-8") == before_content


def test_restore_files_refuses_conflict_without_force(tmp_path):
    """User-edited file → RestoreResult.conflicts populated, file unchanged."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_restore import restore_files

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    user_content = "user_edit"
    f.write_text(user_content, encoding="utf-8")

    store = _make_store(tmp_path)
    before_ref = store.put(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    result = restore_files(manifest, after_refs, store)

    assert len(result.conflicts) == 1
    assert str(f) not in result.restored
    assert f.read_text(encoding="utf-8") == user_content  # file unchanged


def test_restore_files_force_overwrites_conflict(tmp_path):
    """force=True → file written despite conflict."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_restore import restore_files

    f = tmp_path / "a.cs"
    before_content = "before"
    after_content = "after"
    user_content = "user_edit"
    f.write_text(user_content, encoding="utf-8")

    store = _make_store(tmp_path)
    before_ref = store.put(before_content)
    after_ref = ContentRef.of(after_content)

    manifest = FileManifest.of({str(f): before_ref})
    after_refs = {str(f): after_ref.hash16}

    result = restore_files(manifest, after_refs, store, force=True)

    assert str(f) in result.restored
    assert f.read_text(encoding="utf-8") == before_content


def test_restore_files_aborts_all_when_any_blob_missing(tmp_path):
    """C3: if ANY blob is evicted, NO file is written — guard fires before loop."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_restore import restore_files

    f1 = tmp_path / "a.cs"
    f2 = tmp_path / "b.cs"
    f1.write_text("after", encoding="utf-8")
    f2.write_text("after", encoding="utf-8")

    store = _make_store(tmp_path)
    ref1 = store.put("before_a")               # blob present
    ref2 = ContentRef.of("before_b_evicted")   # blob NOT stored (evicted)

    manifest = FileManifest.of({str(f1): ref1, str(f2): ref2})
    after_refs = {
        str(f1): ContentRef.of("after").hash16,
        str(f2): ContentRef.of("after").hash16,
    }

    result = restore_files(manifest, after_refs, store)

    # f1 must NOT be overwritten — guard aborted before any write
    assert f1.read_text(encoding="utf-8") == "after"
    # missing blob path is in skipped
    assert str(f2) in result.skipped
    # nothing was restored
    assert result.restored == []


def test_restore_files_skips_missing_blob(tmp_path):
    """No blob in store for before_ref → path added to skipped."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest
    from unity_mcp.checkpoint_restore import restore_files

    f = tmp_path / "a.cs"
    f.write_text("current", encoding="utf-8")

    store = _make_store(tmp_path)
    # Don't put the blob — store is empty
    before_ref = ContentRef.of("before_content_not_stored")

    manifest = FileManifest.of({str(f): before_ref})
    after_refs: dict[str, str] = {}  # no after_refs → no conflict check

    result = restore_files(manifest, after_refs, store)

    assert str(f) in result.skipped
    assert str(f) not in result.restored
