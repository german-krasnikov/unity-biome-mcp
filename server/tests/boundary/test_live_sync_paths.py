import json

import pytest

from tests.live.test_sync_live import _installed_editor_package


def _write_manifest(project, editor_reference):
    packages = project / "Packages"
    packages.mkdir(parents=True)
    (packages / "manifest.json").write_text(
        json.dumps(
            {
                "dependencies": {
                    "com.unity-biome-mcp.editor": editor_reference,
                }
            }
        ),
        encoding="utf-8",
    )


def test_installed_editor_package_resolves_canonical_absolute_dependency(tmp_path):
    project = tmp_path / "canonical-project"
    source_package = tmp_path / "repository" / "unity-plugin"
    source_package.mkdir(parents=True)
    package_json = source_package / "package.json"
    package_json.write_text("{}", encoding="utf-8")
    _write_manifest(project, f"file:{source_package}")

    assert _installed_editor_package(project) == package_json.resolve()


def test_installed_editor_package_resolves_disposable_worker_dependency(tmp_path):
    project = tmp_path / "worker"
    worker_package = project / "LocalPackages" / "unity-plugin"
    worker_package.mkdir(parents=True)
    package_json = worker_package / "package.json"
    package_json.write_text("{}", encoding="utf-8")
    _write_manifest(project, "file:../LocalPackages/unity-plugin")

    assert _installed_editor_package(project) == package_json.resolve()


def test_installed_editor_package_rejects_non_local_dependency(tmp_path):
    project = tmp_path / "project"
    _write_manifest(project, "1.10.3")

    with pytest.raises(AssertionError, match="must be a local file dependency"):
        _installed_editor_package(project)
