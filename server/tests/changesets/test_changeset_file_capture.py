"""T16: snapshot_file() unit tests (4 tests)."""
from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


def make_store(tmp_path: Path):
    from unity_mcp.changeset_store import ContentStore
    return ContentStore(str(tmp_path / "blobs"))


def test_snapshot_file_returns_ref(tmp_path):
    from unity_mcp.changeset import ContentRef
    from unity_mcp.changeset_file_capture import snapshot_file

    content = "class Foo {}"
    f = tmp_path / "Foo.cs"
    f.write_text(content, encoding="utf-8")

    store = make_store(tmp_path)
    ref = snapshot_file(str(f), store)

    assert ref == ContentRef.of(content)


def test_snapshot_missing_file_returns_none(tmp_path):
    from unity_mcp.changeset_file_capture import snapshot_file

    store = make_store(tmp_path)
    result = snapshot_file(str(tmp_path / "nonexistent.cs"), store)
    assert result is None


def test_snapshot_binary_file_returns_none(tmp_path):
    from unity_mcp.changeset_file_capture import snapshot_file

    binary_file = tmp_path / "data.bin"
    binary_file.write_bytes(b"\x80\x81\x82\x83")  # invalid UTF-8

    store = make_store(tmp_path)
    result = snapshot_file(str(binary_file), store)
    assert result is None


def test_snapshot_stores_blob_in_store(tmp_path):
    from unity_mcp.changeset_file_capture import snapshot_file

    content = "public class Bar {}"
    f = tmp_path / "Bar.cs"
    f.write_text(content, encoding="utf-8")

    store = make_store(tmp_path)
    ref = snapshot_file(str(f), store)

    assert ref is not None
    assert store.has(ref)
    assert store.get(ref) == content
