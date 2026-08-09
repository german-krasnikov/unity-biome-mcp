"""Adversarial stability checks for bounded release-artifact observations."""

from __future__ import annotations

import os
import sys
from pathlib import Path
from typing import TYPE_CHECKING

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet.artifacts import (
    ArtifactError,
    build_artifact_manifest,
    verify_artifact_files,
)
from gauntlet_test_fixtures import write_release_artifacts

if TYPE_CHECKING:
    from collections.abc import Callable
    from typing import BinaryIO

    from gauntlet.artifact_contracts import ArtifactManifest
    from typing_extensions import Self


class _AfterReadStream:
    def __init__(self, stream: BinaryIO, after_read: Callable[[bytes], None]) -> None:
        self._stream = stream
        self._after_read = after_read
        self._triggered = False

    def __enter__(self) -> Self:
        self._stream.__enter__()
        return self

    def __exit__(self, *args: object) -> object:
        return self._stream.__exit__(*args)

    def fileno(self) -> int:
        return self._stream.fileno()

    def read(self, size: int = -1) -> bytes:
        payload = self._stream.read(size)
        if not self._triggered:
            self._triggered = True
            self._after_read(payload)
        return payload


def _patch_target_read(
    monkeypatch: pytest.MonkeyPatch,
    target: Path,
    after_read: Callable[[bytes], None],
) -> list[int]:
    delegate = Path.open
    mutation_count = [0]

    def mutate(payload: bytes) -> None:
        mutation_count[0] += 1
        after_read(payload)

    def patched(path: Path, *args: object, **kwargs: object) -> object:
        stream = delegate(path, *args, **kwargs)
        if path == target:
            return _AfterReadStream(stream, mutate)
        return stream

    monkeypatch.setattr(Path, "open", patched)
    return mutation_count


def _mutate_same_size(path: Path, payload: bytes) -> None:
    before = path.stat()
    descriptor = os.open(path, os.O_RDWR)
    try:
        first = os.read(descriptor, 1)
        os.lseek(descriptor, 0, os.SEEK_SET)
        os.write(descriptor, b"\x00" if first != b"\x00" else b"\x01")
        os.fsync(descriptor)
    finally:
        os.close(descriptor)
    after = path.stat()
    if (after.st_mtime_ns, after.st_ctime_ns) == (before.st_mtime_ns, before.st_ctime_ns):
        os.utime(path, ns=(after.st_atime_ns, before.st_mtime_ns + 2_000_000_000))
    assert path.stat().st_size == len(payload)


def _replace_same_path(path: Path, payload: bytes) -> None:
    replacement = path.with_name(f".{path.name}.replacement")
    replacement.write_bytes(payload)
    os.replace(replacement, path)


def _invoke(operation: str, artifacts: dict[str, Path], manifest: ArtifactManifest) -> None:
    if operation == "build":
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    else:
        verify_artifact_files(manifest, artifacts["python_wheel"].parent)


@pytest.mark.parametrize("operation", ["build", "verify"])
def test_artifact_observation_same_size_mutation_during_read_is_rejected(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    operation: str,
) -> None:
    artifacts = write_release_artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    target = artifacts["python_wheel"]
    count = _patch_target_read(monkeypatch, target, lambda payload: _mutate_same_size(target, payload))

    with pytest.raises(ArtifactError):
        _invoke(operation, artifacts, manifest)

    assert count == [1]


@pytest.mark.parametrize("operation", ["build", "verify"])
def test_artifact_observation_path_replacement_during_read_is_rejected(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    operation: str,
) -> None:
    artifacts = write_release_artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    target = artifacts["python_wheel"]
    count = _patch_target_read(monkeypatch, target, lambda payload: _replace_same_path(target, payload))

    with pytest.raises(ArtifactError):
        _invoke(operation, artifacts, manifest)

    assert count == [1]
