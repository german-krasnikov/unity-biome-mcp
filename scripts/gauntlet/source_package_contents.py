"""Canonical package-content digests derived from an exact Git commit."""

from __future__ import annotations

import hashlib
import subprocess
from pathlib import PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.git_process import git_command, git_environment
from gauntlet.package_archive_members import content_digest
from gauntlet.package_contracts import MemberFingerprint

if TYPE_CHECKING:
    from collections.abc import Mapping
    from pathlib import Path

_MAX_MEMBERS = 20_000
_MAX_MEMBER_BYTES = 256 * 1024 * 1024
_MAX_TOTAL_BYTES = 1024 * 1024 * 1024


class SourcePackageContentError(ValueError):
    """Raised when an exact source package tree cannot be observed."""


def observe_package_content_digests(
    root: Path,
    head_sha: str,
    package_roots: Mapping[str, tuple[str, str]],
) -> dict[str, str]:
    return {
        artifact_type: _observe_tree(root, head_sha, source_root, logical_root)
        for artifact_type, (source_root, logical_root) in sorted(package_roots.items())
    }


def _observe_tree(
    root: Path,
    head_sha: str,
    source_root: str,
    logical_root: str,
) -> str:
    output = _git_bytes(root, "ls-tree", "-r", "-z", "--full-tree", head_sha, "--", source_root)
    entries = [record for record in output.split(b"\0") if record]
    if not entries or len(entries) > _MAX_MEMBERS:
        raise SourcePackageContentError("source package member count is invalid")
    fingerprints = []
    total_size = 0
    prefix = f"{source_root}/"
    for entry in entries:
        try:
            metadata, encoded_path = entry.split(b"\t", 1)
            mode, object_type, object_id = metadata.decode("ascii").split(" ", 2)
            source_path = encoded_path.decode("utf-8")
        except (UnicodeError, ValueError) as exc:
            raise SourcePackageContentError("source package Git tree entry is malformed") from exc
        if mode != "100644" or object_type != "blob" or not source_path.startswith(prefix):
            raise SourcePackageContentError("source package contains a non-regular tracked entry")
        relative = source_path.removeprefix(prefix)
        logical_path = PurePosixPath(logical_root, relative).as_posix() if logical_root else relative
        size_text = _git_bytes(root, "cat-file", "-s", object_id).decode("ascii").strip()
        try:
            declared_size = int(size_text)
        except ValueError as exc:
            raise SourcePackageContentError("source package Git blob size is invalid") from exc
        if declared_size > _MAX_MEMBER_BYTES:
            raise SourcePackageContentError("source package member exceeds its size limit")
        payload = _git_bytes(root, "cat-file", "blob", object_id)
        if len(payload) != declared_size:
            raise SourcePackageContentError("source package Git blob size changed during observation")
        total_size += len(payload)
        if total_size > _MAX_TOTAL_BYTES:
            raise SourcePackageContentError("source package tree exceeds its size limit")
        fingerprints.append(
            MemberFingerprint(logical_path, len(payload), hashlib.sha256(payload).hexdigest())
        )
    try:
        return content_digest(tuple(fingerprints))
    except ValueError as exc:
        raise SourcePackageContentError(str(exc)) from exc


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
        raise SourcePackageContentError("source package Git observation failed") from exc
    return result.stdout
