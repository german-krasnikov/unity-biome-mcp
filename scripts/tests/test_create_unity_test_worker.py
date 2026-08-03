import json
from pathlib import Path
import sys

import pytest


sys.path.insert(0, str(Path(__file__).parent.parent))
import create_unity_test_worker as worker


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
