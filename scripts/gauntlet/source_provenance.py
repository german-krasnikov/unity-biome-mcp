"""Independent Git and tracked-input observations for release gates."""

from __future__ import annotations

import hashlib
import stat
import subprocess
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from types import MappingProxyType
from typing import TYPE_CHECKING

from gauntlet.git_process import git_command, git_environment
from gauntlet.receipts import content_hash
from gauntlet.source_package_contents import (
    SourcePackageContentError,
    observe_package_content_digests,
)

if TYPE_CHECKING:
    from collections.abc import Mapping, Sequence

_MAX_SOURCE_INPUT_BYTES = 32 * 1024 * 1024


class SourceProvenanceError(ValueError):
    """Raised when a checkout cannot prove exact immutable release inputs."""


@dataclass(frozen=True, slots=True)
class SourceObservation:
    head_sha: str
    tree_sha: str
    file_digests: Mapping[str, str]
    file_payloads: Mapping[str, bytes]
    package_content_digests: Mapping[str, str]
    observation_sha: str


def observe_source_checkout(
    root: Path,
    *,
    expected_head_sha: str,
    required_paths: Sequence[str],
    package_content_roots: Mapping[str, tuple[str, str]] | None = None,
) -> SourceObservation:
    """Observe HEAD and required tracked bytes without trusting caller claims."""
    resolved_root = _validate_root(root)
    expected = _validate_sha(expected_head_sha, "expected HEAD")
    repository_root = Path(_git(resolved_root, "rev-parse", "--show-toplevel")).resolve()
    if repository_root != resolved_root:
        raise SourceProvenanceError("source root is not the Git worktree root")

    head_sha = _validate_sha(_git(resolved_root, "rev-parse", "HEAD"), "observed HEAD")
    if head_sha != expected:
        raise SourceProvenanceError("observed Git HEAD does not match release input")
    tree_sha = _validate_sha(
        _git(resolved_root, "rev-parse", f"{head_sha}^{{tree}}"),
        "Git tree",
    )
    _reject_unsafe_index_flags(resolved_root)
    if not _tracked_tree_is_clean(resolved_root, head_sha):
        raise SourceProvenanceError("tracked worktree differs from observed HEAD")

    normalized = _normalize_required_paths(required_paths)
    file_digests: dict[str, str] = {}
    file_payloads: dict[str, bytes] = {}
    for relative in normalized:
        payload = _read_blob_at_head(resolved_root, head_sha, relative)
        file_digests[relative] = hashlib.sha256(payload).hexdigest()
        file_payloads[relative] = payload
    try:
        package_content_digests = observe_package_content_digests(
            resolved_root,
            head_sha,
            package_content_roots or {},
        )
    except SourcePackageContentError as exc:
        raise SourceProvenanceError(str(exc)) from exc

    if _git(resolved_root, "rev-parse", "HEAD") != head_sha:
        raise SourceProvenanceError("Git HEAD changed during source observation")
    _reject_unsafe_index_flags(resolved_root)
    if not _tracked_tree_is_clean(resolved_root, head_sha):
        raise SourceProvenanceError("tracked worktree changed during source observation")

    observation_sha = content_hash(
        {
            "head_sha": head_sha,
            "tree_sha": tree_sha,
            "file_digests": file_digests,
            "package_content_digests": package_content_digests,
        }
    )
    return SourceObservation(
        head_sha,
        tree_sha,
        MappingProxyType(file_digests),
        MappingProxyType(file_payloads),
        MappingProxyType(package_content_digests),
        observation_sha,
    )


def _validate_root(root: Path) -> Path:
    try:
        metadata = root.lstat()
        resolved = root.resolve(strict=True)
    except OSError as exc:
        raise SourceProvenanceError("source root is not accessible") from exc
    if not stat.S_ISDIR(metadata.st_mode):
        raise SourceProvenanceError("source root must be a real directory")
    return resolved


