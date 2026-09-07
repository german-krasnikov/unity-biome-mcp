#!/usr/bin/env python3
"""N3 T3/T4: live dual-worker reload identity + compile-error recovery.

Two harness-owned disposable Unity workers (A, B) prove reload identity,
new-code execution, lost-ACK safety, and compile-error recovery across
independent projects. The owner's Unity (port 9600) is never touched.

This module is the Task 1 scaffold: CLI parsing, the nonce fixture
installer/mutator, the evidence receipt builder, and the owner-safety
guards (canonical project path, port ports must differ, foreign PID on a
port file). T3/T4 phase execution (read_nonce/read_mvid/trigger_recompile/
wait_for_reload/CounterProxy/UnityBridge identity rejection) lands in later
tasks — see Plans/N3-T3-T4-live-reload-identity.md.

    python3 scripts/run_ab_reload_identity.py \\
        --worker-a-dir /tmp/unity-ab-reload-A --worker-b-dir /tmp/unity-ab-reload-B \\
        --port-a 9620 --port-b 9630 --unity /path/to/Unity \\
        --receipt /tmp/ab-reload-receipt.json --confirm-disposable-worker
"""

import argparse
import json
import os
import re
import shutil
import uuid
from datetime import UTC, datetime
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

UNITY_VERSION = "6000.0.65f1"
UNITY_REVISION = "a18e2220bd50"
UTF_VERSION = "1.6.0"

FIXTURE_SOURCE = Path(__file__).resolve().parent / "fixtures" / "ab_reload_harness"
FIXTURE_RELATIVE = Path("Assets/UnityMCPABReloadHarness")
FIXTURE_FILES = ("AbReloadNonce.cs", "UnityMCP.Worker.ABReloadHarness.asmdef")
NONCE_FILE_NAME = "AbReloadNonce.cs"
NONCE_PATTERN = re.compile(r"^[A-Za-z0-9_-]+$")

OWNER_PORT = 9600

REQUIRED_RECEIPT_FIELDS = (
    "source_sha",
    "unity_version",
    "utf_version",
    "worker_a",
    "worker_b",
    "t3_identity_reload",
    "t3_cross_identity",
    "t3_lost_ack",
    "t3_negative_controls",
    "t4_compile_error",
    "t4_sentinel",
    "cleanup",
)
_MISSING = object()


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


def _atomic_write_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, path)


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


def validate_port_owned_by(port: int, launched_pids: set[int], ports_dir: Path) -> None:
    """Refuse to touch a port whose port-file PID this harness did not
    launch itself. Mitigates 'owner's Unity affected' / foreign-worker
    cross-talk without ever looking a process up by port number."""
    owner_pids: list[int] = []
    for port_file in ports_dir.glob("*.port"):
        if not port_file.stem.isdigit():
            continue
        try:
            advertised_port = int(port_file.read_text(encoding="utf-8").splitlines()[0])
        except (OSError, ValueError, IndexError):
            continue
        if advertised_port == port:
            owner_pids.append(int(port_file.stem))
    if not owner_pids:
        raise ABReloadIdentityError(f"No port file advertises port {port}; refusing to touch an unknown worker")
    foreign = [pid for pid in owner_pids if pid not in launched_pids]
    if foreign:
        raise ABReloadIdentityError(
            f"Port {port} is advertised by PID(s) {foreign} this harness did not launch; "
            "refusing to touch a foreign worker"
        )


def _validate_nonce(nonce: str) -> None:
    if not NONCE_PATTERN.fullmatch(nonce):
        raise ABReloadIdentityError(
            f"Unsafe nonce {nonce!r}: only letters, digits, '-', '_' are allowed"
        )


def _render_nonce_source(nonce: str) -> str:
    _validate_nonce(nonce)
    template = (FIXTURE_SOURCE / NONCE_FILE_NAME).read_text(encoding="utf-8")
    return template.replace("NONCE_PLACEHOLDER", nonce)


def install_nonce_fixture(project: Path, nonce: str) -> Path:
    """Write AbReloadNonce.cs + its asmdef under the worker's Assets/."""
    validate_worker_project(project)
    target_dir = project.resolve() / FIXTURE_RELATIVE
    rendered = _render_nonce_source(nonce)  # validate before touching disk
    target_dir.mkdir(parents=True, exist_ok=True)
    (target_dir / NONCE_FILE_NAME).write_text(rendered, encoding="utf-8")
    asmdef_source = FIXTURE_SOURCE / "UnityMCP.Worker.ABReloadHarness.asmdef"
    shutil.copy2(asmdef_source, target_dir / asmdef_source.name)
    return target_dir


def modify_nonce(project: Path, new_nonce: str) -> Path:
    """Rewrite the installed AbReloadNonce.cs with a new nonce (the
    'changed C# method' new-code proof the harness recompiles)."""
    validate_worker_project(project)
    target = project.resolve() / FIXTURE_RELATIVE / NONCE_FILE_NAME
    if not target.is_file():
        raise ABReloadIdentityError(f"Nonce fixture not installed: {target}")
    target.write_text(_render_nonce_source(new_nonce), encoding="utf-8")
    return target


def inject_compile_error(project: Path) -> Path:
    """Append an invalid top-level statement so the next recompile fails."""
    validate_worker_project(project)
    target = project.resolve() / FIXTURE_RELATIVE / NONCE_FILE_NAME
    if not target.is_file():
        raise ABReloadIdentityError(f"Nonce fixture not installed: {target}")
    with target.open("a", encoding="utf-8") as stream:
        stream.write("\nSYNTAX ERROR;\n")
    return target


def repair_compile_error(project: Path, new_nonce: str) -> Path:
    """Restore a clean, compilable AbReloadNonce.cs carrying a new nonce."""
    return modify_nonce(project, new_nonce)


def build_receipt(**fields: object) -> dict[str, object]:
    missing = [name for name in REQUIRED_RECEIPT_FIELDS if fields.get(name, _MISSING) is _MISSING]
    if missing:
        raise ABReloadIdentityError(f"Receipt missing required field(s): {', '.join(missing)}")
    receipt: dict[str, object] = {"schema_version": 1}
    receipt.update({name: fields[name] for name in REQUIRED_RECEIPT_FIELDS})
    receipt["timestamp"] = datetime.now(UTC).isoformat()
    return receipt


def write_receipt(path: Path, receipt: dict[str, object]) -> None:
    _atomic_write_json(path, receipt)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="A/B live dual-worker reload identity + compile-error recovery harness"
    )
    parser.add_argument("--worker-a-dir", type=Path, required=True)
    parser.add_argument("--worker-b-dir", type=Path, required=True)
    parser.add_argument("--port-a", type=int, default=9620)
    parser.add_argument("--port-b", type=int, default=9630)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--mode", choices=("t3", "t4", "both"), default="both")
    parser.add_argument("--timeout-seconds", type=float, default=900.0)
    parser.add_argument("--confirm-disposable-worker", action="store_true")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_arg_parser().parse_args(argv)
    if not args.confirm_disposable_worker:
        raise ABReloadIdentityError("Refusing to run without --confirm-disposable-worker")
    validate_ports(args.port_a, args.port_b)
    validate_worker_project(args.worker_a_dir)
    validate_worker_project(args.worker_b_dir)
    raise NotImplementedError(
        "T3/T4 phase execution lands in a later task; this is the Task 1 scaffold "
        "(scripts/run_ab_reload_identity.py: fixture + receipt + owner-safety guards only)"
    )


if __name__ == "__main__":
    raise SystemExit(main())
