"""Trusted Git/source observations for release evidence."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import gauntlet.source_provenance as source_provenance  # noqa: E402
from gauntlet.source_provenance import (  # noqa: E402
    SourceProvenanceError,
    observe_source_checkout,
)


def _git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def _repository(tmp_path: Path) -> tuple[Path, str]:
    root = tmp_path / "source"
    (root / "config").mkdir(parents=True)
    (root / "config" / "policy.json").write_text('{"policy":1}\n', encoding="utf-8")
    (root / "config" / "catalog.json").write_text('{"catalog":1}\n', encoding="utf-8")
    (root / "harness.lock").write_text("locked\n", encoding="utf-8")
    _git(root, "init", "-q")
    _git(root, "config", "user.name", "Gauntlet Test")
    _git(root, "config", "user.email", "gauntlet@example.invalid")
    _git(root, "add", ".")
    env = os.environ.copy()
    env.update(
        {
            "GIT_AUTHOR_DATE": "2026-01-01T00:00:00Z",
            "GIT_COMMITTER_DATE": "2026-01-01T00:00:00Z",
        }
    )
    subprocess.run(
        ["git", "-C", str(root), "commit", "-q", "-m", "fixture"],
        check=True,
        env=env,
    )
    return root, _git(root, "rev-parse", "HEAD")


def test_source_observation_binds_clean_head_tree_and_tracked_inputs(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)

    observed = observe_source_checkout(
        root,
        expected_head_sha=head,
        required_paths=("config/policy.json", "config/catalog.json", "harness.lock"),
    )

    assert observed.head_sha == head
    assert len(observed.tree_sha) == 40
    assert tuple(observed.file_digests) == (
        "config/catalog.json",
        "config/policy.json",
        "harness.lock",
    )
    assert all(len(digest) == 64 for digest in observed.file_digests.values())
    assert observed.file_payloads["config/policy.json"] == b'{"policy":1}\n'
    with pytest.raises(TypeError):
        observed.file_payloads["config/policy.json"] = b"substituted"  # type: ignore[index]
    assert len(observed.observation_sha) == 64


def test_source_observation_rejects_wrong_head_or_dirty_tracked_file(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)

    with pytest.raises(SourceProvenanceError, match="HEAD"):
        observe_source_checkout(
            root,
            expected_head_sha="0" * len(head),
            required_paths=("config/policy.json",),
        )

    (root / "config" / "policy.json").write_text('{"policy":2}\n', encoding="utf-8")
    with pytest.raises(SourceProvenanceError, match="tracked worktree"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_rejects_index_only_delta(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    policy = root / "config" / "policy.json"
    original = policy.read_bytes()
    policy.write_text('{"policy":2}\n', encoding="utf-8")
    _git(root, "add", "config/policy.json")
    policy.write_bytes(original)

    with pytest.raises(SourceProvenanceError, match="tracked worktree"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_rejects_assume_unchanged_tracked_input(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    _git(root, "update-index", "--assume-unchanged", "config/catalog.json")
    (root / "config" / "catalog.json").write_text('{"catalog":"always-green"}\n', encoding="utf-8")

    with pytest.raises(SourceProvenanceError, match="index flags"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_does_not_follow_git_replace_refs(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    (root / "config/policy.json").write_text('{"policy":"substituted"}\n', encoding="utf-8")
    _git(root, "add", "config/policy.json")
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
    assert _git(root, "rev-parse", "HEAD") == head

    with pytest.raises(SourceProvenanceError, match="tracked worktree"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_uses_git_blob_bytes_in_clean_autocrlf_checkout(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    _git(root, "config", "core.autocrlf", "true")
    policy = root / "config" / "policy.json"
    policy.unlink()
    _git(root, "checkout", "--", "config/policy.json")
    if b"\r\n" not in policy.read_bytes():
        pytest.skip("Git checkout filters did not materialize CRLF on this platform")
    assert _git(root, "status", "--porcelain") == ""

    observed = observe_source_checkout(
        root,
        expected_head_sha=head,
        required_paths=("config/policy.json",),
    )

    assert observed.file_payloads["config/policy.json"] == b'{"policy":1}\n'


def test_source_observation_rejects_untracked_or_escaping_required_path(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    (root / "untracked.json").write_text("{}\n", encoding="utf-8")

    with pytest.raises(SourceProvenanceError, match="tracked"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("untracked.json",),
        )
    with pytest.raises(SourceProvenanceError, match="normalized"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("../outside",),
        )


def test_source_observation_rejects_symlinked_tracked_input(tmp_path: Path) -> None:
    root, head = _repository(tmp_path)
    target = root / "config" / "policy.json"
    original = target.read_bytes()
    target.unlink()
    outside = tmp_path / "outside-policy.json"
    outside.write_bytes(original)
    try:
        target.symlink_to(outside)
    except OSError as exc:
        pytest.skip(f"symlinks are unavailable on this platform: {exc}")

    with pytest.raises(SourceProvenanceError, match="tracked worktree|regular file"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_rejects_head_change_during_capture(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    root, head = _repository(tmp_path)
    original_git = source_provenance._git
    head_reads = 0

    def drifting_git(repository: Path, *arguments: str) -> str:
        nonlocal head_reads
        result = original_git(repository, *arguments)
        if arguments == ("rev-parse", "HEAD"):
            head_reads += 1
            if head_reads == 2:
                return "0" * len(head)
        return result

    monkeypatch.setattr(source_provenance, "_git", drifting_git)
    with pytest.raises(SourceProvenanceError, match="changed"):
        observe_source_checkout(
            root,
            expected_head_sha=head,
            required_paths=("config/policy.json",),
        )


def test_source_observation_rejects_a_b_a_checkout_flip(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    root, head_a = _repository(tmp_path)
    (root / "config" / "policy.json").write_text('{"policy":2}\n', encoding="utf-8")
    _git(root, "add", "config/policy.json")
    _git(root, "commit", "-q", "-m", "second")
    head_b = _git(root, "rev-parse", "HEAD")
    tree_b = _git(root, "rev-parse", f"{head_b}^{{tree}}")
    _git(root, "checkout", "-q", head_a)
    original_git = source_provenance._git
    head_reads = 0

    def flipping_git(repository: Path, *arguments: str) -> str:
        nonlocal head_reads
        if arguments == ("rev-parse", "HEAD"):
            head_reads += 1
            if head_reads == 1:
                observed = original_git(repository, *arguments)
                _git(repository, "checkout", "-q", head_b)
                return observed
            _git(repository, "checkout", "-q", head_a)
            return head_a
        if arguments == ("rev-parse", "HEAD^{tree}"):
            return tree_b
        return original_git(repository, *arguments)

    monkeypatch.setattr(source_provenance, "_git", flipping_git)
    with pytest.raises(SourceProvenanceError, match="worktree|bytes|changed"):
        observe_source_checkout(
            root,
            expected_head_sha=head_a,
            required_paths=("config/policy.json",),
        )