def _normalize_required_paths(paths: Sequence[str]) -> tuple[str, ...]:
    if not paths:
        raise SourceProvenanceError("at least one tracked input is required")
    normalized: list[str] = []
    for value in paths:
        path = PurePosixPath(value)
        if (
            not value
            or path.is_absolute()
            or path.as_posix() != value
            or "\\" in value
            or ":" in value
            or any(part in {"", ".", ".."} for part in path.parts)
        ):
            raise SourceProvenanceError("tracked input path must be normalized and repository-relative")
        normalized.append(value)
    if len(set(normalized)) != len(normalized):
        raise SourceProvenanceError("tracked input paths contain a duplicate")
    return tuple(sorted(normalized))


def _read_blob_at_head(root: Path, head_sha: str, relative: str) -> bytes:
    try:
        entry = _git_bytes(
            root,
            "ls-tree",
            "-z",
            "--full-tree",
            head_sha,
            "--",
            relative,
        )
        records = tuple(record for record in entry.split(b"\0") if record)
        if len(records) != 1:
            raise SourceProvenanceError("required source input is not tracked at HEAD")
        metadata, observed_path = records[0].split(b"\t", 1)
        mode, object_type, object_id = metadata.decode("ascii").split(" ", 2)
        if observed_path != relative.encode("utf-8") or mode not in {"100644", "100755"}:
            raise SourceProvenanceError("required source input is not a regular Git blob")
        if object_type != "blob":
            raise SourceProvenanceError("required source input is not a regular Git blob")
        size = int(_git(root, "cat-file", "-s", object_id))
        if size > _MAX_SOURCE_INPUT_BYTES:
            raise SourceProvenanceError("tracked input exceeds the source evidence size limit")
        payload = _git_bytes(root, "cat-file", "blob", object_id)
    except (UnicodeError, ValueError) as exc:
        if isinstance(exc, SourceProvenanceError):
            raise
        raise SourceProvenanceError("required Git blob observation failed") from exc
    if len(payload) != size:
        raise SourceProvenanceError("required Git blob size changed during observation")
    return payload


def _git(root: Path, *arguments: str) -> str:
    try:
        result = subprocess.run(
            git_command(root, *arguments),
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=30,
            env=git_environment(),
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise SourceProvenanceError("required Git source observation failed") from exc
    return result.stdout.strip()


def _git_bytes(root: Path, *arguments: str) -> bytes:
    try:
        result = subprocess.run(
            git_command(root, *arguments),
            check=True,
            capture_output=True,
            timeout=30,
            env=git_environment(),
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise SourceProvenanceError("required Git blob observation failed") from exc
    return result.stdout


def _reject_unsafe_index_flags(root: Path) -> None:
    entries = _git_bytes(root, "ls-files", "-v", "-z")
    for entry in entries.split(b"\0"):
        if not entry:
            continue
        marker = chr(entry[0])
        if marker == "S" or marker.islower():
            raise SourceProvenanceError("tracked index flags can hide source changes")


def _tracked_tree_is_clean(root: Path, head_sha: str) -> bool:
    commands = (
        git_command(root, "diff", "--quiet", "--no-ext-diff", head_sha, "--"),
        git_command(
            root,
            "diff",
            "--cached",
            "--quiet",
            "--no-ext-diff",
            head_sha,
            "--",
        ),
    )
    for command in commands:
        try:
            result = subprocess.run(
                command,
                capture_output=True,
                timeout=30,
                env=git_environment(),
            )
        except (OSError, subprocess.SubprocessError) as exc:
            raise SourceProvenanceError("required Git cleanliness observation failed") from exc
        if result.returncode == 1:
            return False
        if result.returncode != 0:
            raise SourceProvenanceError("required Git cleanliness observation failed")
    return True


def _validate_sha(value: str, label: str) -> str:
    normalized = value.lower()
    if len(normalized) not in {40, 64} or any(character not in "0123456789abcdef" for character in normalized):
        raise SourceProvenanceError(f"{label} is not a Git object ID")
    return normalized
