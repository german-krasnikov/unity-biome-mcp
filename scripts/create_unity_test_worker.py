#!/usr/bin/env python3
"""Create a disposable Unity 6000.0 / built-in UTF 1.6 test worker snapshot."""


import argparse
import hashlib
import json
import shutil
import subprocess
from collections.abc import Mapping  # noqa: TC003
from datetime import UTC, datetime
from pathlib import Path

from gauntlet.worker_artifacts import WorkerArtifactError, install_worker_artifacts

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_PROJECT = REPO_ROOT / "unity-test-project"
DEFAULT_UNITY = Path(
    "/Applications/Unity/Hub/Editor/6000.0.65f1-arm64/Unity.app/Contents/MacOS/Unity"
)
UNITY_VERSION = "6000.0.65f1"
UNITY_REVISION = "a18e2220bd50"
UTF_VERSION = "1.6.0"
BOOTSTRAP_SCENE = "Assets/Scenes/GridTest.unity"


class WorkerCreationError(RuntimeError):
    pass


def _ignore_project_assets(directory: str, names: list[str]) -> set[str]:
    if Path(directory).name != "Assets":
        return {name for name in names if name in {".DS_Store"}}
    return {
        name
        for name in names
        if name.startswith("TestsTemp")
        or name.startswith("InitTestScene")
        or name.startswith("UnityMCPRecovery")
        or name == "PlaytestDefs 1"
        or name == ".DS_Store"
    }


def _ignore_package(directory: str, names: list[str]) -> set[str]:
    return {
        name
        for name in names
        if name in {".DS_Store", ".git", "Library", "Temp", "obj", "Logs"}
    }


def _hash_tree(root: Path) -> str:
    digest = hashlib.sha256()
    for path in sorted(value for value in root.rglob("*") if value.is_file()):
        relative = path.relative_to(root).as_posix().encode("utf-8")
        digest.update(len(relative).to_bytes(4, "big"))
        digest.update(relative)
        with path.open("rb") as stream:
            while chunk := stream.read(1024 * 1024):
                digest.update(chunk)
    return digest.hexdigest()


def _load_source_patch_pin(pin_path: Path) -> dict[str, str]:
    """Read a tracked Source Patch provider pin (e.g. scripts/
    source_patch_provider_pin.json) and return the one manifest dependency
    entry it describes: {package_name: "git_url#ref"}.

    Disposable-worker/matrix-lock input only (§6 P0-70) — never merged into
    the tracked unity-test-project/Packages/manifest.json by default; a
    caller must pass this explicitly to create_worker()."""
    if not pin_path.is_file():
        raise WorkerCreationError(f"Source Patch provider pin not found: {pin_path}")
    payload = json.loads(pin_path.read_text(encoding="utf-8"))
    missing = [key for key in ("package_name", "git_url", "ref") if not payload.get(key)]
    if missing:
        raise WorkerCreationError(
            f"Source Patch provider pin {pin_path} missing field(s): {', '.join(missing)}"
        )
    return {payload["package_name"]: f"{payload['git_url']}#{payload['ref']}"}


def _validate_source(source: Path) -> None:
    required = (source / "Assets", source / "Packages", source / "ProjectSettings")
    if not all(path.is_dir() for path in required):
        raise WorkerCreationError(f"Not a Unity source project: {source}")
    version_text = (source / "ProjectSettings" / "ProjectVersion.txt").read_text(
        encoding="utf-8"
    )
    if f"m_EditorVersion: {UNITY_VERSION}" not in version_text:
        raise WorkerCreationError(
            f"Canonical test project must use Unity {UNITY_VERSION}"
        )
    if UNITY_REVISION not in version_text:
        raise WorkerCreationError(
            f"Canonical test project must use Unity revision {UNITY_REVISION}"
        )
    if not (source / BOOTSTRAP_SCENE).is_file():
        raise WorkerCreationError(f"Named bootstrap scene is missing: {BOOTSTRAP_SCENE}")


