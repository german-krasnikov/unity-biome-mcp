#!/usr/bin/env python3
"""Run an exact policy profile with collection attestation.

This runner proves pytest selection and JUnit identity only. Until the trusted
runtime/worker producers wrap it, its output is compatibility evidence and is
not sufficient for a release decision.
"""


import argparse
import os
import stat
import subprocess
import sys
import tempfile
from contextlib import suppress
from pathlib import Path, PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.attested_conformance import (
    AttestedConformanceError,
    assess_attested_junit_bytes,
    load_observed_profile,
    profile_bindings,
)
from gauntlet.pytest_attestation import write_attestation_manifest
from gauntlet.source_snapshot import SourceSnapshotError, materialize_source_snapshot

if TYPE_CHECKING:
    from collections.abc import Sequence

    from gauntlet.release_policy import ProfilePolicy


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Policy-bound MCP conformance runner")
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--policy", type=Path, required=True)
    parser.add_argument("--expected-head", required=True)
    parser.add_argument("--profile", required=True)
    parser.add_argument("--junit", type=Path, required=True)
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--project", type=Path)
    parser.add_argument("--second-port", type=int, default=0)
    parser.add_argument("--second-project", type=Path)
    parser.add_argument("--timeout", type=int, default=300)
    parser.add_argument("--record", type=Path)
    parser.add_argument("--verbose", "-v", action="store_true")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        profile = load_observed_profile(
            source_root=args.source_root,
            policy_path=args.policy,
            expected_head_sha=args.expected_head,
            profile_id=args.profile,
        )
        workers = _validate_workers(args, profile)
        if args.timeout < 1:
            raise AttestedConformanceError("timeout must be positive")
        return _run(args, profile, workers)
    except AttestedConformanceError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


def _validate_workers(
    args: argparse.Namespace,
    profile: ProfilePolicy,
) -> tuple[tuple[int, Path], ...]:
    pairs = (
        (args.port, args.project, "first"),
        (args.second_port, args.second_project, "second"),
    )
    workers: list[tuple[int, Path]] = []
    for port, project, label in pairs:
        if bool(port) != (project is not None):
            raise AttestedConformanceError(f"{label} worker port and project must be supplied together")
        if not port:
            continue
        if not 1 <= port <= 65535:
            raise AttestedConformanceError(f"{label} worker port is invalid")
        resolved = project.resolve(strict=False)
        if not (resolved / "Assets").is_dir():
            raise AttestedConformanceError(f"{label} worker project has no Assets directory")
        workers.append((port, resolved))
    if len(workers) != profile.required_workers:
        raise AttestedConformanceError(
            f"profile requires {profile.required_workers} workers, got {len(workers)}"
        )
    if len({port for port, _ in workers}) != len(workers):
        raise AttestedConformanceError("worker ports must be distinct")
    if len({project for _, project in workers}) != len(workers):
        raise AttestedConformanceError("worker projects must be distinct")
    return tuple(workers)


def _run(
    args: argparse.Namespace,
    profile: ProfilePolicy,
    workers: tuple[tuple[int, Path], ...],
) -> int:
    bindings = profile_bindings(profile)
    with tempfile.TemporaryDirectory(prefix="unity-mcp-attested-pytest-") as directory:
        owned_root = Path(directory)
        execution_root = owned_root / "source"
        try:
            materialize_source_snapshot(
                args.source_root,
                expected_head_sha=args.expected_head,
                destination=execution_root,
            )
        except SourceSnapshotError as exc:
            raise AttestedConformanceError(str(exc)) from exc
        manifest = owned_root / "scenario-manifest.json"
        manifest_sha = write_attestation_manifest(manifest, profile.profile_id, bindings)
        internal_junit = owned_root / "junit.xml"
        command = _pytest_command(
            args,
            profile,
            execution_root,
            manifest,
            manifest_sha,
            internal_junit,
            owned_root / "pytest-temp",
            owned_root / "pytest.args",
        )
        environment = _pytest_environment(args, workers)
        try:
            result = subprocess.run(
                command,
                cwd=owned_root,
                env=environment,
                capture_output=True,
                text=True,
                timeout=args.timeout + 60,
            )
        except subprocess.TimeoutExpired as exc:
            raise AttestedConformanceError("attested pytest process timed out") from exc
        if result.stdout:
            print(result.stdout, end="")
        if result.stderr:
            print(result.stderr, end="", file=sys.stderr)
        junit_payload = _read_owned_junit(internal_junit)
        assessed = assess_attested_junit_bytes(
            junit_payload,
            process_exit_code=result.returncode,
            expected_bindings=bindings,
        )
        _publish_bytes(args.junit, junit_payload)
    print(
        f"ATTESTED SELECTION PASS: {assessed.passed}/{assessed.total}; "
        "compatibility-only until trusted runtime and worker receipts are attached"
    )
    return 0


