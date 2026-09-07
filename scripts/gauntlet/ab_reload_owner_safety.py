"""N3 T3/T4: owner-safety guards for the A/B live dual-worker reload
identity harness. Disposable-worker project validation, port constants, and
the harness's own error type -- no Unity lifecycle, no process management
(that lives in scripts/run_ab_reload_identity.py). See
Plans/N3-T3-T4-live-reload-identity.md.
"""
from __future__ import annotations

import json
import os
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

UNITY_VERSION = "6000.0.65f1"
UNITY_REVISION = "a18e2220bd50"
UTF_VERSION = "1.6.0"

OWNER_PORT = 9600
DEFAULT_PORT_A = 9620
DEFAULT_PORT_B = 9630


class ABReloadIdentityError(RuntimeError):
    pass


def _read_json(path: Path) -> dict[str, object]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise ABReloadIdentityError(f"Cannot read JSON evidence {path}: {error}") from error
    if not isinstance(value, dict):
        raise ABReloadIdentityError(f"Expected a JSON object in {path}")
    return value


def _project_version_text(project: Path) -> str:
    path = project / "ProjectSettings/ProjectVersion.txt"
    try:
        return path.read_text(encoding="utf-8")
    except OSError as error:
        raise ABReloadIdentityError(f"Cannot read {path}: {error}") from error


def validate_worker_project(project: Path) -> None:
    """Reject the owner's canonical worker, anything inside the source
    checkout, and any directory that is not a disposable worker pinned to
    the same Unity/UTF versions as the rest of the release lane."""
    project = project.resolve()
    canonical = (REPO_ROOT / "unity-test-project").resolve()
    if project == canonical:
        raise ABReloadIdentityError(
            "Refusing to touch the canonical unity-test-project worker"
        )
    if project == REPO_ROOT or REPO_ROOT in project.parents:
        raise ABReloadIdentityError(
            "AB reload identity harness is forbidden inside the source checkout"
        )
    if not all((project / name).is_dir() for name in ("Assets", "Packages", "ProjectSettings")):
        raise ABReloadIdentityError(f"Not a Unity project: {project}")

    marker = _read_json(project / "Library/UnityMCP/disposable-worker.json")
    required = {
        "schema_version": 1,
        "disposable": True,
        "unity_version": UNITY_VERSION,
        "unity_revision": UNITY_REVISION,
        "utf_version": UTF_VERSION,
    }
    mismatches = [
        f"{name}={marker.get(name)!r}" for name, expected in required.items() if marker.get(name) != expected
    ]
    if mismatches:
        raise ABReloadIdentityError("Disposable worker marker mismatch: " + ", ".join(mismatches))

    version_text = _project_version_text(project)
    if f"m_EditorVersion: {UNITY_VERSION}" not in version_text or UNITY_REVISION not in version_text:
        raise ABReloadIdentityError(f"Worker must use Unity {UNITY_VERSION} revision {UNITY_REVISION}")

    manifest = _read_json(project / "Packages/manifest.json")
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict) or dependencies.get("com.unity.test-framework") != UTF_VERSION:
        raise ABReloadIdentityError(f"Worker manifest must pin built-in UTF {UTF_VERSION}")


def validate_ports(port_a: int, port_b: int) -> None:
    if port_a == OWNER_PORT or port_b == OWNER_PORT:
        raise ABReloadIdentityError(f"Refusing to allocate the owner's port {OWNER_PORT}")
    if port_a == port_b:
        raise ABReloadIdentityError(f"Worker A and Worker B ports must differ (got {port_a})")


def _pid_alive(pid: int) -> bool:
    """os.kill(pid, 0) liveness probe -- same idiom as
    gauntlet.process_posix.group_exists (ProcessLookupError=dead,
    PermissionError=alive-but-different-user)."""
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def validate_port_owned_by(port: int, launched_pids: set[int], ports_dir: Path) -> None:
    """Refuse to touch a port whose port-file PID this harness did not
    launch itself. A port file whose PID is no longer alive is stale
    evidence, not a foreign worker -- ignore it. Mitigates 'owner's Unity
    affected' / foreign-worker cross-talk without ever looking a process up
    by port number."""
    owner_pids: list[int] = []
    for port_file in ports_dir.glob("*.port"):
        if not port_file.stem.isdigit():
            continue
        pid = int(port_file.stem)
        if not _pid_alive(pid):
            continue
        try:
            advertised_port = int(port_file.read_text(encoding="utf-8").splitlines()[0])
        except (OSError, ValueError, IndexError):
            continue
        if advertised_port == port:
            owner_pids.append(pid)
    if not owner_pids:
        raise ABReloadIdentityError(f"No port file advertises port {port}; refusing to touch an unknown worker")
    foreign = [pid for pid in owner_pids if pid not in launched_pids]
    if foreign:
        raise ABReloadIdentityError(
            f"Port {port} is advertised by PID(s) {foreign} this harness did not launch; "
            "refusing to touch a foreign worker"
        )
