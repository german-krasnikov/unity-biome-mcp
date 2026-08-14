"""T19: Checkpoint value objects unit tests (checkpoint.py)."""
from __future__ import annotations

import dataclasses

import pytest


def test_file_manifest_of_builds_sorted_entries():
    """FileManifest.of sorts entries by path."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest

    ref_a = ContentRef.of("a")
    ref_b = ContentRef.of("b")
    m = FileManifest.of({"/z.txt": ref_b, "/a.txt": ref_a})
    assert m.entries[0][0] == "/a.txt"
    assert m.entries[1][0] == "/z.txt"


def test_file_manifest_find_existing_path():
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest

    ref = ContentRef.of("content")
    m = FileManifest.of({"/x.txt": ref})
    assert m.find("/x.txt") == ref.hash16


def test_file_manifest_find_missing_path_returns_none():
    from unity_mcp.checkpoint import FileManifest

    m = FileManifest(entries=())
    assert m.find("/missing") is None


def test_file_manifest_paths_returns_sorted_list():
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import FileManifest

    ref = ContentRef.of("x")
    m = FileManifest.of({"/b": ref, "/a": ref})
    assert m.paths() == ["/a", "/b"]


def test_checkpoint_frozen():
    """Checkpoint is frozen — attribute assignment raises."""
    from unity_mcp.checkpoint import Checkpoint, FileManifest

    cp = Checkpoint(
        checkpoint_id="id-1",
        turn_id=0,
        state="ready",
        fingerprint="fp",
        manifest=FileManifest(entries=()),
        undo_group_id=0,
        domain_stamp="",
        created_at="2026-08-14T00:00:00+00:00",
        finalized_at=None,
    )
    with pytest.raises((AttributeError, dataclasses.FrozenInstanceError)):
        cp.state = "failed"  # type: ignore[misc]


def test_checkpoint_to_dict_from_dict_roundtrip():
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import Checkpoint, FileManifest

    ref = ContentRef.of("data")
    cp = Checkpoint(
        checkpoint_id="cp-abc-123",
        turn_id=3,
        state="ready",
        fingerprint="fp12345678abcd",
        manifest=FileManifest.of({"/a.cs": ref}),
        undo_group_id=7,
        domain_stamp="d1a2b3c4",
        created_at="2026-08-14T00:00:00+00:00",
        finalized_at=None,
    )
    d = cp.to_dict()
    cp2 = Checkpoint.from_dict(d)
    assert cp == cp2


def test_conflict_info_fields():
    from unity_mcp.checkpoint import ConflictInfo

    ci = ConflictInfo(path="/a.cs", expected_hash="abcd1234", actual_hash="efgh5678")
    assert ci.path == "/a.cs"
    assert ci.expected_hash == "abcd1234"
    assert ci.actual_hash == "efgh5678"