def _pytest_command(
    args: argparse.Namespace,
    profile: ProfilePolicy,
    execution_root: Path,
    manifest: Path,
    manifest_sha: str,
    junit: Path,
    base_temp: Path,
    arguments_file: Path,
) -> list[str]:
    pytest_arguments = [
        "-p",
        "no:cacheprovider",
        *(_absolute_pytest_node(execution_root, node) for node in profile.pytest_node_ids),
        f"--rootdir={execution_root}",
        f"--basetemp={base_temp}",
        f"--junitxml={junit}",
        f"--timeout={args.timeout}",
        f"--gauntlet-manifest={manifest}",
        f"--gauntlet-manifest-sha={manifest_sha}",
        f"--gauntlet-source-root={execution_root}",
        "--color=no",
        "-v" if args.verbose else "-q",
    ]
    config = execution_root / "server" / "pyproject.toml"
    if config.is_file():
        pytest_arguments.extend(
            ("-c", str(config), "-o", "pythonpath=", "-o", "addopts=--strict-markers")
        )
    _write_pytest_arguments(arguments_file, pytest_arguments)
    scripts_root = Path(__file__).resolve().parent
    return [
        sys.executable,
        "-I",
        "-X",
        "utf8",
        str(scripts_root / "gauntlet" / "pytest_bootstrap.py"),
        str(scripts_root),
        str(execution_root / "server" / "src"),
        str(execution_root / "server" / "tests"),
        f"@{arguments_file}",
    ]


def _absolute_pytest_node(root: Path, node_id: str) -> str:
    relative, separator, selector = node_id.partition("::")
    if not separator:
        raise AttestedConformanceError("policy contains an invalid pytest node")
    path = root.joinpath(*PurePosixPath(relative).parts)
    return f"{path}::{selector}"


def _write_pytest_arguments(path: Path, arguments: Sequence[str]) -> None:
    if any("\n" in argument or "\r" in argument for argument in arguments):
        raise AttestedConformanceError("pytest argument contains a line break")
    try:
        with path.open("x", encoding="utf-8", newline="\n") as stream:
            for argument in arguments:
                stream.write(f"{argument}\n")
    except OSError as exc:
        raise AttestedConformanceError("pytest argument file cannot be written") from exc


def _pytest_environment(
    args: argparse.Namespace,
    workers: tuple[tuple[int, Path], ...],
) -> dict[str, str]:
    environment = os.environ.copy()
    for key in (
        "PYTEST_ADDOPTS",
        "PYTEST_PLUGINS",
        "PYTHONOPTIMIZE",
        "PYTHONSTARTUP",
        "PYTHONINSPECT",
        "PYTHONWARNINGS",
        "PYTHONPATH",
        "UNITY_MCP_PORT",
        "UNITY_MCP_PROJECT_PATH",
        "UNITY_MCP_SECOND_PORT",
        "UNITY_MCP_SECOND_PROJECT_PATH",
    ):
        environment.pop(key, None)
    environment.update(
        {
            "PYTEST_DISABLE_PLUGIN_AUTOLOAD": "1",
            "PYTHONDONTWRITEBYTECODE": "1",
            "PYTHONWARNDEFAULTENCODING": "1",
        }
    )
    if workers:
        environment["UNITY_MCP_PORT"] = str(workers[0][0])
        environment["UNITY_MCP_PROJECT_PATH"] = str(workers[0][1])
    if len(workers) == 2:
        environment["UNITY_MCP_SECOND_PORT"] = str(workers[1][0])
        environment["UNITY_MCP_SECOND_PROJECT_PATH"] = str(workers[1][1])
    if args.record is not None:
        environment["UNITY_MCP_TRACE_FILE"] = str(args.record)
    return environment


def _read_owned_junit(path: Path) -> bytes:
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags)
        with os.fdopen(descriptor, "rb") as stream:
            metadata = os.fstat(stream.fileno())
            if not stat.S_ISREG(metadata.st_mode):
                raise AttestedConformanceError("pytest JUnit output is not a regular file")
            payload = stream.read(16 * 1024 * 1024 + 1)
    except OSError as exc:
        raise AttestedConformanceError("pytest JUnit output cannot be read") from exc
    if len(payload) > 16 * 1024 * 1024:
        raise AttestedConformanceError("pytest JUnit output exceeds the size limit")
    return payload


def _publish_bytes(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix=f".{path.name}.")
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except Exception:
        with suppress(FileNotFoundError):
            os.unlink(temporary)
        raise


if __name__ == "__main__":
    raise SystemExit(main())
