import json
import sys
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
SCRIPTS = TESTS.parent
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))
import create_unity_test_worker as worker
from gauntlet.artifacts import build_artifact_manifest, write_artifact_manifest
from gauntlet_test_fixtures import write_release_artifacts


def source_project(tmp_path: Path) -> Path:
    source = tmp_path / "source"
    (source / "Assets" / "Scenes").mkdir(parents=True)
    (source / "Packages").mkdir()
    (source / "ProjectSettings").mkdir()
    (source / worker.BOOTSTRAP_SCENE).write_text("scene", encoding="utf-8")
    (source / "Assets" / "TestsTemp 42").mkdir()
    (source / "Assets" / "TestsTemp 42" / "stale.txt").write_text(
        "stale", encoding="utf-8"
    )
    (source / "Packages" / "manifest.json").write_text(
        json.dumps(
            {
                "dependencies": {
                    "com.unity.test-framework": "1.6.0",
                    "com.unity-biome-mcp.editor": "file:/stale/editor",
                    "com.unity-biome-mcp.reload": "file:/stale/reload",
                }
            }
        ),
        encoding="utf-8",
    )
    (source / "Packages" / "packages-lock.json").write_text("{}", encoding="utf-8")
    (source / "ProjectSettings" / "ProjectVersion.txt").write_text(
        f"m_EditorVersion: {worker.UNITY_VERSION}\n"
        f"m_EditorVersionWithRevision: {worker.UNITY_VERSION} ({worker.UNITY_REVISION})\n",
        encoding="utf-8",
    )
    return source


def test_worker_snapshot_pins_tools_and_named_bootstrap(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = source_project(tmp_path)
    repository = tmp_path / "repository"
    (repository / "unity-plugin").mkdir(parents=True)
    (repository / "unity-plugin-reload").mkdir()
    (repository / "unity-plugin" / "package.json").write_text("{}", encoding="utf-8")
    (repository / "unity-plugin-reload" / "package.json").write_text(
        "{}", encoding="utf-8"
    )
    destination = tmp_path / "worker"
    monkeypatch.setattr(worker, "REPO_ROOT", repository)

    marker = worker.create_worker(source, destination)

    manifest = json.loads(
        (destination / "Packages" / "manifest.json").read_text(encoding="utf-8")
    )
    assert manifest["dependencies"]["com.unity.test-framework"] == "1.6.0"
    assert manifest["dependencies"]["com.unity-biome-mcp.editor"] == (
        "file:../LocalPackages/unity-plugin"
    )
    assert not (destination / "Packages" / "packages-lock.json").exists()
    assert not (destination / "Assets" / "TestsTemp 42").exists()
    assert worker.BOOTSTRAP_SCENE in (
        destination / "Library" / "LastSceneManagerSetup.txt"
    ).read_text(encoding="utf-8")
    assert marker["disposable"] is True
    assert marker["utf_version"] == "1.6.0"


def test_worker_snapshot_can_consume_exact_upm_artifacts(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    source = source_project(tmp_path)
    repository = tmp_path / "repository"
    repository.mkdir()
    artifacts = write_release_artifacts(tmp_path / "artifacts", product_version="1.27.0")
    manifest = build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    manifest_path = tmp_path / "artifacts" / "artifact-manifest.json"
    write_artifact_manifest(manifest_path, manifest)
    destination = tmp_path / "worker"
    monkeypatch.setattr(worker, "REPO_ROOT", repository)

    marker = worker.create_worker(
        source,
        destination,
        artifact_manifest=manifest_path,
        artifact_root=manifest_path.parent,
    )

    project_manifest = json.loads(
        (destination / "Packages" / "manifest.json").read_text(encoding="utf-8")
    )
    dependencies = project_manifest["dependencies"]
    assert dependencies["com.unity-biome-mcp.editor"].endswith(
        "/com.unity-biome-mcp.editor-1.27.0.tgz"
    )
    assert dependencies["com.unity-biome-mcp.reload"].endswith(
        "/com.unity-biome-mcp.reload-0.1.4.tgz"
    )
    assert (destination / "LocalPackages" / "com.unity-biome-mcp.editor-1.27.0.tgz").is_file()
    assert (destination / "LocalPackages" / "com.unity-biome-mcp.reload-0.1.4.tgz").is_file()
    assert marker["artifact_manifest_sha256"] == manifest.manifest_sha
    assert marker["loaded_artifacts"] == {
        "unity_editor_upm": manifest.archive_digests["unity_editor_upm"],
        "unity_reload_upm": manifest.archive_digests["unity_reload_upm"],
    }


def test_worker_snapshot_rejects_tampered_upm_artifact(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    source = source_project(tmp_path)
    repository = tmp_path / "repository"
    repository.mkdir()
    artifacts = write_release_artifacts(tmp_path / "artifacts", product_version="1.27.0")
    manifest = build_artifact_manifest("a" * 40, "1.27.0", artifacts)
    manifest_path = tmp_path / "artifacts" / "artifact-manifest.json"
    write_artifact_manifest(manifest_path, manifest)
    artifacts["unity_editor_upm"].write_bytes(artifacts["unity_editor_upm"].read_bytes() + b"tamper")
    monkeypatch.setattr(worker, "REPO_ROOT", repository)

    with pytest.raises(worker.WorkerCreationError, match="size|digest|trailing|archive"):
        worker.create_worker(
            source,
            tmp_path / "worker",
            artifact_manifest=manifest_path,
            artifact_root=manifest_path.parent,
        )


def test_worker_creation_refuses_existing_destination(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    source = source_project(tmp_path)
    repository = tmp_path / "repository"
    (repository / "unity-plugin").mkdir(parents=True)
    (repository / "unity-plugin-reload").mkdir()
    destination = tmp_path / "worker"
    destination.mkdir()
    monkeypatch.setattr(worker, "REPO_ROOT", repository)

    with pytest.raises(worker.WorkerCreationError, match="already exists"):
        worker.create_worker(source, destination)


def test_source_version_must_match_canonical_toolchain(tmp_path: Path) -> None:
    source = source_project(tmp_path)
    (source / "ProjectSettings" / "ProjectVersion.txt").write_text(
        "m_EditorVersion: 6000.0.65f1\n", encoding="utf-8"
    )

    with pytest.raises(worker.WorkerCreationError, match="Unity revision"):
        worker._validate_source(source)
