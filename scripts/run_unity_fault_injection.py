#!/usr/bin/env python3
"""Run cleanup fault probes against one disposable Unity MCP worker.

The script deliberately starts tests that fail. Success means each fault is one
ordinary failed UTF leaf, its paired canary passes, and both durable runs finish
with complete evidence under their exact request_id/run_id identities.
"""

from __future__ import annotations

import argparse
import asyncio
import contextlib
import json
import re
import socket
import struct
import sys
import time
import uuid
from dataclasses import dataclass
from pathlib import Path

UTF_VERSION = "1.6.0"
CATEGORY = "UnityMCP.FaultInjection"
PORTS_DIR = Path.home() / ".unity-biome-mcp" / "ports"
ALLOWED_STATES = {"prepared", "dispatched", "running", "finalizing", "terminal"}
IDENTITY_RE = re.compile(r"^[A-Za-z0-9._-]{1,200}$")


@dataclass(frozen=True)
class Scenario:
    name: str
    fault_filter: str
    canary_filter: str
    failure_marker: str


SCENARIOS = {
    "sync": Scenario(
        "sync",
        "UnityMCP.Editor.Tests.FaultInjection.SyncTearDownFailureProbe."
        "FailsInDerivedTearDownAfterDirtyingScene",
        "UnityMCP.Editor.Tests.FaultInjection.SyncTearDownCleanupCanary."
        "SentinelAndSceneAreClean",
        "UNITYMCP_EXPECTED_SYNC_TEARDOWN_FAILURE",
    ),
    "async": Scenario(
        "async",
        "UnityMCP.Editor.Tests.FaultInjection.AsyncTearDownFailureProbe."
        "FaultsTaskTearDownAfterDirtyingScene",
        "UnityMCP.Editor.Tests.FaultInjection.AsyncTearDownCleanupCanary."
        "SentinelAndSceneAreClean",
        "UNITYMCP_EXPECTED_ASYNC_TEARDOWN_FAILURE",
    ),
    "setup": Scenario(
        "setup",
        "UnityMCP.Editor.Tests.FaultInjection.SetUpFailureProbe.BodyMustNotRun",
        "UnityMCP.Editor.Tests.FaultInjection.SetUpFailureCleanupCanary."
        "SentinelAndSceneAreClean",
        "UNITYMCP_EXPECTED_SETUP_FAILURE",
    ),
}


class FaultLaneError(RuntimeError):
    pass


class TransportUncertain(FaultLaneError):
    pass


def _read_exact(sock: socket.socket, count: int) -> bytes:
    chunks: list[bytes] = []
    remaining = count
    while remaining:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("Unity closed the MCP connection")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def _call_sync(port: int, command: str, args: dict[str, object], timeout: float) -> str:
    message_id = "fault-lane-" + uuid.uuid4().hex
    payload = json.dumps(
        {
            "id": message_id,
            "cmd": command,
            "args": args,
            "role": "fault-injection-worker",
        },
        separators=(",", ":"),
    ).encode("utf-8")
    with socket.create_connection(("127.0.0.1", port), timeout=timeout) as sock:
        sock.settimeout(timeout)
        sock.sendall(struct.pack("!I", len(payload)) + payload)
        length = struct.unpack("!I", _read_exact(sock, 4))[0]
        if length <= 0 or length > 64 * 1024 * 1024:
            raise ConnectionError(f"Invalid MCP response length: {length}")
        response = json.loads(_read_exact(sock, length).decode("utf-8"))
    if response.get("ev") == "going_away":
        raise TransportUncertain(
            "Unity announced domain reload before returning the response"
        )
    if response.get("id") != message_id:
        raise TransportUncertain("MCP response id did not match the request")
    if not response.get("ok"):
        raise FaultLaneError(f"{command} failed: {response.get('err', 'unknown error')}")
    data = response.get("data", "")
    if not isinstance(data, str):
        raise FaultLaneError(f"{command} returned non-string data")
    return data


async def call(port: int, command: str, args: dict[str, object], timeout: float = 15.0) -> str:
    return await asyncio.wait_for(
        asyncio.to_thread(_call_sync, port, command, args, timeout),
        timeout=timeout + 1.0,
    )


