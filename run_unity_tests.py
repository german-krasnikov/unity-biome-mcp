#!/usr/bin/env python3
"""Run one exact Unity test request through the durable MCP protocol.

Examples:
    python3 run_unity_tests.py EditMode
    python3 run_unity_tests.py EditMode --filter My.Namespace.Fixture.Test
    python3 run_unity_tests.py --project /private/tmp/unity-worker --timeout 1800

The command dispatches once. If the ACK is lost during a domain reload it
resolves the same request_id. A request-only ``prepared`` intent is resumed
with the same immutable identity and payload; a dispatched run is never resent.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
from pathlib import Path
import re
import socket
import struct
import sys
import time
import uuid
from typing import Any


PORTS_DIR = Path.home() / ".unity-biome-mcp" / "ports"
DEFAULT_PROJECT = Path(__file__).resolve().parent / "unity-test-project"
IDENTITY_RE = re.compile(r"^[A-Za-z0-9._-]{1,200}$")
RUN_STATES = {"prepared", "dispatched", "running", "finalizing", "terminal"}
TERMINAL_OUTCOMES = {
    "passed",
    "failed",
    "cancelled",
    "incomplete",
    "invalid",
    "dispatch_failed",
}
MAX_FILE_RESPONSE_BYTES = 64 * 1024 * 1024
CANONICAL_FULL_EDITMODE_MINIMUM = 6001


class RunnerError(RuntimeError):
    pass


class TransportUncertain(RunnerError):
    pass


class RetryableServerState(TransportUncertain):
    def __init__(self, message: str, retry_ms: int) -> None:
        super().__init__(message)
        self.retry_seconds = retry_ms / 1000.0


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


def _read_file_backed_text(
    file_value: object,
    *,
    command: str,
    response_file_roots: list[Path] | None,
) -> str:
    if not isinstance(file_value, str) or not file_value:
        raise RunnerError(f"{command} returned an invalid response file path")
    if not response_file_roots:
        raise RunnerError(
            f"{command} returned file-backed data without an allowed response root"
        )

    try:
        response_path = Path(file_value).resolve(strict=True)
    except OSError as error:
        raise RunnerError(
            f"{command} returned a response file outside any allowed root"
        ) from error
    if not any(response_path.is_relative_to(r.resolve()) for r in response_file_roots):
        raise RunnerError(
            f"{command} returned a response file outside any allowed root"
        )
    if not response_path.is_file():
        raise RunnerError(f"{command} response path is not a regular file")

    try:
        size = response_path.stat().st_size
        if size > MAX_FILE_RESPONSE_BYTES:
            raise RunnerError(
                f"{command} response file is too large: {size} bytes"
            )
        payload = response_path.read_bytes()
    except OSError as error:
        raise RunnerError(f"could not read {command} response file: {error}") from error
    if len(payload) > MAX_FILE_RESPONSE_BYTES:
        raise RunnerError(
            f"{command} response file is too large: {len(payload)} bytes"
        )
    try:
        return payload.decode("utf-8")
    except UnicodeDecodeError as error:
        raise RunnerError(f"{command} response file is not UTF-8") from error


def _call_sync(
    port: int,
    command: str,
    args: dict[str, object],
    timeout: float,
    response_file_roots: list[Path] | None = None,
) -> str:
    message_id = "standalone-tests-" + uuid.uuid4().hex
    payload = json.dumps(
        {
            "id": message_id,
            "cmd": command,
            "args": args,
            "role": "standalone-test-runner",
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
    if not isinstance(response, dict):
        raise RunnerError(f"{command} returned a non-object MCP response")
    if response.get("ev") == "going_away":
        raise TransportUncertain(
            "Unity announced domain reload before returning the response"
        )
    if response.get("id") != message_id:
        raise RunnerError("MCP response id did not match the request")
    if not response.get("ok"):
        error = str(response.get("err", "unknown error"))
        retry_ms = response.get("retry")
        if (
            isinstance(retry_ms, int)
            and not isinstance(retry_ms, bool)
            and 0 < retry_ms <= 600_000
        ):
            raise RetryableServerState(
                f"{command} temporarily unavailable: {error}", retry_ms
            )
        raise RunnerError(f"{command} failed: {error}")
    if "file" in response:
        return _read_file_backed_text(
            response["file"],
            command=command,
            response_file_roots=response_file_roots,
        )
    data = response.get("data", "")
    if not isinstance(data, str):
        raise RunnerError(f"{command} returned non-string data")
    return data


async def call(
    port: int,
    command: str,
    args: dict[str, object],
    timeout: float = 15.0,
    response_file_roots: list[Path] | None = None,
) -> str:
    return await asyncio.wait_for(
        asyncio.to_thread(
            _call_sync,
            port,
            command,
            args,
            timeout,
            response_file_roots,
        ),
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
    return None if status is None else status[1]["run_id"]


def correlated_status(
    value: str, request_id: str
) -> tuple[str, dict[str, str]] | None:
    parsed = parse_record(value)
    if parsed is None:
        return None
    prefix, fields = parsed
    run_id = fields.get("run_id", "")
    state = fields.get("state", "")
    if (
        fields.get("request_id") != request_id
        or not IDENTITY_RE.fullmatch(run_id)
        or state not in RUN_STATES
    ):
        return None
    if prefix == "tests-started" and not fields.get("utf_guid"):
        return None
    outcome = fields.get("outcome", "")
    if outcome and outcome not in TERMINAL_OUTCOMES:
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
        except (OSError, ValueError, IndexError):
            continue
    matches.sort(reverse=True)
    ports = [port for _, port in matches]
    if requested_port is not None:
        if requested_port not in ports:
            raise RunnerError(
                f"Port {requested_port} is not advertised by project {project}"
            )
        return [requested_port]
    if not ports:
        raise RunnerError(f"No live MCP port advertises project {project}")
    return list(dict.fromkeys(ports))


def rediscovery_candidates(project: Path, requested_port: int | None) -> list[int]:
    ports: list[int] = []
    try:
        ports.extend(advertised_ports(project, None))
    except RunnerError:
        pass
    if requested_port is not None:
        if not 1 <= requested_port <= 65535:
            raise RunnerError(f"Invalid MCP port: {requested_port}")
        if requested_port not in ports:
            ports.append(requested_port)
    if not ports:
        raise RunnerError(f"No MCP endpoint candidate exists for project {project}")
    return ports


async def verified_port(
    project: Path,
    requested_port: int | None,
    *,
    rediscover: bool = False,
) -> int:
    errors: list[str] = []
    candidates = (
        rediscovery_candidates(project, requested_port)
        if rediscover
        else advertised_ports(project, requested_port)
    )
    for port in candidates:
        try:
            actual = Path(
                await call(port, "editor", {"action": "project_path"}, timeout=10.0)
            ).resolve()
            if actual == project.resolve():
                return port
            errors.append(f"{port}: serves {actual}")
        except (OSError, ConnectionError, asyncio.TimeoutError, RunnerError) as error:
            errors.append(f"{port}: {type(error).__name__}: {error}")
    raise RunnerError(
        f"No responsive MCP endpoint belongs to {project}: " + "; ".join(errors)
    )


async def wait_for_verified_port(
    project: Path,
    requested_port: int | None,
    deadline: float,
    poll_interval: float,
    *,
    rediscover: bool = False,
) -> int:
    last_error = "endpoint unavailable"
    while time.monotonic() < deadline:
        try:
            return await verified_port(
                project,
                requested_port,
                rediscover=rediscover,
            )
        except (OSError, ConnectionError, asyncio.TimeoutError, RunnerError) as error:
            last_error = f"{type(error).__name__}: {error}"
        await asyncio.sleep(
            min(poll_interval, max(0.0, deadline - time.monotonic()))
        )
    raise RunnerError(
        f"Timed out waiting for verified endpoint for {project}: {last_error}"
    )


async def read_with_rediscovery(
    project: Path,
    requested_port: int | None,
    command: str,
    args: dict[str, object],
) -> str:
    errors: list[str] = []
    try:
        ports = rediscovery_candidates(project, requested_port)
    except RunnerError as error:
        raise TransportUncertain(str(error)) from error
    for port in ports:
        try:
            actual = Path(
                await call(port, "editor", {"action": "project_path"}, timeout=10.0)
            ).resolve()
            if actual != project.resolve():
                errors.append(f"{port}: serves {actual}")
                continue
            return await call(
                port,
                command,
                args,
                response_file_roots=[project / "Temp" / "MCP", project / "ScreenShots"],
            )
        except (OSError, ConnectionError, asyncio.TimeoutError, TransportUncertain) as error:
            errors.append(f"{port}: {type(error).__name__}")
    raise TransportUncertain(
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
    resume_attempted = False
    last_run_id = ""
    while time.monotonic() < deadline:
        status = correlated_status(candidate, request_id)
        if status is not None:
            last_run_id = status[1]["run_id"]
            if not is_recoverable_prepared(status):
                return last_run_id
            if not resume_attempted:
                port = await wait_for_verified_port(
                    project,
                    requested_port,
                    deadline,
                    poll_interval,
                    rediscover=True,
                )
                resume_attempted = True
                try:
                    candidate = await call(
                        port, "run_tests", resume_args, timeout=30.0
                    )
                except (OSError, ConnectionError, asyncio.TimeoutError, TransportUncertain):
                    candidate = ""
                continued = correlated_status(candidate, request_id)
                if continued is not None and continued[1]["run_id"] != last_run_id:
                    raise RunnerError(
                        "prepared request recovery changed its durable run_id"
                    )
                continue

        try:
            resolved = await read_with_rediscovery(
                project,
                requested_port,
                "resolve_test_request",
                {"request_id": request_id},
            )
            candidate = resolved
            if (
                resolved not in {"", "none", "pending"}
                and correlated_status(resolved, request_id) is None
            ):
                raise RunnerError(
                    "resolve_test_request returned malformed or uncorrelated evidence: "
                    + resolved
                )
        except TransportUncertain:
            candidate = ""
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    raise RunnerError(
        f"Could not resolve a dispatched run for request_id={request_id}; "
        f"last_run_id={last_run_id or 'unknown'}"
    )


def decode_snapshot(value: str, request_id: str, run_id: str) -> dict[str, Any]:
    try:
        snapshot = json.loads(value)
    except json.JSONDecodeError as error:
        raise RunnerError(f"get_test_run returned invalid JSON: {error}") from error
    if not isinstance(snapshot, dict):
        raise RunnerError("get_test_run returned a non-object snapshot")
    if snapshot.get("request_id") != request_id:
        raise RunnerError("snapshot request_id does not match the dispatched request")
    if snapshot.get("run_id") != run_id:
        raise RunnerError("snapshot run_id does not match the resolved run")
    return snapshot


async def confirm_existing_request(
    project: Path,
    requested_port: int | None,
    request_id: str,
    run_id: str,
) -> None:
    value = await read_with_rediscovery(
        project,
        requested_port,
        "resolve_test_request",
        {"request_id": request_id},
    )
    if value in {"", "none", "pending"}:
        raise TransportUncertain(
            f"request_id={request_id} is temporarily unavailable during recovery"
        )
    status = correlated_status(value, request_id)
    if status is None:
        raise RunnerError(
            "resolve_test_request returned malformed or uncorrelated evidence: " + value
        )
    resolved_run_id = status[1]["run_id"]
    if resolved_run_id != run_id:
        raise RunnerError(
            f"request_id={request_id} changed run_id from {run_id} to {resolved_run_id}"
        )


async def wait_for_terminal(
    project: Path,
    requested_port: int | None,
    request_id: str,
    run_id: str,
    deadline: float,
    poll_interval: float,
) -> dict[str, Any]:
    last_snapshot: dict[str, Any] | None = None
    last_state = ""
    must_reconfirm = False
    while time.monotonic() < deadline:
        try:
            if must_reconfirm:
                await confirm_existing_request(
                    project, requested_port, request_id, run_id
                )
                must_reconfirm = False
            value = await read_with_rediscovery(
                project, requested_port, "get_test_run", {"run_id": run_id}
            )
            if value not in {"", "none", "pending"}:
                last_snapshot = decode_snapshot(value, request_id, run_id)
                state = str(
                    last_snapshot.get("state") or last_snapshot.get("lifecycle") or ""
                )
                if state != last_state:
                    completed = last_snapshot.get("completed_expected_count", 0)
                    expected = last_snapshot.get("expected_count", 0)
                    current = last_snapshot.get("current_test", "")
                    print(
                        f"  state={state} completed={completed}/{expected}"
                        + (f" current={current}" if current else "")
                    )
                    last_state = state
                if state == "terminal":
                    return last_snapshot
        except TransportUncertain:
            must_reconfirm = True
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    compact = json.dumps(last_snapshot, separators=(",", ":"), sort_keys=True)
    raise TimeoutError(
        f"Caller deadline expired for request_id={request_id} run_id={run_id}; "
        f"Unity was not marked complete; last_snapshot={compact}"
    )


def _require_equal(snapshot: dict[str, Any], names: tuple[str, ...], expected: int) -> None:
    failures = [f"{name}={snapshot.get(name)!r}" for name in names if snapshot.get(name) != expected]
    if failures:
        raise RunnerError("terminal count invariant failed: " + ", ".join(failures))


def required_minimum_tests(
    mode: str,
    filter_name: str,
    group: str,
    requested: int | None,
    allow_empty: bool,
) -> int:
    if requested is not None and requested < 0:
        raise RunnerError("minimum_tests cannot be negative")
    baseline = 0 if allow_empty else 1
    if mode == "EditMode" and not filter_name and not group:
        baseline = CANONICAL_FULL_EDITMODE_MINIMUM
    if requested is not None:
        baseline = max(baseline, requested)
    return baseline


def validate_terminal(
    snapshot: dict[str, Any],
    *,
    project: Path,
    mode: str,
    filter_name: str,
    group: str,
    expected_utf: str,
    allow_empty: bool,
    minimum_tests: int = 0,
) -> None:
    required_true = (
        "is_terminal",
        "execution_finished",
        "cleanup_complete",
        "run_started_observed",
        "manifest_complete",
        "run_finished_observed",
        "build_coherent",
    )
    missing_flags = [name for name in required_true if snapshot.get(name) is not True]
    if missing_flags:
        raise RunnerError("terminal evidence is incomplete: " + ", ".join(missing_flags))
    if snapshot.get("state") != "terminal" or snapshot.get("lifecycle") != "terminal":
        raise RunnerError("state and lifecycle must both be terminal")
    if snapshot.get("outcome") not in TERMINAL_OUTCOMES:
        raise RunnerError(f"unknown terminal outcome: {snapshot.get('outcome')!r}")
    if not snapshot.get("utf_guid"):
        raise RunnerError("terminal run has no correlated UTF GUID")
    if snapshot.get("utf_version") != expected_utf:
        raise RunnerError(
            f"expected UTF {expected_utf}, got {snapshot.get('utf_version')!r}"
        )
    if Path(str(snapshot.get("project_identity", ""))).resolve() != project.resolve():
        raise RunnerError("terminal snapshot belongs to a different Unity project")
    if snapshot.get("source") != "mcp" or snapshot.get("mode") != mode:
        raise RunnerError("terminal source/mode does not match the request")
    if str(snapshot.get("filter") or "") != filter_name:
        raise RunnerError("terminal filter does not match the request")
    if str(snapshot.get("group") or "") != group:
        raise RunnerError("terminal group does not match the request")

    expected = snapshot.get("expected_count")
    if (
        not isinstance(expected, int)
        or expected < 0
        or (expected == 0 and not allow_empty)
    ):
        raise RunnerError(f"invalid expected_count={expected!r}")
    if expected < minimum_tests:
        raise RunnerError(
            f"expected_count={expected} is below required minimum_tests={minimum_tests}"
        )
    _require_equal(
        snapshot,
        (
            "declared_expected_count",
            "readable_manifest_count",
            "completed_expected_count",
            "unique_terminal_count",
        ),
        expected,
    )
    _require_equal(
        snapshot,
        (
            "unmaterialized_expected_count",
            "missing_count",
            "unexpected_count",
            "conflict_count",
        ),
        0,
    )
    statuses = (
        "passed",
        "failed",
        "skipped",
        "inconclusive",
        "cancelled",
        "invalid",
    )
    if sum(snapshot.get(name, 0) for name in statuses) != expected:
        raise RunnerError("terminal status totals do not equal expected_count")
    if snapshot.get("utf_xml_scope") not in {"complete", "partial"}:
        raise RunnerError("RunFinished XML scope evidence is missing")
    issues = snapshot.get("issues")
    if not isinstance(issues, list):
        raise RunnerError("terminal issues is not an array")
    errors = [
        issue
        for issue in issues
        if isinstance(issue, dict) and issue.get("severity") == "error"
    ]
    if errors:
        raise RunnerError("terminal run has infrastructure errors: " + json.dumps(errors))
    if snapshot.get("outcome") != "passed":
        raise RunnerError(
            f"test run completed with outcome={snapshot.get('outcome')}, "
            f"failed={snapshot.get('failed')}, invalid={snapshot.get('invalid')}"
        )


async def run(args: argparse.Namespace) -> int:
    project = args.project.resolve()
    if not (project / "Assets").is_dir() or not (project / "Packages").is_dir():
        raise RunnerError(f"Not a Unity project: {project}")
    request_id = args.request_id or "standalone-" + uuid.uuid4().hex
    if IDENTITY_RE.fullmatch(request_id) is None:
        raise RunnerError("request_id must use 1-200 ASCII letters, digits, '.', '_' or '-'")
    minimum_tests = required_minimum_tests(
        args.mode,
        args.filter,
        args.group,
        args.minimum_tests,
        args.allow_empty,
    )
    deadline = time.monotonic() + args.timeout
    port = await wait_for_verified_port(
        project, args.port, deadline, args.poll_interval
    )
    print(f"project={project}")
    print(f"port={port}")
    print(f"request_id={request_id}")

    command_args: dict[str, object] = {
        "mode": args.mode,
        "request_id": request_id,
    }
    if args.filter:
        command_args["filter"] = args.filter
    if args.group:
        command_args["group"] = args.group

    initial = ""
    while time.monotonic() < deadline:
        try:
            initial = await call(port, "run_tests", command_args, timeout=30.0)
            break
        except RetryableServerState as error:
            print(f"  server busy before dispatch; retrying in {error.retry_seconds:g}s")
            await asyncio.sleep(
                min(
                    max(args.poll_interval, error.retry_seconds),
                    max(0.0, deadline - time.monotonic()),
                )
            )
            port = await wait_for_verified_port(
                project,
                args.port,
                deadline,
                args.poll_interval,
                rediscover=True,
            )
        except (OSError, ConnectionError, asyncio.TimeoutError, TransportUncertain):
            print("  start ACK uncertain; resolving the original request_id")
            break
    else:
        raise TimeoutError(f"Caller deadline expired before dispatching {request_id}")

    run_id = await resolve_run_id(
        project,
        args.port,
        request_id,
        initial,
        deadline,
        args.poll_interval,
        command_args,
    )
    print(f"run_id={run_id}")
    snapshot = await wait_for_terminal(
        project,
        args.port,
        request_id,
        run_id,
        deadline,
        args.poll_interval,
    )
    validate_terminal(
        snapshot,
        project=project,
        mode=args.mode,
        filter_name=args.filter,
        group=args.group,
        expected_utf=args.expected_utf,
        allow_empty=args.allow_empty,
        minimum_tests=minimum_tests,
    )
    if args.json:
        print(json.dumps(snapshot, indent=2, sort_keys=True))
    else:
        print(
            "PASS "
            f"expected={snapshot['expected_count']} passed={snapshot.get('passed', 0)} "
            f"skipped={snapshot.get('skipped', 0)} duration={snapshot.get('duration_seconds', 0)}s"
        )
    return 0


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", nargs="?", choices=("EditMode", "PlayMode"), default="EditMode")
    parser.add_argument("--project", type=Path, default=DEFAULT_PROJECT)
    parser.add_argument("--port", type=int, default=int(os.environ.get("UNITY_MCP_PORT", "0")) or None)
    parser.add_argument("--filter", default="")
    parser.add_argument("--group", default="")
    parser.add_argument("--request-id")
    parser.add_argument("--timeout", type=float, default=1800.0)
    parser.add_argument("--poll-interval", type=float, default=5.0)
    parser.add_argument("--expected-utf", default="1.6.0")
    parser.add_argument("--allow-empty", action="store_true")
    parser.add_argument(
        "--minimum-tests",
        type=int,
        default=None,
        help=(
            "minimum discovered leaves; unfiltered EditMode runs always require "
            f"at least {CANONICAL_FULL_EDITMODE_MINIMUM}"
        ),
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)
    if args.timeout <= 0 or args.poll_interval <= 0:
        parser.error("timeouts must be positive")
    return args


def main() -> int:
    try:
        return asyncio.run(run(parse_args()))
    except TimeoutError as error:
        print(f"TIMEOUT: {error}", file=sys.stderr)
        return 2
    except (RunnerError, OSError, asyncio.TimeoutError) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("INTERRUPTED", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
