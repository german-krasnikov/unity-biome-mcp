"""Container-framing and portable-install regressions for release packages."""

from __future__ import annotations

import gzip
import io
import json
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet.artifacts import ArtifactError, build_artifact_manifest  # noqa: E402
from gauntlet.source_package_contents import (  # noqa: E402
    SourcePackageContentError,
    observe_package_content_digests,
)
from gauntlet_test_fixtures import write_release_artifacts  # noqa: E402


def _git(root: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *arguments],
        check=True,
        capture_output=True,
        encoding="utf-8",
        text=True,
    )
    return result.stdout.strip()


def _write_editor_upm(
    path: Path,
    *,
    member_name: str = "package/Editor/Marker.txt",
    member_mode: int = 0o644,
    pax_headers: dict[str, str] | None = None,
) -> None:
    members = (
        (
            "package/package.json",
            json.dumps(
                {"name": "com.unity-biome-mcp.editor", "version": "1.27.0"}
            ).encode(),
            0o644,
            None,
        ),
        (member_name, b"content", member_mode, pax_headers),
    )
    output = io.BytesIO()
    needs_extended_path = any(len(part.encode("utf-8")) > 100 for part in member_name.split("/"))
    archive_format = tarfile.PAX_FORMAT if pax_headers or needs_extended_path else tarfile.USTAR_FORMAT
    with tarfile.open(fileobj=output, mode="w", format=archive_format) as archive:
        for name, payload, mode, headers in members:
            info = tarfile.TarInfo(name)
            info.size = len(payload)
            info.mode = mode
            info.mtime = 499_162_500
            if headers:
                info.pax_headers = headers
            archive.addfile(info, io.BytesIO(payload))
    compressed = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=compressed, mtime=0) as stream:
        stream.write(output.getvalue())
    path.write_bytes(compressed.getvalue())


def test_wheel_rejects_bytes_before_first_zip_record(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    wheel = artifacts["python_wheel"]
    wheel.write_bytes(b"UNACCOUNTED-PREFIX" + wheel.read_bytes())

    with pytest.raises(ArtifactError, match="before|framing|ZIP"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_wheel_rejects_archive_comment_payload(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    with zipfile.ZipFile(artifacts["python_wheel"], "a") as archive:
        archive.comment = b"UNACCOUNTED-COMMENT"

    with pytest.raises(ArtifactError, match="comment|framing|canonical"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_upm_rejects_gzip_filename_metadata(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    editor = artifacts["unity_editor_upm"]
    tar_payload = gzip.decompress(editor.read_bytes())
    output = io.BytesIO()
    with gzip.GzipFile(filename="hidden-name", mode="wb", fileobj=output, mtime=0) as stream:
        stream.write(tar_payload)
    editor.write_bytes(output.getvalue())

    with pytest.raises(ArtifactError, match="gzip.*canonical"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_upm_rejects_pax_metadata_payload(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_editor_upm(
        artifacts["unity_editor_upm"],
        pax_headers={"NDA.secret": "hidden"},
    )

    with pytest.raises(ArtifactError, match="PAX|metadata.*canonical|extended"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_upm_rejects_overlong_portable_path_segment(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_editor_upm(
        artifacts["unity_editor_upm"],
        member_name=f"package/Editor/{'a' * 256}.cs",
    )

    with pytest.raises(ArtifactError, match="length|portable|extended|metadata"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_upm_rejects_noncanonical_file_mode(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path)
    _write_editor_upm(artifacts["unity_editor_upm"], member_mode=0o755)

    with pytest.raises(ArtifactError, match="mode|metadata.*canonical"):
        build_artifact_manifest("a" * 40, "1.27.0", artifacts)


def test_source_package_digest_rejects_git_executable_mode(tmp_path: Path) -> None:
    root = tmp_path / "source"
    tracked = root / "package" / "Editor" / "Marker.txt"
    tracked.parent.mkdir(parents=True)
    tracked.write_text("content\n", encoding="utf-8")
    _git(root, "init", "-q")
    _git(root, "config", "user.name", "Gauntlet Test")
    _git(root, "config", "user.email", "gauntlet@example.invalid")
    _git(root, "add", ".")
    _git(root, "update-index", "--chmod=-x", "package/Editor/Marker.txt")
    _git(root, "commit", "-q", "-m", "regular package member")
    regular_head = _git(root, "rev-parse", "HEAD")

    regular_digests = observe_package_content_digests(
        root,
        regular_head,
        {"unity_editor_upm": ("package", "")},
    )
    assert len(regular_digests["unity_editor_upm"]) == 64

    _git(root, "update-index", "--chmod=+x", "package/Editor/Marker.txt")
    _git(root, "commit", "-q", "-m", "make package member executable")
    executable_head = _git(root, "rev-parse", "HEAD")
    assert _git(
        root,
        "ls-tree",
        executable_head,
        "--",
        "package/Editor/Marker.txt",
    ).startswith("100755 blob ")

    with pytest.raises(SourcePackageContentError, match="non-regular tracked entry"):
        observe_package_content_digests(
            root,
            executable_head,
            {"unity_editor_upm": ("package", "")},
        )


def test_wheel_rejects_semver_not_canonical_in_pep440_filename(tmp_path: Path) -> None:
    artifacts = write_release_artifacts(tmp_path, product_version="1.2.3-rc.1")

    with pytest.raises(ArtifactError, match="PEP 440|filename"):
        build_artifact_manifest("a" * 40, "1.2.3-rc.1", artifacts)
