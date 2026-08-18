"""Artifact manifest v2 contracts for the complete released product."""


import base64
import csv
import gzip
import hashlib
import io
import json
import sys
import tarfile
import zipfile
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))

from gauntlet.artifacts import ArtifactError, build_artifact_manifest  # noqa: E402

PRODUCT_VERSION = "1.27.0"
RELOAD_VERSION = "0.1.4"


def _record_hash(payload: bytes) -> str:
    digest = base64.urlsafe_b64encode(hashlib.sha256(payload).digest()).rstrip(b"=")
    return f"sha256={digest.decode('ascii')}"


def _write_wheel(
    path: Path,
    *,
    record_failure: str | None = None,
    reverse_record: bool = False,
) -> None:
    dist_info = f"unity_biome_mcp-{PRODUCT_VERSION}.dist-info"
    members = {
        f"{dist_info}/METADATA": (
            f"Metadata-Version: 2.1\nName: unity-biome-mcp\nVersion: {PRODUCT_VERSION}\n\n"
        ).encode(),
        f"{dist_info}/WHEEL": b"Wheel-Version: 1.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        "unity_mcp/__init__.py": b"__version__ = '1.27.0'\n",
    }
    output = io.StringIO(newline="")
    writer = csv.writer(output, lineterminator="\n")
    rows = [
        [name, _record_hash(payload), str(len(payload))]
        for name, payload in sorted(members.items())
    ]
    if record_failure == "missing":
        rows = [row for row in rows if row[0] != "unity_mcp/__init__.py"]
    elif record_failure == "hash":
        rows[-1][1] = "sha256=AAAAAAAA"
    elif record_failure == "size":
        rows[-1][2] = "999"
    elif record_failure == "duplicate":
        rows.append(list(rows[-1]))
    elif record_failure == "unknown":
        rows.append(["unity_mcp/unknown.py", _record_hash(b"unknown"), "7"])
    if reverse_record:
        rows.reverse()
    writer.writerows(rows)
    record_name = f"{dist_info}/RECORD"
    writer.writerow((record_name, "sha256=AAAAAAAA" if record_failure == "self" else "", ""))
    members[record_name] = b"\xff" if record_failure == "encoding" else output.getvalue().encode()
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, payload in members.items():
            info = zipfile.ZipInfo(name)
            info.date_time = (2020, 2, 2, 0, 0, 0)
            mode = 0o100644 if name.startswith("unity_mcp/") else 0o644
            info.external_attr = mode << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, payload)


def _write_upm(
    path: Path,
    *,
    name: str,
    version: str,
    payload: bytes = b"package-content",
    reverse: bool = False,
) -> None:
    members = [
        ("package/package.json", json.dumps({"name": name, "version": version}).encode()),
        ("package/Editor/Marker.txt", payload),
    ]
    if reverse:
        members.reverse()
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w", format=tarfile.USTAR_FORMAT) as archive:
        for member_name, member_payload in members:
            info = tarfile.TarInfo(member_name)
            info.size = len(member_payload)
            info.mtime = 499_162_500
            archive.addfile(info, io.BytesIO(member_payload))
    compressed = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=compressed, mtime=0) as stream:
        stream.write(output.getvalue())
    path.write_bytes(compressed.getvalue())


def _artifacts(tmp_path: Path, *, reverse_editor: bool = False) -> dict[str, Path]:
    tmp_path.mkdir(parents=True, exist_ok=True)
    wheel = tmp_path / f"unity_biome_mcp-{PRODUCT_VERSION}-py3-none-any.whl"
    editor = tmp_path / f"com.unity-biome-mcp.editor-{PRODUCT_VERSION}.tgz"
    reload = tmp_path / f"com.unity-biome-mcp.reload-{RELOAD_VERSION}.tgz"
    _write_wheel(wheel)
    _write_upm(
        editor,
        name="com.unity-biome-mcp.editor",
        version=PRODUCT_VERSION,
        reverse=reverse_editor,
    )
    _write_upm(reload, name="com.unity-biome-mcp.reload", version=RELOAD_VERSION)
    return {
        "python_wheel": wheel,
        "unity_editor_upm": editor,
        "unity_reload_upm": reload,
    }


