"""Semantic and archive-safety checks for staged wheel and UPM artifacts."""

from __future__ import annotations

import hashlib
import io
import json
import sys
import tarfile
import zipfile
from dataclasses import replace
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet.artifacts import (  # noqa: E402
    ArtifactError,
    build_artifact_manifest,
    verify_artifact_files,
)
from gauntlet_test_fixtures import write_upm, write_wheel  # noqa: E402


def _artifacts(tmp_path: Path) -> dict[str, Path]:
    wheel = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    upm = tmp_path / "unity-biome-mcp-1.27.0.tgz"
    write_wheel(wheel)
    write_upm(upm)
    return {"python_wheel": wheel, "unity_upm": upm}


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_builder_rejects_unsafe_archive_member(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    if artifact_type == "python_wheel":
        artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
        write_wheel(artifact)
        with zipfile.ZipFile(artifact, "a") as archive:
            archive.writestr("../../outside.py", "unsafe")
    else:
        artifact = tmp_path / "unity-biome-mcp-1.27.0.tgz"
        package_data = json.dumps({"name": "com.unity-biome-mcp.editor", "version": "1.27.0"}).encode()
        with tarfile.open(artifact, "w:gz") as archive:
            package_info = tarfile.TarInfo("package/package.json")
            package_info.size = len(package_data)
            archive.addfile(package_info, io.BytesIO(package_data))
            unsafe = tarfile.TarInfo("../../outside.txt")
            unsafe.size = 1
            archive.addfile(unsafe, io.BytesIO(b"x"))

    with pytest.raises(ArtifactError, match="unsafe member"):
        build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})


@pytest.mark.parametrize(
    ("artifact_type", "filename"),
    [
        ("python_wheel", "unity_biome_mcp-1.27.0-py3-none-any.whl"),
        ("unity_upm", "unity-biome-mcp-1.27.0.tgz"),
    ],
)
def test_manifest_builder_rejects_malformed_package_archive(
    tmp_path: Path,
    artifact_type: str,
    filename: str,
) -> None:
    artifact = tmp_path / filename
    artifact.write_bytes(b"arbitrary bytes are not a package archive")

    with pytest.raises(ArtifactError, match="valid .* archive"):
        build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_builder_rejects_embedded_package_version_mismatch(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    if artifact_type == "python_wheel":
        artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
        write_wheel(artifact, version="1.26.0")
    else:
        artifact = tmp_path / "unity-biome-mcp-1.27.0.tgz"
        write_upm(artifact, version="1.26.0")

    with pytest.raises(ArtifactError, match="embedded package version"):
        build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_builder_rejects_wrong_embedded_package_name(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    if artifact_type == "python_wheel":
        artifact = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
        write_wheel(artifact, name="other")
    else:
        artifact = tmp_path / "unity-biome-mcp-1.27.0.tgz"
        write_upm(artifact, name="com.example.other")

    with pytest.raises(ArtifactError, match="embedded package name"):
        build_artifact_manifest("a" * 40, "1.27.0", {artifact_type: artifact})


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_verification_revalidates_package_archive(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    artifact = paths[artifact_type]
    artifact.write_bytes(b"x" * artifact.stat().st_size)
    records = tuple(
        replace(record, sha256=hashlib.sha256(artifact.read_bytes()).hexdigest())
        if record.artifact_type == artifact_type
        else record
        for record in manifest.artifacts
    )

    with pytest.raises(ArtifactError, match="valid .* archive"):
        verify_artifact_files(replace(manifest, artifacts=records), tmp_path)


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_upm"])
def test_manifest_verification_rejects_embedded_version_mismatch(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    artifact = paths[artifact_type]
    if artifact_type == "python_wheel":
        write_wheel(artifact, version="1.28.0")
    else:
        write_upm(artifact, version="1.28.0")
    records = tuple(
        replace(
            record,
            sha256=hashlib.sha256(artifact.read_bytes()).hexdigest(),
            size_bytes=artifact.stat().st_size,
        )
        if record.artifact_type == artifact_type
        else record
        for record in manifest.artifacts
    )

    with pytest.raises(ArtifactError, match="embedded package version"):
        verify_artifact_files(replace(manifest, artifacts=records), tmp_path)