def rewrite_manifest_pin(destination: Path, pin: Path, *, install: bool) -> None:
    """Add (install=True) or remove (install=False) the Source Patch
    provider dependency on an ALREADY-CREATED disposable worker's
    manifest.json, mid-lifecycle — for the P1-20 matrix's offline
    install/uninstall phases (§6 P0-80 steps 3 and 7). Distinct from
    create_worker(..., source_patch_provider_pin=...), which only pins at
    creation time. Never touches a non-disposable project; this function
    only ever writes destination/Packages/manifest.json."""
    dependency = _load_source_patch_pin(pin)
    package_name = next(iter(dependency))
    manifest_path = destination / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict):
        raise WorkerCreationError("Packages/manifest.json has no dependencies object")
    if install:
        dependencies.update(dependency)
    else:
        dependencies.pop(package_name, None)
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )
    lock_path = destination / "Packages" / "packages-lock.json"
    if lock_path.exists():
        lock_path.unlink()


def rewrite_project_version(
    destination: Path, *, unity_version: str, unity_revision: str
) -> None:
    """Rewrite an ALREADY-CREATED disposable worker's
    ProjectSettings/ProjectVersion.txt to declare a different target Unity
    version/revision, so a headed (non-batchmode) launch of a different
    Editor never hits Unity's interactive "project created with an earlier
    version, continue anyway?" dialog — which blocks indefinitely outside
    batchmode (§7 P1-20). Disposable-worker-only; never called on
    create_worker's default path."""
    path = destination / "ProjectSettings" / "ProjectVersion.txt"
    if not path.is_file():
        raise WorkerCreationError(f"ProjectVersion.txt is missing: {path}")
    path.write_text(
        f"m_EditorVersion: {unity_version}\n"
        f"m_EditorVersionWithRevision: {unity_version} ({unity_revision})\n",
        encoding="utf-8",
    )


def _prepare_destination(destination: Path) -> None:
    resolved = destination.resolve()
    if resolved == REPO_ROOT or REPO_ROOT in resolved.parents:
        raise WorkerCreationError("Disposable worker must be outside the source checkout")
    if destination.exists():
        raise WorkerCreationError(
            f"Destination already exists; refusing to merge or delete it: {destination}"
        )
    destination.mkdir(parents=True)


def _rewrite_manifest(destination: Path, package_dependencies: Mapping[str, str]) -> None:
    manifest_path = destination / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict):
        raise WorkerCreationError("Packages/manifest.json has no dependencies object")
    dependencies["com.unity.test-framework"] = UTF_VERSION
    dependencies.update(package_dependencies)
    manifest["testables"] = [
        "com.unity-biome-mcp.editor",
        "com.unity-biome-mcp.reload",
    ]
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=True) + "\n", encoding="utf-8"
    )
    lock_path = destination / "Packages" / "packages-lock.json"
    if lock_path.exists():
        lock_path.unlink()


def _install_repo_packages(local_packages: Path) -> tuple[dict[str, str], dict[str, object]]:
    shutil.copytree(
        REPO_ROOT / "unity-plugin",
        local_packages / "unity-plugin",
        ignore=_ignore_package,
    )
    shutil.copytree(
        REPO_ROOT / "unity-plugin-reload",
        local_packages / "unity-plugin-reload",
        ignore=_ignore_package,
    )
    return (
        {
            "com.unity-biome-mcp.editor": "file:../LocalPackages/unity-plugin",
            "com.unity-biome-mcp.reload": "file:../LocalPackages/unity-plugin-reload",
        },
        {
            "editor_package_sha256": _hash_tree(local_packages / "unity-plugin"),
            "reload_package_sha256": _hash_tree(local_packages / "unity-plugin-reload"),
        },
    )


def _install_worker_packages(
    local_packages: Path,
    artifact_manifest: Path | None,
    artifact_root: Path | None,
) -> tuple[dict[str, str], dict[str, object]]:
    if artifact_manifest is None:
        return _install_repo_packages(local_packages)
    root = artifact_root or artifact_manifest.parent
    try:
        install = install_worker_artifacts(artifact_manifest, root, local_packages)
    except WorkerArtifactError as exc:
        raise WorkerCreationError(str(exc)) from exc
    return install.dependencies, install.marker_fields


