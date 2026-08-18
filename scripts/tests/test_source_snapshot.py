"""Tests for materializing an exact, overlay-free Git source snapshot."""


import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from gauntlet.source_snapshot import (  # noqa: E402
    SourceSnapshotError,
    materialize_source_snapshot,
)


def _git(root: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *arguments],
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def _repository(root: Path) -> tuple[str, Path]:
    root.mkdir()
    _git(root, "init", "-q")
    _git(root, "config", "user.name", "Gauntlet Test")
    _git(root, "config", "user.email", "gauntlet@example.invalid")
    tracked = root / "server" / "tests" / "test_contract.py"
    tracked.parent.mkdir(parents=True)
    tracked.write_text("def test_contract():\n    assert False\n", encoding="utf-8")
    _git(root, "add", ".")
    _git(root, "commit", "-q", "-m", "exact source")
    return _git(root, "rev-parse", "HEAD"), tracked


def test_snapshot_uses_exact_commit_and_excludes_untracked_overlays(
    tmp_path: Path,
) -> None:
    root = tmp_path / "source"
    head, tracked = _repository(root)
    tracked.write_text("def test_contract():\n    assert True\n", encoding="utf-8")
    overlay = tracked.parent / "conftest.py"
    overlay.write_text("def pytest_collection_modifyitems(items): pass\n", encoding="utf-8")
    destination = tmp_path / "snapshot"

    materialize_source_snapshot(root, expected_head_sha=head, destination=destination)

    assert (destination / "server/tests/test_contract.py").read_text(encoding="utf-8") == (
        "def test_contract():\n    assert False\n"
    )
    assert not (destination / "server/tests/conftest.py").exists()


def test_snapshot_rejects_wrong_commit_or_nonempty_destination(tmp_path: Path) -> None:
    root = tmp_path / "source"
    head, _ = _repository(root)

    with pytest.raises(SourceSnapshotError, match="commit"):
        materialize_source_snapshot(
            root,
            expected_head_sha="0" * len(head),
            destination=tmp_path / "wrong",
        )

    destination = tmp_path / "occupied"
    destination.mkdir()
    (destination / "sentinel").write_text("owned elsewhere", encoding="utf-8")
    with pytest.raises(SourceSnapshotError, match="empty"):
        materialize_source_snapshot(
            root,
            expected_head_sha=head,
            destination=destination,
        )


@pytest.mark.parametrize(
    ("attribute", "expected"),
    [
        ("export-ignore", "def test_contract():\n    assert False\n"),
        ("export-subst", "value = '$Format:%H$'\n"),
    ],
)
def test_snapshot_ignores_git_archive_export_attributes(
    tmp_path: Path,
    attribute: str,
    expected: str,
) -> None:
    root = tmp_path / "source"
    _, tracked = _repository(root)
    tracked.write_text(expected, encoding="utf-8")
    (root / ".gitattributes").write_text(
        f"server/tests/test_contract.py {attribute}\n",
        encoding="utf-8",
    )
    _git(root, "add", ".")
    _git(root, "commit", "-q", "-m", "archive attributes")
    head = _git(root, "rev-parse", "HEAD")
    destination = tmp_path / "snapshot"

    materialize_source_snapshot(root, expected_head_sha=head, destination=destination)

    assert (destination / "server/tests/test_contract.py").read_text(
        encoding="utf-8"
    ) == expected


def test_snapshot_reads_expected_commit_without_git_replace_refs(tmp_path: Path) -> None:
    root = tmp_path / "source"
    head, tracked = _repository(root)
    original = tracked.read_text(encoding="utf-8")
    tracked.write_text("def test_contract():\n    assert True\n", encoding="utf-8")
    _git(root, "add", "server/tests/test_contract.py")
    replacement_tree = _git(root, "write-tree")
    replacement = _git(
        root,
        "commit-tree",
        replacement_tree,
        "-p",
        head,
        "-m",
        "unreferenced replacement",
    )
    _git(root, "reset", "--hard", "-q", head)
    _git(root, "replace", head, replacement)
    _git(root, "reset", "--hard", "-q", head)
    destination = tmp_path / "snapshot"

    materialize_source_snapshot(root, expected_head_sha=head, destination=destination)

    assert (destination / "server/tests/test_contract.py").read_text(
        encoding="utf-8"
    ) == original
