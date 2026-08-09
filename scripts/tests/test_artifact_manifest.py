"""Artifact identity tests for build-once/test-once/publish-once releases."""

from __future__ import annotations

import builtins
import io
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
from gauntlet_test_fixtures import write_upm as _write_upm  # noqa: E402
from gauntlet_test_fixtures import write_wheel as _write_wheel  # noqa: E402


def _artifacts(tmp_path: Path) -> dict[str, Path]:
    wheel = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    upm = tmp_path / "unity-biome-mcp-1.27.0.tgz"
    _write_wheel(wheel)
    _write_upm(upm)
    return {"python_wheel": wheel, "unity_upm": upm}


def _reject_reopen(monkeypatch: pytest.MonkeyPatch, target: Path) -> list[int]:
    open_count = [0]

    def guard(delegate: object) -> object:
        assert callable(delegate)

        def guarded(file: object, *args: object, **kwargs: object) -> object:
            try:
                is_target = Path(file) == target
            except TypeError:
                is_target = False
            if is_target:
                open_count[0] += 1
                if open_count[0] > 1:
                    raise AssertionError("artifact was reopened after its first snapshot")
            return delegate(file, *args, **kwargs)

        return guarded

    accessor_type = type(target._accessor)  # noqa: SLF001 - observe the real filesystem boundary
    monkeypatch.setattr(accessor_type, "open", staticmethod(guard(accessor_type.open)))
    monkeypatch.setattr(io, "open", guard(io.open))
    monkeypatch.setattr(builtins, "open", guard(builtins.open))
    return open_count


def test_manifest_round_trip_proves_exact_artifact_bytes(tmp_path: Path) -> None:
    manifest = build_artifact_manifest("a" * 40, "1.27.0", _artifacts(tmp_path))
    manifest_path = tmp_path / "artifact-manifest.json"

    write_artifact_manifest(manifest_path, manifest)
    loaded = load_artifact_manifest(manifest_path)
    verify_artifact_files(loaded, tmp_path)

    assert loaded == manifest
    assert loaded.package_version == "1.27.0"
    assert set(loaded.artifact_digests) == {"python_wheel", "unity_upm"}
    assert len(loaded.manifest_sha) == 64
    assert not list(tmp_path.glob(".artifact-manifest.json.*.tmp"))


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_builder_reads_each_artifact_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    artifact_type: str,
) -> None:
    artifact = _artifacts(tmp_path)[artifact_type]
    open_count = _reject_reopen(monkeypatch, artifact)

    build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})

    assert open_count == [1]


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
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
    artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    artifact.write_bytes(b"12345")
    monkeypatch.setattr(artifact_module, "_MAX_ARTIFACT_BYTES", 4)

    with pytest.raises(ArtifactError, match="safety limit"):
        build_artifact_manifest("a" * 40, "1.27.0", {"python_wheel": artifact})


def test_manifest_builder_rejects_unknown_artifact_type(tmp_path: Path) -> None:
    artifact = tmp_path / "generic.bin"
    artifact.write_bytes(b"arbitrary")

    with pytest.raises(ArtifactError, match="unsupported release artifact type"):
        build_artifact_manifest("a" * 40, "1.27.0", {"generic": artifact})


@pytest.mark.parametrize(
    ("artifact_type", "filename"),
    [
        ("python_wheel", "totally_other-9.9.9-py3-none-any.whl"),
        ("python_wheel", "unity_biome_mcp-1.27.0.bin"),
        ("unity_upm", "totally-other-1.27.0.tgz"),
        ("unity_upm", "unity-biome-mcp-1.27.0.bin"),
    ],
)
def test_manifest_builder_rejects_artifact_type_filename_mismatch(
    tmp_path: Path,
    artifact_type: str,
    filename: str,
) -> None:
    artifact = tmp_path / filename
    if artifact_type == "python_wheel":
        _write_wheel(artifact)
    else:
        _write_upm(artifact)

    with pytest.raises(ArtifactError, match="artifact filename"):
        build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})


def test_manifest_builder_rejects_symlinked_artifact(tmp_path: Path) -> None:
    outside = tmp_path / "outside.whl"
    _write_wheel(outside)
    artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    try:
        artifact.symlink_to(outside)
    except OSError as exc:
        pytest.skip(f"symlinks are unavailable on this platform: {exc}")

    with pytest.raises(ArtifactError, match="stable regular file"):
        build_artifact_manifest("a" * 40, "1.27.0", {"python_wheel": artifact})


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
    missing = tmp_path / "missing.whl"
    with pytest.raises(ArtifactError, match="regular file"):
        build_artifact_manifest("a" * 40, "1.27.0", {"python_wheel": missing})