def parse_record(value: str) -> tuple[str, dict[str, str]] | None:
    parts = value.split("|")
    if not parts or parts[0] not in {"tests-started", "test-request"}:
        return None
    fields: dict[str, str] = {}
    for part in parts[1:]:
        if "=" not in part:
            return None
        key, field_value = part.split("=", 1)
        if not key or key in fields:
            return None
        fields[key] = field_value
    return parts[0], fields


def correlated_run_id(value: str, request_id: str) -> str | None:
    status = correlated_status(value, request_id)
    if status is None:
        return None
    prefix, fields = status
    if prefix == "tests-started" and not fields.get("utf_guid"):
        return None
    if fields.get("outcome") == "dispatch_failed":
        raise FaultLaneError(
            f"Unity rejected request {request_id}: {value}"
        )
    return fields["run_id"]


def correlated_status(
    value: str, request_id: str
) -> tuple[str, dict[str, str]] | None:
    record = parse_record(value)
    if record is None:
        return None
    prefix, fields = record
    if (
        fields.get("request_id") != request_id
        or IDENTITY_RE.fullmatch(fields.get("run_id", "")) is None
        or fields.get("state") not in ALLOWED_STATES
    ):
        return None
    return prefix, fields


def is_recoverable_prepared(status: tuple[str, dict[str, str]]) -> bool:
    prefix, fields = status
    return (
        prefix == "test-request"
        and fields.get("state") == "prepared"
        and not fields.get("outcome")
    )


def advertised_ports(project: Path, requested_port: int | None) -> list[int]:
    matches: list[tuple[float, int]] = []
    for port_file in PORTS_DIR.glob("*.port"):
        try:
            lines = port_file.read_text(encoding="utf-8").splitlines()
            port = int(lines[0])
            advertised_project = Path(lines[1]).resolve()
            if advertised_project == project.resolve():
                matches.append((port_file.stat().st_mtime, port))
        except (OSError, ValueError, IndexError):  # noqa: PERF203
            continue
    matches.sort(reverse=True)
    ports = list(dict.fromkeys(port for _, port in matches))
    if requested_port is not None:
        if requested_port not in ports:
            raise FaultLaneError(
                f"Port {requested_port} is not advertised by worker {project}"
            )
        return [requested_port]
    if not ports:
        raise FaultLaneError(f"No live MCP port file advertises worker {project}")
    return ports


def connection_candidates(project: Path, requested_port: int | None) -> list[int]:
    if requested_port is not None:
        if requested_port < 1 or requested_port > 65535:
            raise FaultLaneError(f"Invalid MCP port: {requested_port}")
        # A domain reload temporarily removes the port file. An explicit port is
        # still a valid connection candidate, but never a trusted project identity.
        return [requested_port]
    return advertised_ports(project, None)


async def verified_port(
    project: Path,
    requested_port: int | None,
    timeout: float = 10.0,
) -> int:
    errors: list[str] = []
    for port in connection_candidates(project, requested_port):
        try:
            actual = Path(
                await call(
                    port,
                    "editor",
                    {"action": "project_path"},
                    timeout=timeout,
                )
            ).resolve()
            if actual == project.resolve():
                return port
            errors.append(f"{port}: serves {actual}")
        except (OSError, ConnectionError, asyncio.TimeoutError, FaultLaneError) as error:  # noqa: PERF203
            errors.append(f"{port}: {type(error).__name__}: {error}")
    raise FaultLaneError(
        f"No responsive MCP endpoint belongs to {project}: " + "; ".join(errors)
    )


async def wait_for_verified_port(
    project: Path,
    requested_port: int | None,
    deadline: float,
    poll_interval: float,
) -> int:
    last_error = "endpoint unavailable"
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            break
        try:
            return await verified_port(
                project,
                requested_port,
                timeout=max(0.05, min(10.0, remaining)),
            )
        except (OSError, ConnectionError, asyncio.TimeoutError, FaultLaneError) as error:
            last_error = f"{type(error).__name__}: {error}"
        await asyncio.sleep(
            min(poll_interval, max(0.0, deadline - time.monotonic()))
        )
    port_label = str(requested_port) if requested_port is not None else "auto"
    raise FaultLaneError(
        f"Timed out waiting for verified worker endpoint port={port_label} "
        f"project={project.resolve()}: {last_error}"
    )