def test_v2_manifest_binds_three_package_identities_and_two_digests(tmp_path: Path) -> None:
    manifest = build_artifact_manifest("a" * 40, PRODUCT_VERSION, _artifacts(tmp_path))

    assert manifest.product_version == PRODUCT_VERSION
    assert [record.artifact_type for record in manifest.artifacts] == [
        "python_wheel",
        "unity_editor_upm",
        "unity_reload_upm",
    ]
    assert [record.package_name for record in manifest.artifacts] == [
        "unity-biome-mcp",
        "com.unity-biome-mcp.editor",
        "com.unity-biome-mcp.reload",
    ]
    assert [record.package_version for record in manifest.artifacts] == [
        PRODUCT_VERSION,
        PRODUCT_VERSION,
        RELOAD_VERSION,
    ]
    assert set(manifest.archive_digests) == set(manifest.content_digests) == {
        "python_wheel",
        "unity_editor_upm",
        "unity_reload_upm",
    }
    assert all(len(value) == 64 for value in manifest.archive_digests.values())
    assert all(len(value) == 64 for value in manifest.content_digests.values())


def test_v2_manifest_rejects_partial_or_legacy_artifact_sets(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    artifacts.pop("unity_reload_upm")
    with pytest.raises(ArtifactError, match="exactly.*three|artifact set"):
        build_artifact_manifest("a" * 40, PRODUCT_VERSION, artifacts)

    complete = _artifacts(tmp_path / "legacy")
    legacy_path = complete.pop("unity_editor_upm")
    complete["unity_upm"] = legacy_path
    with pytest.raises(ArtifactError, match="artifact set|unsupported"):
        build_artifact_manifest("a" * 40, PRODUCT_VERSION, complete)


def test_v2_manifest_rejects_reload_filename_version_mismatch(tmp_path: Path) -> None:
    artifacts = _artifacts(tmp_path)
    reload = artifacts["unity_reload_upm"]
    renamed = tmp_path / "com.unity-biome-mcp.reload-9.9.9.tgz"
    reload.rename(renamed)
    artifacts["unity_reload_upm"] = renamed

    with pytest.raises(ArtifactError, match="filename.*package identity"):
        build_artifact_manifest("a" * 40, PRODUCT_VERSION, artifacts)


@pytest.mark.parametrize(
    "record_failure",
    ["missing", "hash", "size", "duplicate", "unknown", "self", "encoding"],
)
def test_v2_manifest_rejects_invalid_wheel_record(tmp_path: Path, record_failure: str) -> None:
    artifacts = _artifacts(tmp_path)
    wheel = artifacts["python_wheel"]
    _write_wheel(wheel, record_failure=record_failure)

    with pytest.raises(ArtifactError, match="RECORD"):
        build_artifact_manifest("a" * 40, PRODUCT_VERSION, artifacts)


def test_v2_content_digest_is_stable_across_container_member_order(tmp_path: Path) -> None:
    first_root = tmp_path / "first"
    second_root = tmp_path / "second"
    first_root.mkdir()
    second_root.mkdir()
    first = build_artifact_manifest("a" * 40, PRODUCT_VERSION, _artifacts(first_root))
    second = build_artifact_manifest(
        "a" * 40,
        PRODUCT_VERSION,
        _artifacts(second_root, reverse_editor=True),
    )

    assert first.content_digests["unity_editor_upm"] == second.content_digests["unity_editor_upm"]
    assert first.archive_digests["unity_editor_upm"] != second.archive_digests["unity_editor_upm"]


def test_v2_wheel_content_digest_ignores_record_row_order(tmp_path: Path) -> None:
    first_root = tmp_path / "first"
    second_root = tmp_path / "second"
    first = _artifacts(first_root)
    second = _artifacts(second_root)
    _write_wheel(second["python_wheel"], reverse_record=True)

    first_manifest = build_artifact_manifest("a" * 40, PRODUCT_VERSION, first)
    second_manifest = build_artifact_manifest("a" * 40, PRODUCT_VERSION, second)

    assert first_manifest.content_digests["python_wheel"] == second_manifest.content_digests["python_wheel"]
    assert first_manifest.archive_digests["python_wheel"] != second_manifest.archive_digests["python_wheel"]


def test_v2_content_digest_changes_with_logical_member_bytes(tmp_path: Path) -> None:
    first_root = tmp_path / "first"
    second_root = tmp_path / "second"
    first_root.mkdir()
    second_root.mkdir()
    first = build_artifact_manifest("a" * 40, PRODUCT_VERSION, _artifacts(first_root))
    changed = _artifacts(second_root)
    _write_upm(
        changed["unity_editor_upm"],
        name="com.unity-biome-mcp.editor",
        version=PRODUCT_VERSION,
        payload=b"changed-content",
    )
    second = build_artifact_manifest("a" * 40, PRODUCT_VERSION, changed)

    assert first.content_digests["unity_editor_upm"] != second.content_digests["unity_editor_upm"]
