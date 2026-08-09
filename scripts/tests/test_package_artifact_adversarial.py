"""Adversarial package-identity regressions at the release boundary."""

from __future__ import annotations

import base64
import csv
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
from gauntlet.python_package_contract import (  # noqa: E402
    source_python_contract,
    wheel_python_contract,
)
from gauntlet.release_gate import GateError  # noqa: E402
from gauntlet_test_fixtures import write_release_artifacts  # noqa: E402
from release_gate_mutation_support import rewrite_receipt  # noqa: E402
from release_gate_test_support import prepare_bundle, validate_bundle  # noqa: E402


def _record_hash(payload: bytes) -> str:
    value = base64.urlsafe_b64encode(hashlib.sha256(payload).digest()).rstrip(b"=")
    return f"sha256={value.decode('ascii')}"


def _write_wheel(
    path: Path,
    *,
    wheel_metadata: bytes | None = None,
    case_colliding_record: bool = False,
    metadata_dependency: str | None = None,
    entry_points: bytes | None = None,
    extra_members: tuple[tuple[str, bytes], ...] = (),
    member_date_time: tuple[int, int, int, int, int, int] = (2020, 2, 2, 0, 0, 0),
    external_mode: int | None = None,
) -> None:
    dist_info = "unity_biome_mcp-1.27.0.dist-info"
    members = {
        f"{dist_info}/METADATA": (
            b"Metadata-Version: 2.1\nName: unity-biome-mcp\nVersion: 1.27.0\n"
            + (f"Requires-Dist: {metadata_dependency}\n".encode() if metadata_dependency else b"")
            + b"\n"
        ),
        f"{dist_info}/WHEEL": wheel_metadata
        or b"Wheel-Version: 1.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        "unity_mcp/__init__.py": b"__version__ = 'test'\n",
    }
    record_name = f"{dist_info}/RECORD"
    if case_colliding_record:
        members[f"{dist_info}/record"] = b"collision"
    if entry_points is not None:
        members[f"{dist_info}/entry_points.txt"] = entry_points
    members.update(extra_members)
    output = io.StringIO(newline="")
    writer = csv.writer(output, lineterminator="\n")
    for name, payload in sorted(members.items()):
        writer.writerow((name, _record_hash(payload), len(payload)))
    writer.writerow((record_name, "", ""))
    members[record_name] = output.getvalue().encode()
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, payload in members.items():
            info = zipfile.ZipInfo(name)
            info.date_time = member_date_time
            canonical_mode = 0o100644 if name.startswith("unity_mcp/") else 0o644
            info.external_attr = (external_mode or canonical_mode) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, payload)


def _write_upm(path: Path, extra: tuple[tuple[str, bytes], ...]) -> None:
    members = (
        (
            "package/package.json",
            json.dumps(
                {"name": "com.unity-biome-mcp.editor", "version": "1.27.0"}
            ).encode(),
        ),
        *extra,
    )
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        for name, payload in members:
            info = tarfile.TarInfo(name)
            info.size = len(payload)
            info.mtime = 499_162_500
            archive.addfile(info, io.BytesIO(payload))
    compressed = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=compressed, mtime=0) as stream:
        stream.write(output.getvalue())
    path.write_bytes(compressed.getvalue())


def test_release_gate_rejects_package_payload_not_built_from_observed_source(
    tmp_path: Path,
) -> None:
    paths = prepare_bundle(tmp_path, source_python_payload="__version__ = 'different'\n")

    with pytest.raises(GateError, match="content|observed source"):
        validate_bundle(paths)


@pytest.mark.parametrize("attack", ["extra-package", "entry-point", "dependency"])
def test_release_gate_rejects_unreviewed_wheel_install_semantics(
    tmp_path: Path,
    attack: str,
) -> None:
    def mutate(artifacts: dict[str, Path]) -> None:
        options: dict[str, object] = {}
        if attack == "extra-package":
            options["extra_members"] = (("evil/__init__.py", b"def main(): pass\n"),)
        elif attack == "entry-point":
            options["entry_points"] = b"[console_scripts]\nunity-biome-mcp = evil:main\n"
        else:
            options["metadata_dependency"] = "attacker-controlled>=1"
        _write_wheel(artifacts["python_wheel"], **options)

    with pytest.raises((ArtifactError, GateError), match="wheel|contract|source|member"):
        paths = prepare_bundle(tmp_path, artifact_mutator=mutate)
        validate_bundle(paths)


