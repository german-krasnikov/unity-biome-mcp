"""Artifact identity tests for build-once/test-once/publish-once releases."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet import artifacts as artifact_module  # noqa: E402
from gauntlet.artifacts import (  # noqa: E402
    ArtifactError,
    build_artifact_manifest,
    load_artifact_manifest,
    verify_artifact_files,
    write_artifact_manifest,
)
from gauntlet_test_fixtures import write_release_artifacts  # noqa: E402


def _artifacts(tmp_path: Path) -> dict[str, Path]:
    return write_release_artifacts(tmp_path)


def _reject_reopen(monkeypatch: pytest.MonkeyPatch, target: Path) -> list[int]:
    open_count = [0]
    delegate = Path.open

    def guarded(file: Path, *args: object, **kwargs: object) -> object:
        if file == target:
            open_count[0] += 1
            if open_count[0] > 1:
                raise AssertionError("artifact was reopened after its first snapshot")
        return delegate(file, *args, **kwargs)

    monkeypatch.setattr(Path, "open", guarded)
    return open_count


def test_manifest_round_trip_proves_exact_artifact_bytes(tmp_path: Path) -> None:
    manifest = build_artifact_manifest("a" * 40, "1.27.0", _artifacts(tmp_path))
    manifest_path = tmp_path / "artifact-manifest.json"

    write_artifact_manifest(manifest_path, manifest)
    loaded = load_artifact_manifest(manifest_path)
    verify_artifact_files(loaded, tmp_path)

    assert loaded == manifest
    assert loaded.product_version == "1.27.0"
    assert set(loaded.artifact_digests) == {
        "python_wheel",
        "unity_editor_upm",
        "unity_reload_upm",
    }
    assert len(loaded.manifest_sha) == 64
    assert not list(tmp_path.glob(".artifact-manifest.json.*.tmp"))


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_builder_reads_each_artifact_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    artifact_type: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    open_count = _reject_reopen(monkeypatch, artifact)

    build_artifact_manifest("a" * 40, "1.27.0", artifacts)

    assert open_count == [1]


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_verification_reads_each_artifact_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    artifact_type: str,
) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    open_count = _reject_reopen(monkeypatch, paths[artifact_type])

    verify_artifact_files(manifest, tmp_path)

    assert open_count == [1]


def test_manifest_builder_bounds_artifact_snapshot(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts["python_wheel"]
    artifact.write_bytes(b"12345")
    monkeypatch.setattr(artifact_module, "_MAX_ARTIFACT_BYTES", 4)

    with pytest.raises(ArtifactError, match="safety limit"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_builder_rejects_unknown_artifact_type(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = tmp_path / "generic.bin"
    artifact.write_bytes(b"arbitrary")
    artifacts.pop("unity_reload_upm")
    artifacts["generic"] = artifact

    with pytest.raises(ArtifactError, match="artifact set"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    ("artifact_type", "filename"),
    [
        ("python_wheel", "totally_other-9.9.9-py3-none-any.whl"),
        ("python_wheel", "unity_biome_mcp-1.27.0.bin"),
        ("unity_editor_upm", "totally-other-1.27.0.tgz"),
        ("unity_editor_upm", "unity-biome-mcp-editor-1.27.0.bin"),
        ("unity_reload_upm", "totally-other-0.1.4.tgz"),
    ],
)
def test_manifest_builder_rejects_artifact_type_filename_mismatch(
    tmp_path: Path,
    artifact_type: str,
    filename: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    original = artifacts[artifact_type]
    artifact = tmp_path / filename
    original.rename(artifact)
    artifacts[artifact_type] = artifact

    with pytest.raises(ArtifactError, match="filename"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_builder_rejects_symlinked_artifact(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    original = artifacts["python_wheel"]
    outside = tmp_path / "outside.whl"
    original.rename(outside)
    artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    try:
        artifact.symlink_to(outside)
    except OSError as exc:
        pytest.skip(f"symlinks are unavailable on this platform: {exc}")

    with pytest.raises(ArtifactError, match="stable regular file"):
        artifacts["python_wheel"] = artifact
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_verification_detects_substitution(tmp_path: Path) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    paths["python_wheel"].write_bytes(b"substituted")

    with pytest.raises(ArtifactError, match="size|digest"):
        verify_artifact_files(manifest, tmp_path)


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda data: data.update({"unknown": True}), "schema"),
        (lambda data: data.update({"schema_version": 1}), "schema|unsupported"),
        (lambda data: data.update({"head_sha": "not-a-sha"}), "head"),
        (lambda data: data.update({"manifest_sha": "0" * 64}), "digest"),
        (lambda data: data["artifacts"][0].update({"filename": "../escape.whl"}), "filename"),
        (lambda data: data["artifacts"][0].update({"size_bytes": -1}), "size"),
        (lambda data: data["artifacts"].append(dict(data["artifacts"][0])), "duplicate"),
    ],
)
def test_manifest_loader_rejects_ambiguous_or_tampered_data(
    tmp_path: Path,
    mutate: object,
    message: str,
) -> None:
    manifest = build_artifact_manifest("a" * 40, "1.27.0", _artifacts(tmp_path))
    path = tmp_path / "artifact-manifest.json"
    write_artifact_manifest(path, manifest)
    data = json.loads(path.read_text(encoding="utf-8"))
    assert callable(mutate)
    mutate(data)
    path.write_text(json.dumps(data), encoding="utf-8")

    with pytest.raises(ArtifactError, match=message):
        load_artifact_manifest(path)


def test_manifest_builder_rejects_missing_artifact_file(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    artifacts["python_wheel"].unlink()
    missing = tmp_path / "missing.whl"
    artifacts["python_wheel"] = missing
    with pytest.raises(ArtifactError, match="regular file"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_loader_rejects_duplicate_type_with_distinct_filename(tmp_path: Path) -> None:
    manifest = build_artifact_manifest("a" * 40, "1.27.0", _artifacts(tmp_path))
    path = tmp_path / "artifact-manifest.json"
    write_artifact_manifest(path, manifest)
    data = json.loads(path.read_text(encoding="utf-8"))
    wheel = next(record for record in data["artifacts"] if record["type"] == "python_wheel")
    duplicate = dict(wheel)
    duplicate["filename"] = "unity_biome_mcp-1.27.0-1-py3-none-any.whl"
    data["artifacts"].append(duplicate)
    path.write_text(json.dumps(data), encoding="utf-8")

    with pytest.raises(ArtifactError, match="duplicate or incomplete"):
        load_artifact_manifest(path)