async def read_with_rediscovery(
    project: Path,
    requested_port: int | None,
    command: str,
    args: dict[str, object],
) -> str:
    errors: list[str] = []
    for port in connection_candidates(project, requested_port):
        try:
            actual = Path(
                await call(port, "editor", {"action": "project_path"}, timeout=10.0)
            ).resolve()
            if actual != project.resolve():
                errors.append(f"{port}: serves {actual}")
                continue
            return await call(port, command, args)
        except (OSError, ConnectionError, asyncio.TimeoutError, TransportUncertain) as error:
            errors.append(f"{port}: {type(error).__name__}")
    raise ConnectionError(
        f"No current worker endpoint answered {command}: " + ", ".join(errors)
    )


async def resolve_run_id(
    project: Path,
    requested_port: int | None,
    request_id: str,
    initial: str,
    deadline: float,
    poll_interval: float,
    resume_args: dict[str, object],
) -> str:
    candidate = initial
    resumed_prepared = False
    last_run_id = ""
    while time.monotonic() < deadline:
        status = correlated_status(candidate, request_id)
        if status is not None:
            run_id = correlated_run_id(candidate, request_id)
            if run_id is not None:
                last_run_id = run_id
                if not is_recoverable_prepared(status):
                    return run_id
                if not resumed_prepared:
                    resumed_prepared = True
                    port = await wait_for_verified_port(
                        project,
                        requested_port,
                        deadline,
                        poll_interval,
                    )
                    try:
                        candidate = await call(
                            port, "run_tests", resume_args, timeout=30.0
                        )
                    except (
                        OSError,
                        ConnectionError,
                        asyncio.TimeoutError,
                        TransportUncertain,
                    ):
                        candidate = ""
                    continued = correlated_status(candidate, request_id)
                    if continued is not None and continued[1]["run_id"] != last_run_id:
                        raise FaultLaneError(
                            "prepared request recovery changed its durable run_id"
                        )
                    continue
        try:
            candidate = await read_with_rediscovery(
                project,
                requested_port,
                "resolve_test_request",
                {"request_id": request_id},
            )
        except (OSError, ConnectionError, asyncio.TimeoutError):
            candidate = ""
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    raise FaultLaneError(
        f"Could not resolve dispatched run for request_id={request_id}; "
        f"last_run_id={last_run_id or 'unknown'}"
    )


async def wait_for_terminal(
    project: Path,
    requested_port: int | None,
    request_id: str,
    run_id: str,
    deadline: float,
    poll_interval: float,
) -> dict[str, object]:
    last_snapshot = "none"
    while time.monotonic() < deadline:
        try:
            value = await read_with_rediscovery(
                project, requested_port, "get_test_run", {"run_id": run_id}
            )
            if value not in {"", "none", "pending"}:
                last_snapshot = value
                snapshot = json.loads(value)
                if not isinstance(snapshot, dict):
                    raise FaultLaneError("get_test_run returned a non-object snapshot")
                if snapshot.get("request_id") != request_id:
                    raise FaultLaneError("snapshot request_id correlation failed")
                if snapshot.get("run_id") != run_id:
                    raise FaultLaneError("snapshot run_id correlation failed")
                if snapshot.get("state") == "terminal":
                    return snapshot
        except json.JSONDecodeError as error:
            raise FaultLaneError(f"Invalid get_test_run JSON: {error}") from error
        except (OSError, ConnectionError, asyncio.TimeoutError):
            pass
        await asyncio.sleep(poll_interval)
    raise FaultLaneError(
        f"Timed out waiting for run_id={run_id}; last snapshot={last_snapshot}"
    )


async def start_exact_run(
    project: Path,
    requested_port: int | None,
    filter_name: str,
    timeout: float,
    poll_interval: float,
) -> dict[str, object]:
    request_id = "fault-" + uuid.uuid4().hex
    deadline = time.monotonic() + timeout
    command_args: dict[str, object] = {
        "mode": "EditMode",
        "filter": filter_name,
        "request_id": request_id,
    }
    initial = ""
    port = await wait_for_verified_port(
        project,
        requested_port,
        deadline,
        poll_interval,
    )
    # Dispatch may have succeeded. Resolve the same request identity; never retry it.
    with contextlib.suppress(OSError, ConnectionError, asyncio.TimeoutError, TransportUncertain):
        initial = await call(
            port,
            "run_tests",
            command_args,
            timeout=30.0,
        )
    run_id = await resolve_run_id(
        project,
        requested_port,
        request_id,
        initial,
        deadline,
        poll_interval,
        command_args,
    )
    print(f"    request_id={request_id}")
    print(f"    run_id={run_id}")
    return await wait_for_terminal(
        project, requested_port, request_id, run_id, deadline, poll_interval
    )