@pytest.mark.parametrize("attack", ["timestamp", "external-mode"])
def test_release_gate_rejects_noncanonical_wheel_member_metadata(
    tmp_path: Path,
    attack: str,
) -> None:
    def mutate(artifacts: dict[str, Path]) -> None:
        options: dict[str, object] = {}
        if attack == "timestamp":
            options["member_date_time"] = (2098, 12, 31, 23, 58, 0)
        else:
            options["external_mode"] = 0o4644
        _write_wheel(artifacts["python_wheel"], **options)

    with pytest.raises((ArtifactError, GateError), match="timestamp|mode|metadata.*canonical"):
        paths = prepare_bundle(tmp_path, artifact_mutator=mutate)
        validate_bundle(paths)


def test_wheel_rejects_case_collision_even_when_record_is_excluded(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_wheel(artifacts["python_wheel"], case_colliding_record=True)

    with pytest.raises(ArtifactError, match="collision"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize(
    "member",
    [
        "package/Editor/a:b.cs",
        "package/Editor/trailing. ",
        "package/Editor/CON.txt",
        "package/Editor/control\x01.cs",
    ],
)
def test_upm_rejects_nonportable_member_paths(tmp_path: Path, member: str) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_upm(artifacts["unity_editor_upm"], ((member, b"x"),))

    with pytest.raises(ArtifactError, match="unsafe|portable"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_upm_rejects_file_as_ancestor_of_another_member(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_upm(
        artifacts["unity_editor_upm"],
        (("package/Editor", b"file"), ("package/Editor/A.cs", b"child")),
    )

    with pytest.raises(ArtifactError, match="ancestor|collision"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_wheel_rejects_contradictory_wheel_metadata(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_wheel(
        artifacts["python_wheel"],
        wheel_metadata=(
            b"Wheel-Version: 99.0\n"
            b"Root-Is-Purelib: false\n"
            b"Tag: cp313-cp313-win_amd64\n"
        ),
    )

    with pytest.raises(ArtifactError, match="WHEEL|tag|purelib"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


@pytest.mark.parametrize("artifact_type", ["python_wheel", "unity_editor_upm", "unity_reload_upm"])
def test_archive_rejects_unaccounted_trailing_bytes(
    tmp_path: Path,
    artifact_type: str,
) -> None:
    artifacts = write_release_artifacts(tmp_path)
    artifact = artifacts[artifact_type]
    artifact.write_bytes(artifact.read_bytes() + b"UNACCOUNTED-SECRET-BYTES")

    with pytest.raises(ArtifactError, match="trailing|container|gzip|archive"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_verifier_rejects_invalid_in_memory_manifest_envelope(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    manifest = build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    duplicate = replace(manifest, artifacts=manifest.artifacts + (manifest.artifacts[0],))

    with pytest.raises(ArtifactError, match="duplicate|exactly|manifest"):
        verify_artifact_files(duplicate, tmp_path)
    with pytest.raises(ArtifactError, match="manifest|digest"):
        verify_artifact_files(replace(manifest, manifest_sha="0" * 64), tmp_path)


@pytest.mark.parametrize("version", ["01.2.3", "1.2.3-01", "1.2.3-..", "1.2.3+"])
def test_manifest_rejects_noncanonical_semver(tmp_path: Path, version: str) -> None:
    with pytest.raises(ArtifactError, match="semantic version"):
        build_artifact_manifest("a" * 40, version, write_release_artifacts(tmp_path))


def test_release_gate_rejects_runtime_missing_reload_package(tmp_path: Path) -> None:
    paths = prepare_bundle(tmp_path)
    manifest = json.loads(paths["manifest"].read_text(encoding="utf-8"))
    digests = {record["type"]: record["archive_sha256"] for record in manifest["artifacts"]}
    rewrite_receipt(
        paths,
        "runtime",
        "runtime",
        {
            "consumed_artifacts": {
                "python_wheel": digests["python_wheel"],
                "unity_editor_upm": digests["unity_editor_upm"],
            }
        },
    )

    with pytest.raises(GateError, match="runtime identity"):
        validate_bundle(paths)


def test_requirement_contract_preserves_whitespace_inside_marker_literals() -> None:
    from email.parser import BytesParser

    source = source_python_contract(
        {"dependencies": ['foo; os_name == "a b"']}
    )
    metadata = BytesParser().parsebytes(
        b"Metadata-Version: 2.1\nRequires-Dist: foo; os_name == \"ab\"\n\n"
    )

    assert source != wheel_python_contract(metadata, None)
