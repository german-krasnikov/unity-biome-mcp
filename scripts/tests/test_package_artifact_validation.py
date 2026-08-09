"""Semantic and archive-safety checks for staged wheel and UPM artifacts."""

from __future__ import annotations

import gzip
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
from gauntlet.receipts import content_hash  # noqa: E402
from gauntlet_test_fixtures import (  # noqa: E402
    write_release_artifacts,
    write_upm,
    write_wheel,
)


def _artifacts(tmp_path: Path) -> dict[str, Path]:
    return write_release_artifacts(tmp_path)


def _package_identity(artifact_type: str) -> tuple[str, str]:
    if artifact_type == "unity_reload_upm":
        return "com.unity-biome-mcp.reload", "0.1.4"
    if artifact_type == "unity_editor_upm":
        return "com.unity-biome-mcp.editor", "1.27.0"
    return "unity-biome-mcp", "1.27.0"


def _write_tar(path: Path, members: dict[str, bytes]) -> None:
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        for name, payload in members.items():
            info = tarfile.TarInfo(name)
            info.size = len(payload)
            info.mtime = 499_162_500
            archive.addfile(info, io.BytesIO(payload))
    compressed = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=compressed, mtime=0) as stream:
        stream.write(output.getvalue())
    path.write_bytes(compressed.getvalue())


def _with_records(manifest: object, records: tuple[object, ...]) -> object:
    payload = {
        "schema_version": 2,
        "head_sha": manifest.head_sha,
        "product_version": manifest.product_version,
        "artifacts": [
            {
                "type": record.artifact_type,
                "filename": record.filename,
                "package_name": record.package_name,
                "package_version": record.package_version,
                "archive_sha256": record.archive_sha256,
                "content_sha256": record.content_sha256,
                "size_bytes": record.size_bytes,
            }
            for record in records
        ],
    }
    return replace(manifest, artifacts=records, manifest_sha=content_hash(payload))


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_builder_rejects_unsafe_archive_member(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    if artifact_type == "python_wheel":
        write_wheel(artifact)
        with zipfile.ZipFile(artifact, "a") as archive:
            archive.writestr("../../outside.py", "unsafe")
    else:
        name, version = _package_identity(artifact_type)
        package_data = json.dumps({"name": name, "version": version}).encode()
        _write_tar(
            artifact,
            {"package/package.json": package_data, "../../outside.txt": b"x"},
        )

    with pytest.raises(ArtifactError, match="unsafe member"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    ("artifact_type", "filename"),
    [
        ("python_wheel", "unity_biome_mcp-1.27.0-py3-none-any.whl"),
        ("unity_editor_upm", "com.unity-biome-mcp.editor-1.27.0.tgz"),
        ("unity_reload_upm", "com.unity-biome-mcp.reload-0.1.4.tgz"),
    ],
)
def test_manifest_builder_rejects_malformed_package_archive(
    tmp_path: Path,
    artifact_type: str,
    filename: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    artifact.write_bytes(b"arbitrary bytes are not a package archive")

    with pytest.raises(ArtifactError, match="valid .* archive|container|before|canonical"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_builder_rejects_embedded_package_version_mismatch(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    if artifact_type == "python_wheel":
        write_wheel(artifact, version="1.26.0")
    elif artifact_type == "unity_editor_upm":
        write_upm(artifact, version="1.26.0", name="com.unity-biome-mcp.editor")
    else:
        write_upm(artifact, version="0.1.3", name="com.unity-biome-mcp.reload")

    expected = "embedded package version|filename does not match"
    with pytest.raises(ArtifactError, match=expected):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_builder_rejects_wrong_embedded_package_name(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    if artifact_type == "python_wheel":
        write_wheel(artifact, name="other")
    else:
        _, version = _package_identity(artifact_type)
        write_upm(artifact, version=version, name="com.example.other")

    with pytest.raises(ArtifactError, match="embedded package name"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_builder_rejects_duplicate_upm_package_json_keys(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts["unity_editor_upm"]
    package_data = (
        b'{"name":"com.unity-biome-mcp.editor",'
        b'"name":"com.example.other","version":"1.27.0"}'
    )
    _write_tar(artifact, {"package/package.json": package_data})

    with pytest.raises(ArtifactError, match="duplicate key"):
        build_artifact_manifest(
            "a" * 40,
            "1.27.0",
            artifacts,
        )


def test_manifest_builder_rejects_case_colliding_upm_paths(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    artifact = artifacts["unity_editor_upm"]
    members = {
        "package/package.json": json.dumps(
            {"name": "com.unity-biome-mcp.editor", "version": "1.27.0"}
        ).encode(),
        "package/Editor/Marker.txt": b"first",
        "package/editor/marker.txt": b"second",
    }
    _write_tar(artifact, members)

    with pytest.raises(ArtifactError, match="canonical path collision"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_manifest_builder_rejects_invalid_reload_semver(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    write_upm(
        artifacts["unity_reload_upm"],
        version="not-semver",
        name="com.unity-biome-mcp.reload",
    )

    with pytest.raises(ArtifactError, match="package version is invalid"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_verification_revalidates_package_archive(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    artifact = paths[artifact_type]
    artifact.write_bytes(b"x" * artifact.stat().st_size)
    records = tuple(
        replace(record, archive_sha256=hashlib.sha256(artifact.read_bytes()).hexdigest())
        if record.artifact_type == artifact_type
        else record
        for record in manifest.artifacts
    )

    with pytest.raises(ArtifactError, match="valid .* archive|container|before|canonical"):
        verify_artifact_files(_with_records(manifest, records), tmp_path)


@pytest.mark.parametrize(
    "artifact_type",
    ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
)
def test_manifest_verification_rejects_embedded_version_mismatch(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    paths = _artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", paths)
    artifact = paths[artifact_type]
    if artifact_type == "python_wheel":
        write_wheel(artifact, version="1.28.0")
    elif artifact_type == "unity_editor_upm":
        write_upm(artifact, version="1.28.0", name="com.unity-biome-mcp.editor")
    else:
        write_upm(artifact, version="0.1.5", name="com.unity-biome-mcp.reload")
    records = tuple(
        replace(
            record,
            archive_sha256=hashlib.sha256(artifact.read_bytes()).hexdigest(),
            size_bytes=artifact.stat().st_size,
        )
        if record.artifact_type == artifact_type
        else record
        for record in manifest.artifacts
    )

    with pytest.raises(ArtifactError, match="package version|filename does not match"):
        verify_artifact_files(_with_records(manifest, records), tmp_path)