def create_worker(
    source: Path,
    destination: Path,
    *,
    artifact_manifest: Path | None = None,
    artifact_root: Path | None = None,
    source_patch_provider_pin: Path | None = None,
    target_unity_version: str | None = None,
    target_unity_revision: str | None = None,
) -> dict[str, object]:
    if bool(target_unity_version) != bool(target_unity_revision):
        raise WorkerCreationError(
            "target_unity_version and target_unity_revision must be given together"
        )
    source = source.resolve()
    destination = destination.resolve()
    _validate_source(source)
    _prepare_destination(destination)

    try:
        shutil.copytree(
            source / "Assets",
            destination / "Assets",
            ignore=_ignore_project_assets,
        )
        shutil.copytree(source / "Packages", destination / "Packages")
        shutil.copytree(source / "ProjectSettings", destination / "ProjectSettings")
        if target_unity_version and target_unity_revision:
            rewrite_project_version(
                destination,
                unity_version=target_unity_version,
                unity_revision=target_unity_revision,
            )
        local_packages = destination / "LocalPackages"
        package_dependencies, package_marker = _install_worker_packages(
            local_packages,
            artifact_manifest,
            artifact_root,
        )
        if source_patch_provider_pin is not None:
            pin_dependency = _load_source_patch_pin(source_patch_provider_pin)
            package_dependencies = {**package_dependencies, **pin_dependency}
            pin_payload = json.loads(source_patch_provider_pin.read_text(encoding="utf-8"))
            package_marker = {
                **package_marker,
                "source_patch_provider_package": pin_payload["package_name"],
                "source_patch_provider_ref": pin_payload["ref"],
                "source_patch_provider_pin_sha256": hashlib.sha256(
                    source_patch_provider_pin.read_bytes()
                ).hexdigest(),
            }
        _rewrite_manifest(destination, package_dependencies)

        library = destination / "Library"
        evidence = library / "UnityMCP"
        evidence.mkdir(parents=True)
        (library / "LastSceneManagerSetup.txt").write_text(
            "sceneSetups:\n"
            f"- path: {BOOTSTRAP_SCENE}\n"
            "  isLoaded: 1\n"
            "  isActive: 1\n"
            "  isSubScene: 0\n",
            encoding="utf-8",
        )
        marker: dict[str, object] = {
            "schema_version": 1,
            "disposable": True,
            "created_utc": datetime.now(UTC).isoformat(),
            "source_project_sha256": _hash_tree(source),
            "source_repository_sha256": hashlib.sha256(
                (
                    _hash_tree(REPO_ROOT / "unity-plugin")
                    + _hash_tree(REPO_ROOT / "unity-plugin-reload")
                ).encode("ascii")
            ).hexdigest(),
            "unity_version": target_unity_version or UNITY_VERSION,
            "unity_revision": target_unity_revision or UNITY_REVISION,
            "utf_version": UTF_VERSION,
            "bootstrap_scene": BOOTSTRAP_SCENE,
            **package_marker,
        }
        (evidence / "disposable-worker.json").write_text(
            json.dumps(marker, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        return marker
    except Exception:
        # The destination did not exist before this call, so a partial snapshot
        # is owned by this operation and is safe to remove.
        shutil.rmtree(destination, ignore_errors=True)
        raise


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--destination", type=Path, required=True)
    parser.add_argument("--source-project", type=Path, default=DEFAULT_SOURCE_PROJECT)
    parser.add_argument("--artifact-manifest", type=Path)
    parser.add_argument("--artifact-root", type=Path)
    parser.add_argument("--source-patch-provider-pin", type=Path)
    parser.add_argument("--target-unity-version", type=str)
    parser.add_argument("--target-unity-revision", type=str)
    parser.add_argument("--unity", type=Path, default=DEFAULT_UNITY)
    parser.add_argument("--launch", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        marker = create_worker(
            args.source_project,
            args.destination,
            artifact_manifest=args.artifact_manifest,
            artifact_root=args.artifact_root,
            source_patch_provider_pin=args.source_patch_provider_pin,
            target_unity_version=args.target_unity_version,
            target_unity_revision=args.target_unity_revision,
        )
        print(json.dumps(marker, indent=2, sort_keys=True))
        print(f"worker={args.destination.resolve()}")
        if args.launch:
            if not args.unity.is_file():
                raise WorkerCreationError(f"Unity executable is missing: {args.unity}")
            process = subprocess.Popen(
                [str(args.unity), "-projectPath", str(args.destination.resolve())],
                start_new_session=True,
            )
            print(f"unity_pid={process.pid}")
        return 0
    except (OSError, ValueError, json.JSONDecodeError, WorkerCreationError) as error:
        print(f"WORKER CREATION FAILED: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
