#!/usr/bin/env python3
"""Create a disposable Unity 6000.0 / built-in UTF 1.6 test worker snapshot."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


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


def _prepare_destination(destination: Path) -> None:
    resolved = destination.resolve()
    if resolved == REPO_ROOT or REPO_ROOT in resolved.parents:
        raise WorkerCreationError("Disposable worker must be outside the source checkout")
    if destination.exists():
        raise WorkerCreationError(
            f"Destination already exists; refusing to merge or delete it: {destination}"
        )
    destination.mkdir(parents=True)


def _rewrite_manifest(destination: Path) -> None:
    manifest_path = destination / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict):
        raise WorkerCreationError("Packages/manifest.json has no dependencies object")
    dependencies["com.unity.test-framework"] = UTF_VERSION
    dependencies["com.unity-biome-mcp.editor"] = "file:../LocalPackages/unity-plugin"
    dependencies["com.unity-biome-mcp.reload"] = (
        "file:../LocalPackages/unity-plugin-reload"
    )
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


def create_worker(source: Path, destination: Path) -> dict[str, object]:
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
        local_packages = destination / "LocalPackages"
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
        _rewrite_manifest(destination)

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
            "created_utc": datetime.now(timezone.utc).isoformat(),
            "source_project": str(source),
            "source_repository": str(REPO_ROOT),
            "unity_version": UNITY_VERSION,
            "unity_revision": UNITY_REVISION,
            "utf_version": UTF_VERSION,
            "bootstrap_scene": BOOTSTRAP_SCENE,
            "editor_package_sha256": _hash_tree(local_packages / "unity-plugin"),
            "reload_package_sha256": _hash_tree(
                local_packages / "unity-plugin-reload"
            ),
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
    parser.add_argument("--unity", type=Path, default=DEFAULT_UNITY)
    parser.add_argument("--launch", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        marker = create_worker(args.source_project, args.destination)
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