def error_issues(snapshot: dict[str, object]) -> list[dict[str, object]]:
    issues = snapshot.get("issues", [])
    if not isinstance(issues, list):
        raise FaultLaneError("snapshot issues field is not an array")
    return [
        issue
        for issue in issues
        if isinstance(issue, dict) and issue.get("severity") == "error"
    ]


def validate_common(
    snapshot: dict[str, object],
    filter_name: str,
    expected_project: Path,
) -> dict[str, object]:
    required_true = (
        "is_terminal",
        "execution_finished",
        "cleanup_complete",
        "run_started_observed",
        "manifest_complete",
        "run_finished_observed",
        "build_coherent",
    )
    false_fields = [name for name in required_true if snapshot.get(name) is not True]
    if false_fields:
        raise FaultLaneError(f"Incomplete durable evidence: {', '.join(false_fields)}")
    if snapshot.get("utf_version") != UTF_VERSION:
        raise FaultLaneError(
            f"Expected UTF {UTF_VERSION}, got {snapshot.get('utf_version')!r}"
        )
    if Path(str(snapshot.get("project_identity", ""))).resolve() != expected_project.resolve():
        raise FaultLaneError("run snapshot belongs to a different Unity project")
    if snapshot.get("state") != "terminal" or snapshot.get("lifecycle") != "terminal":
        raise FaultLaneError("terminal state/lifecycle correlation failed")
    if snapshot.get("source") != "mcp" or snapshot.get("mode") != "EditMode":
        raise FaultLaneError("run source/mode does not match this worker lane")
    if snapshot.get("filter") != filter_name:
        raise FaultLaneError("run snapshot filter does not match the exact requested leaf")
    exact_one_fields = (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "started_attempt_count",
        "finished_attempt_count",
    )
    if any(snapshot.get(name) != 1 for name in exact_one_fields):
        raise FaultLaneError("Exact filter did not produce exactly one terminal leaf")
    exact_zero_fields = (
        "unmaterialized_expected_count",
        "finish_without_start_count",
        "missing_count",
        "unexpected_count",
        "conflict_count",
        "cancelled",
        "invalid",
    )
    if any(snapshot.get(name) != 0 for name in exact_zero_fields):
        raise FaultLaneError("Run has missing, unexpected, conflicting, or invalid evidence")
    if snapshot.get("utf_xml_scope") != "complete":
        raise FaultLaneError("UTF XML is not a complete same-observer capture")
    issues = error_issues(snapshot)
    if issues:
        raise FaultLaneError(f"Run-level error/RunError evidence: {issues}")
    leaves = snapshot.get("leaves")
    if not isinstance(leaves, list) or len(leaves) != 1 or not isinstance(leaves[0], dict):
        raise FaultLaneError("Expected one materialized leaf result")
    leaf = leaves[0]
    identity = str(leaf.get("full_name") or "")
    if identity != filter_name:
        raise FaultLaneError(
            f"Terminal leaf {identity!r} does not match exact filter {filter_name!r}"
        )
    if leaf.get("attempt_count") != 1:
        raise FaultLaneError("Exact leaf did not have exactly one observed attempt")
    return leaf


def validate_fault(
    snapshot: dict[str, object], scenario: Scenario, expected_project: Path
) -> None:
    leaf = validate_common(snapshot, scenario.fault_filter, expected_project)
    if snapshot.get("outcome") != "failed" or snapshot.get("failed") != 1:
        raise FaultLaneError(
            "Expected one ordinary failed leaf, not a passed/invalid/incomplete run"
        )
    if leaf.get("outcome") != "failed":
        raise FaultLaneError(f"Fault leaf outcome is {leaf.get('outcome')!r}")
    message = str(leaf.get("message") or "")
    if scenario.failure_marker not in message:
        raise FaultLaneError(
            f"Fault leaf did not contain marker {scenario.failure_marker}"
        )


