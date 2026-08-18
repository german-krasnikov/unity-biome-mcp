"""Install verified Unity package artifacts into a disposable worker snapshot."""


import shutil
from dataclasses import dataclass
from typing import TYPE_CHECKING

from gauntlet.artifacts import ArtifactError, load_artifact_manifest, verify_artifact_files

if TYPE_CHECKING:
    from pathlib import Path

    from gauntlet.artifact_contracts import ArtifactManifest


class WorkerArtifactError(RuntimeError):
    """Raised when worker package artifacts cannot be installed."""


@dataclass(frozen=True, slots=True)
class WorkerArtifactInstall:
    dependencies: dict[str, str]
    marker_fields: dict[str, object]


def install_worker_artifacts(
    manifest_path: Path,
    artifact_root: Path,
    local_packages: Path,
) -> WorkerArtifactInstall:
    """Copy verified editor/reload UPM archives and return manifest dependencies."""
    try:
        manifest = load_artifact_manifest(manifest_path)
        verify_artifact_files(manifest, artifact_root)
    except ArtifactError as exc:
        raise WorkerArtifactError(str(exc)) from exc
    records = {record.artifact_type: record for record in manifest.artifacts}
    if "unity_editor_upm" not in records or "unity_reload_upm" not in records:
        raise WorkerArtifactError("artifact manifest must include both Unity packages")
    local_packages.mkdir(parents=True, exist_ok=True)
    copied: dict[str, str] = {}
    dependencies: dict[str, str] = {}
    loaded: dict[str, str] = {}
    for artifact_type, package_name in (
        ("unity_editor_upm", "com.unity-biome-mcp.editor"),
        ("unity_reload_upm", "com.unity-biome-mcp.reload"),
    ):
        record = records[artifact_type]
        destination = local_packages / record.filename
        if destination.exists():
            raise WorkerArtifactError(f"worker artifact destination already exists: {record.filename}")
        shutil.copy2(artifact_root / record.filename, destination)
        copied[artifact_type] = record.filename
        loaded[artifact_type] = record.archive_sha256
        dependencies[package_name] = f"file:../LocalPackages/{record.filename}"
    return WorkerArtifactInstall(
        dependencies=dependencies,
        marker_fields=_marker_fields(manifest, copied, loaded),
    )


def _marker_fields(
    manifest: ArtifactManifest,
    copied: dict[str, str],
    loaded: dict[str, str],
) -> dict[str, object]:
    return {
        "artifact_manifest_sha256": manifest.manifest_sha,
        "product_version": manifest.product_version,
        "loaded_artifacts": loaded,
        "local_package_files": copied,
    }