def validate_canary(
    snapshot: dict[str, object], scenario: Scenario, expected_project: Path
) -> None:
    leaf = validate_common(snapshot, scenario.canary_filter, expected_project)
    if snapshot.get("outcome") != "passed" or snapshot.get("passed") != 1:
        raise FaultLaneError("Cleanup canary did not produce one passed leaf")
    if leaf.get("outcome") != "passed":
        raise FaultLaneError(f"Canary leaf outcome is {leaf.get('outcome')!r}")


def validate_worker_project(project: Path) -> None:
    project = project.resolve()
    repo_root = Path(__file__).resolve().parents[1]
    if project == repo_root or repo_root in project.parents:
        raise FaultLaneError(
            "Fault injection is forbidden inside the source checkout; use a disposable copy"
        )
    if not (project / "Assets").is_dir():
        raise FaultLaneError(f"Not a Unity project: {project}")
    manifest_path = project / "Packages" / "manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        version = manifest["dependencies"]["com.unity.test-framework"]
    except (OSError, KeyError, TypeError, json.JSONDecodeError) as error:
        raise FaultLaneError(f"Cannot verify worker package manifest: {error}") from error
    if version != UTF_VERSION:
        raise FaultLaneError(f"Worker must pin UTF {UTF_VERSION}; manifest has {version!r}")


def discover_port(project: Path, requested_port: int | None) -> int:
    return advertised_ports(project, requested_port)[0]


async def run(args: argparse.Namespace) -> None:
    project = args.project.resolve()
    validate_worker_project(project)
    port = await wait_for_verified_port(
        project,
        args.port,
        time.monotonic() + args.timeout,
        args.poll_interval,
    )
    pong = await call(port, "ping", {}, timeout=10.0)
    if pong != "pong":
        raise FaultLaneError(f"Unexpected ping response from port {port}: {pong!r}")
    advertised_project = await call(
        port, "editor", {"action": "project_path"}, timeout=10.0
    )
    if Path(advertised_project).resolve() != project:
        raise FaultLaneError(
            f"Port {port} serves {advertised_project!r}, not worker {str(project)!r}"
        )

    selected = list(SCENARIOS.values()) if args.scenario == "all" else [SCENARIOS[args.scenario]]
    print(f"Worker: {project}")
    print(f"MCP port: {port}")
    print(f"Category: {CATEGORY}")

    for scenario in selected:
        print(f"  [{scenario.name}] expected-failure leaf")
        fault = await start_exact_run(
            project, args.port, scenario.fault_filter, args.timeout, args.poll_interval
        )
        validate_fault(fault, scenario, project)
        print("    terminal outcome=failed, one failed leaf, no RunError")

        print(f"  [{scenario.name}] cleanup canary")
        canary = await start_exact_run(
            project, args.port, scenario.canary_filter, args.timeout, args.poll_interval
        )
        validate_canary(canary, scenario, project)
        print("    terminal outcome=passed, sentinel and scene verified")

    print("Fault-injection lane passed.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--project",
        type=Path,
        required=True,
        help="absolute path of the disposable Unity worker project",
    )
    parser.add_argument("--port", type=int, help="worker MCP port (otherwise discovered)")
    parser.add_argument(
        "--scenario",
        choices=("all", *SCENARIOS.keys()),
        default="all",
        help="fault/canary pair to run (default: all)",
    )
    parser.add_argument("--timeout", type=float, default=300.0)
    parser.add_argument("--poll-interval", type=float, default=1.0)
    parser.add_argument(
        "--confirm-disposable-worker",
        action="store_true",
        help="required acknowledgement that the target project may be discarded",
    )
    args = parser.parse_args()
    if not args.confirm_disposable_worker:
        parser.error("--confirm-disposable-worker is required")
    if args.timeout <= 0 or args.poll_interval <= 0:
        parser.error("timeouts must be positive")
    return args


def main() -> int:
    try:
        asyncio.run(run(parse_args()))
        return 0
    except (FaultLaneError, OSError, asyncio.TimeoutError) as error:
        print(f"FAULT LANE FAILED: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("FAULT LANE INTERRUPTED", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
