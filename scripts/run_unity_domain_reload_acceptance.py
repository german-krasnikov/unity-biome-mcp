#!/usr/bin/env python3
"""Validate one and two real UTF domain reloads in a disposable Unity worker.

Preparation copies a worker-only test assembly into a stopped disposable worker::

    python3 scripts/run_unity_domain_reload_acceptance.py \
        --project /private/tmp/unity-domain-reload-worker \
        --prepare-only --confirm-disposable-worker

After that worker is running, the default run executes both acceptance scenarios.
Control and trace traffic stays under ``Library`` so no source or package file is
changed while UTF is executing.
"""

from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import os
import shutil
import socket
import sys
import time
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT))
import run_unity_tests as durable  # noqa: E402

UNITY_VERSION = "6000.0.65f1"
UNITY_REVISION = "a18e2220bd50"
UTF_VERSION = "1.6.0"
FIXTURE_SOURCE = Path(__file__).resolve().parent / "fixtures" / (
    "unity_domain_reload_acceptance"
)
FIXTURE_RELATIVE = Path("Assets/UnityMCPDomainReloadHarness")
FIXTURE_FILES = (
    "DomainReloadHarness.cs",
    "UnityMCP.Worker.DomainReloadHarness.asmdef",
)
FIXTURE_NAME = (
    "UnityMCP.Worker.DomainReloadAcceptance.DomainReloadHarness"
)
EXPECTED_LEAVES = frozenset(
    {
        f"{FIXTURE_NAME}.LeafBeforeReloadBoundary",
        f"{FIXTURE_NAME}.LeafBetweenReloadBoundaries",
        f"{FIXTURE_NAME}.LeafAfterReloadBoundaries",
    }
)
EVIDENCE_RELATIVE = Path("Library/UnityMCP/DomainReloadAcceptance")
CONTROL_SCHEMA = 1
TRACE_PARTIAL_LINE_RETRIES = 5
TRACE_PARTIAL_LINE_RETRY_SECONDS = 0.01


class AcceptanceError(durable.RunnerError):
    pass


@dataclass(frozen=True)
class TraceEntry:
    kind: str
    ordinal: int
    generation: str
    scenario_id: str


@dataclass(frozen=True)
class SourceFileEvidence:
    size: int
    mtime_ns: int
    sha256: str


def _read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise AcceptanceError(f"Cannot read JSON evidence {path}: {error}") from error
    if not isinstance(value, dict):
        raise AcceptanceError(f"Expected a JSON object in {path}")
    return value


def _atomic_write_json(path: Path, value: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
    temporary.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    os.replace(temporary, path)


def _project_version(project: Path) -> str:
    path = project / "ProjectSettings/ProjectVersion.txt"
    try:
        return path.read_text(encoding="utf-8")
    except OSError as error:
        raise AcceptanceError(f"Cannot read {path}: {error}") from error


def validate_worker_project(project: Path, *, require_lock: bool) -> None:
    project = project.resolve()
    if project == REPO_ROOT or REPO_ROOT in project.parents:
        raise AcceptanceError(
            "Domain reload acceptance is forbidden inside the source checkout"
        )
    if not all(
        (project / name).is_dir()
        for name in ("Assets", "Packages", "ProjectSettings")
    ):
        raise AcceptanceError(f"Not a Unity project: {project}")

    marker = _read_json(project / "Library/UnityMCP/disposable-worker.json")
    required_marker = {
        "schema_version": 1,
        "disposable": True,
        "unity_version": UNITY_VERSION,
        "unity_revision": UNITY_REVISION,
        "utf_version": UTF_VERSION,
    }
    mismatches = [
        f"{name}={marker.get(name)!r}"
        for name, expected in required_marker.items()
        if marker.get(name) != expected
    ]
    if mismatches:
        raise AcceptanceError(
            "Disposable worker marker mismatch: " + ", ".join(mismatches)
        )

    version = _project_version(project)
    if f"m_EditorVersion: {UNITY_VERSION}" not in version or UNITY_REVISION not in version:
        raise AcceptanceError(
            f"Worker must use Unity {UNITY_VERSION} revision {UNITY_REVISION}"
        )

    manifest = _read_json(project / "Packages/manifest.json")
    dependencies = manifest.get("dependencies")
    if not isinstance(dependencies, dict):
        raise AcceptanceError("Worker package manifest has no dependencies object")
    if dependencies.get("com.unity.test-framework") != UTF_VERSION:
        raise AcceptanceError(
            f"Worker manifest must pin built-in UTF {UTF_VERSION}"
        )

    if require_lock:
        lock = _read_json(project / "Packages/packages-lock.json")
        locked = lock.get("dependencies")
        utf = locked.get("com.unity.test-framework") if isinstance(locked, dict) else None
        if not isinstance(utf, dict):
            raise AcceptanceError("Resolved package lock has no UTF entry")
        if utf.get("version") != UTF_VERSION or utf.get("source") != "builtin":
            raise AcceptanceError(
                "Resolved UTF must be version 1.6.0 from Unity's builtin source"
            )


def install_worker_fixture(project: Path) -> Path:
    validate_worker_project(project, require_lock=False)
    target = project.resolve() / FIXTURE_RELATIVE
    target.mkdir(parents=True, exist_ok=True)
    for name in FIXTURE_FILES:
        source = FIXTURE_SOURCE / name
        destination = target / name
        if not source.is_file():
            raise AcceptanceError(f"Fixture source is missing: {source}")
        if destination.exists() and destination.read_bytes() != source.read_bytes():
            raise AcceptanceError(
                f"Refusing to overwrite a different worker fixture: {destination}"
            )
        shutil.copy2(source, destination)

    _atomic_write_json(
        project.resolve() / EVIDENCE_RELATIVE / "prepared.json",
        {
            "schema_version": CONTROL_SCHEMA,
            "unity_version": UNITY_VERSION,
            "utf_version": UTF_VERSION,
            "fixture": FIXTURE_NAME,
        },
    )
    return target


def validate_installed_fixture(project: Path) -> None:
    target = project.resolve() / FIXTURE_RELATIVE
    for name in FIXTURE_FILES:
        source = FIXTURE_SOURCE / name
        installed = target / name
        if not installed.is_file() or installed.read_bytes() != source.read_bytes():
            raise AcceptanceError(
                "Worker fixture is absent or changed; run --prepare-only before "
                f"launching Unity ({installed})"
            )


def parse_trace(path: Path) -> list[TraceEntry]:
    for attempt in range(TRACE_PARTIAL_LINE_RETRIES + 1):
        try:
            text = path.read_text(encoding="utf-8")
        except FileNotFoundError:
            return []
        except OSError as error:
            raise AcceptanceError(f"Cannot read harness trace {path}: {error}") from error

        lines = text.splitlines()
        entries: list[TraceEntry] = []
        retry_partial_tail = False
        for line_number, line in enumerate(lines, 1):
            fields = line.split("|")
            error_message = ""
            ordinal = 0
            if len(fields) != 4:
                error_message = (
                    f"Malformed harness trace at {path}:{line_number}: {line!r}"
                )
            else:
                try:
                    ordinal = int(fields[1])
                except ValueError:
                    error_message = f"Invalid trace ordinal at {path}:{line_number}"
                if not error_message and (
                    not fields[0] or not fields[2] or not fields[3]
                ):
                    error_message = (
                        f"Incomplete harness trace at {path}:{line_number}"
                    )

            if error_message:
                is_unterminated_tail = (
                    line_number == len(lines)
                    and not text.endswith(("\n", "\r"))
                )
                if is_unterminated_tail and attempt < TRACE_PARTIAL_LINE_RETRIES:
                    retry_partial_tail = True
                    break
                raise AcceptanceError(error_message)

            is_unterminated_tail = (
                line_number == len(lines) and not text.endswith(("\n", "\r"))
            )
            if is_unterminated_tail:
                if attempt < TRACE_PARTIAL_LINE_RETRIES:
                    retry_partial_tail = True
                    break
                raise AcceptanceError(
                    f"Unterminated harness trace at {path}:{line_number}"
                )
            entries.append(TraceEntry(fields[0], ordinal, fields[2], fields[3]))

        if not retry_partial_tail:
            return entries
        time.sleep(TRACE_PARTIAL_LINE_RETRY_SECONDS)

    raise AssertionError("unreachable")


def quarantine_matching_control(control_path: Path, scenario_id: str) -> Path | None:
    try:
        control = _read_json(control_path)
    except AcceptanceError:
        return None
    if control.get("scenario_id") != scenario_id:
        return None

    quarantine_path = control_path.with_name(
        f"control.failed-{uuid.uuid4().hex}.json"
    )
    try:
        os.replace(control_path, quarantine_path)
    except FileNotFoundError:
        return None
    return quarantine_path


def expected_trace(target_reloads: int, scenario_id: str) -> list[tuple[str, int]]:
    middle: list[tuple[str, int]] = []
    for ordinal in range(1, target_reloads + 1):
        middle.extend((('queued', ordinal), ('resumed', ordinal)))
    return [("leaf_before", 0), *middle, ("leaf_after", 0)]


def validate_trace(
    entries: list[TraceEntry], target_reloads: int, scenario_id: str
) -> list[str]:
    expected = expected_trace(target_reloads, scenario_id)
    actual = [(entry.kind, entry.ordinal) for entry in entries]
    if actual != expected:
        raise AcceptanceError(f"Harness trace mismatch: expected {expected}, got {actual}")
    if any(entry.scenario_id != scenario_id for entry in entries):
        raise AcceptanceError("Harness trace contains evidence from another scenario")

    generations = [entries[0].generation]
    cursor = 1
    for _ordinal in range(1, target_reloads + 1):
        queued = entries[cursor]
        resumed = entries[cursor + 1]
        if queued.generation != generations[-1]:
            raise AcceptanceError("Reload was queued by an unexpected domain generation")
        if resumed.generation == queued.generation:
            raise AcceptanceError("Harness resumed without changing domain generation")
        generations.append(resumed.generation)
        cursor += 2
    if entries[-1].generation != generations[-1]:
        raise AcceptanceError("Final leaf did not execute in the final domain generation")
    if len(set(generations)) != target_reloads + 1:
        raise AcceptanceError("A domain generation was reused across reload boundaries")
    return generations


async def _wait_for_trace_entry(
    path: Path,
    scenario_id: str,
    kind: str,
    ordinal: int,
    deadline: float,
    poll_interval: float,
) -> TraceEntry:
    while time.monotonic() < deadline:
        for entry in parse_trace(path):
            if entry.scenario_id != scenario_id:
                raise AcceptanceError("Harness trace identity changed during the run")
            if entry.kind == kind and entry.ordinal == ordinal:
                return entry
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    raise TimeoutError(f"Timed out waiting for harness event {kind}/{ordinal}")


async def _wait_for_worker_disconnect(
    project: Path, port: int, deadline: float, poll_interval: float
) -> None:
    while time.monotonic() < deadline:
        try:
            actual = Path(
                await durable.call(
                    port, "editor", {"action": "project_path"}, timeout=0.25
                )
            ).resolve()
            if actual != project.resolve():
                return
        except (
            OSError,
            ConnectionError,
            asyncio.TimeoutError,
            durable.RunnerError,
        ):
            return
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    raise TimeoutError(
        f"Worker endpoint on port {port} never disconnected for domain reload"
    )


async def _wait_for_replacement_port(
    project: Path,
    old_port: int,
    expected_port: int,
    deadline: float,
    poll_interval: float,
) -> int:
    if expected_port == old_port:
        raise AcceptanceError("Worker reload port plan reuses the current port")
    last_error = f"port {expected_port} is not advertised"
    while time.monotonic() < deadline:
        try:
            advertised = durable.advertised_ports(project, None)
            if expected_port in advertised:
                return await durable.verified_port(project, expected_port)
        except (
            OSError,
            ConnectionError,
            asyncio.TimeoutError,
            durable.RunnerError,
        ) as error:
            last_error = f"{type(error).__name__}: {error}"
        await asyncio.sleep(min(poll_interval, max(0.0, deadline - time.monotonic())))
    raise TimeoutError(
        f"Worker did not reconnect on planned port {expected_port} after closing "
        f"port {old_port}: {last_error}"
    )


async def monitor_reload_cycles(
    project: Path,
    initial_port: int,
    replacement_ports: tuple[int, ...],
    target_reloads: int,
    scenario_id: str,
    control_path: Path,
    trace_path: Path,
    deadline: float,
    poll_interval: float,
) -> list[int]:
    history = [initial_port]
    for ordinal in range(1, target_reloads + 1):
        await _wait_for_trace_entry(
            trace_path, scenario_id, "queued", ordinal, deadline, poll_interval
        )
        await _wait_for_worker_disconnect(
            project, history[-1], deadline, poll_interval
        )
        print(f"    reload {ordinal}: disconnected port={history[-1]}")
        replacement = await _wait_for_replacement_port(
            project,
            history[-1],
            replacement_ports[ordinal - 1],
            deadline,
            poll_interval,
        )
        if replacement == history[-1]:
            raise AcceptanceError("Reload reused the prior MCP port")
        history.append(replacement)
        print(f"    reload {ordinal}: reconnected port={replacement}")

        await _wait_for_trace_entry(
            trace_path, scenario_id, "resumed", ordinal, deadline, poll_interval
        )
        if ordinal == 1 and target_reloads == 2:
            control = _read_json(control_path)
            if control.get("scenario_id") != scenario_id:
                raise AcceptanceError("Acceptance control identity changed after reload")
            control["allow_second_reload"] = True
            _atomic_write_json(control_path, control)
    await _wait_for_trace_entry(
        trace_path, scenario_id, "leaf_after", 0, deadline, poll_interval
    )
    return history


def _iter_source_files(project: Path) -> list[Path]:
    roots = (
        project / FIXTURE_RELATIVE,
        project / "LocalPackages/unity-plugin",
        project / "LocalPackages/unity-plugin-reload",
    )
    files: list[Path] = []
    for root in roots:
        if not root.is_dir():
            raise AcceptanceError(f"Worker source root is missing: {root}")
        files.extend(path for path in root.rglob("*") if path.is_file())
    files.extend(
        (
            project / "Packages/manifest.json",
            project / "Packages/packages-lock.json",
        )
    )
    return sorted(set(files))


def capture_source_surface(project: Path) -> dict[str, SourceFileEvidence]:
    evidence: dict[str, SourceFileEvidence] = {}
    for path in _iter_source_files(project):
        try:
            stat = path.stat()
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
        except OSError as error:
            raise AcceptanceError(f"Cannot fingerprint worker source {path}: {error}") from error
        evidence[path.relative_to(project).as_posix()] = SourceFileEvidence(
            stat.st_size, stat.st_mtime_ns, digest
        )
    return evidence


def _read_jsonl(path: Path) -> list[dict[str, Any]]:
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except OSError as error:
        raise AcceptanceError(f"Cannot read JSONL evidence {path}: {error}") from error
    records: list[dict[str, Any]] = []
    for line_number, line in enumerate(lines, 1):
        if not line.strip():
            continue
        try:
            record = json.loads(line)
        except json.JSONDecodeError as error:
            raise AcceptanceError(f"Invalid JSONL at {path}:{line_number}") from error
        if not isinstance(record, dict):
            raise AcceptanceError(f"Non-object JSONL record at {path}:{line_number}")
        records.append(record)
    return records


def _require_counts(snapshot: dict[str, Any], names: tuple[str, ...], value: int) -> None:
    wrong = [f"{name}={snapshot.get(name)!r}" for name in names if snapshot.get(name) != value]
    if wrong:
        raise AcceptanceError("Reload count invariant failed: " + ", ".join(wrong))


def validate_snapshot(snapshot: dict[str, Any]) -> None:
    _require_counts(
        snapshot,
        (
            "expected_count",
            "declared_expected_count",
            "readable_manifest_count",
            "completed_expected_count",
            "unique_terminal_count",
            "started_attempt_count",
            "finished_attempt_count",
            "test_started_callback_count",
            "passed",
        ),
        3,
    )
    _require_counts(
        snapshot,
        (
            "unmaterialized_expected_count",
            "finish_without_start_count",
            "missing_count",
            "unexpected_count",
            "conflict_count",
            "failed",
            "skipped",
            "inconclusive",
            "cancelled",
            "invalid",
        ),
        0,
    )
    if snapshot.get("utf_xml_scope") != "partial":
        raise AcceptanceError(
            "A cross-generation run must retain exact leaves with partial root XML scope"
        )
    if not snapshot.get("build_fingerprint"):
        raise AcceptanceError("Run has no immutable build fingerprint")

    leaves = snapshot.get("leaves")
    if not isinstance(leaves, list) or len(leaves) != 3:
        raise AcceptanceError("Expected exactly three reconciled leaves")
    by_name: dict[str, dict[str, Any]] = {}
    for leaf in leaves:
        if not isinstance(leaf, dict):
            raise AcceptanceError("Reconciled leaf evidence is malformed")
        name = str(leaf.get("full_name") or "")
        if not name or name in by_name:
            raise AcceptanceError("Reconciled leaves contain a missing or duplicate identity")
        by_name[name] = leaf
    if set(by_name) != EXPECTED_LEAVES:
        raise AcceptanceError(
            f"Reconciled leaf set mismatch: {sorted(by_name)}"
        )
    for name, leaf in by_name.items():
        attempts = leaf.get("attempts")
        if (
            leaf.get("expected") is not True
            or leaf.get("outcome") != "passed"
            or leaf.get("attempt_count") != 1
            or not isinstance(attempts, list)
            or len(attempts) != 1
            or not isinstance(attempts[0], dict)
            or attempts[0].get("outcome") != "passed"
        ):
            raise AcceptanceError(f"Leaf {name} is not one exact passed attempt")


def validate_durable_evidence(
    project: Path,
    request_id: str,
    run_id: str,
    target_reloads: int,
) -> list[str]:
    run_dir = project / "Library/UnityMCP/TestRuns/runs" / run_id
    run = _read_json(run_dir / "run.json")
    if run.get("run_id") != run_id or run.get("request_id") != request_id:
        raise AcceptanceError("run.json identity does not match the dispatched request")
    if (
        run.get("utf_version") != UTF_VERSION
        or run.get("build_coherent") is not True
        or not run.get("build_fingerprint")
    ):
        raise AcceptanceError("run.json does not prove a coherent UTF 1.6.0 build")

    manifest = _read_jsonl(run_dir / "expected-tests.jsonl")
    if len(manifest) != 3 or any(entry.get("run_id") != run_id for entry in manifest):
        raise AcceptanceError("Manifest is partial or belongs to another run")
    manifest_names = [str(entry.get("full_name") or "") for entry in manifest]
    if len(set(manifest_names)) != 3 or set(manifest_names) != EXPECTED_LEAVES:
        raise AcceptanceError(f"Manifest leaf set mismatch: {manifest_names}")
    if any(
        entry.get("assembly_name") != "UnityMCP.Worker.DomainReloadHarness.dll"
        for entry in manifest
    ):
        raise AcceptanceError("Manifest contains a leaf from another assembly")

    events = _read_jsonl(run_dir / "events.jsonl")
    if not events or any(event.get("run_id") != run_id for event in events):
        raise AcceptanceError("Event journal is empty or contains another run_id")
    event_ids = [str(event.get("event_id") or "") for event in events]
    if "" in event_ids or len(set(event_ids)) != len(event_ids):
        raise AcceptanceError("Event journal contains a missing or duplicate event_id")

    event_types = [str(event.get("event_type") or "") for event in events]
    required_counts = {
        "run_started": 1,
        "manifest_sealed": 1,
        "test_started": 3,
        "test_finished": 3,
        "domain_reloading": target_reloads,
        "run_finished": 1,
        "run_finalized": 1,
    }
    wrong = {
        name: event_types.count(name)
        for name, expected in required_counts.items()
        if event_types.count(name) != expected
    }
    if wrong:
        raise AcceptanceError(f"Event cardinality mismatch: {wrong}")
    forbidden = {
        "infrastructure_error",
        "dispatch_failed",
        "cancel_requested",
        "abandoned",
        "cancelled",
    }
    present_forbidden = sorted(forbidden.intersection(event_types))
    if present_forbidden:
        raise AcceptanceError(f"Run contains failure events: {present_forbidden}")

    started = [event for event in events if event.get("event_type") == "test_started"]
    finished = [event for event in events if event.get("event_type") == "test_finished"]
    started_names = [str(event.get("full_name") or "") for event in started]
    finished_names = [str(event.get("full_name") or "") for event in finished]
    if (
        len(set(started_names)) != 3
        or set(started_names) != EXPECTED_LEAVES
        or len(set(finished_names)) != 3
        or set(finished_names) != EXPECTED_LEAVES
        or any(event.get("outcome") != "passed" for event in finished)
    ):
        raise AcceptanceError("Started/finished callbacks are duplicate or partial")

    execution_types = {
        "run_started",
        "test_started",
        "test_finished",
        "domain_reloading",
        "run_finished",
    }
    generations: list[str] = []
    for event in events:
        if event.get("event_type") not in execution_types:
            continue
        generation = str(event.get("observer_generation") or "")
        if not generation:
            raise AcceptanceError("Execution event has no observer generation")
        if generation not in generations:
            generations.append(generation)
    if len(generations) != target_reloads + 1:
        raise AcceptanceError(
            f"Expected {target_reloads + 1} observer generations, got {generations}"
        )
    reload_generations = [
        str(event.get("observer_generation") or "")
        for event in events
        if event.get("event_type") == "domain_reloading"
    ]
    if reload_generations != generations[:-1]:
        raise AcceptanceError(
            "domain_reloading events do not delimit each observer generation"
        )

    xml_path = run_dir / "utf-results.xml"
    try:
        root = ET.parse(xml_path).getroot()
    except (OSError, ET.ParseError) as error:
        raise AcceptanceError(f"Cannot parse UTF result XML {xml_path}: {error}") from error
    if root.tag != "test-run":
        raise AcceptanceError("UTF result XML has no NUnit test-run root")
    cases = list(root.iter("test-case"))
    xml_names = [case.attrib.get("fullname", "") for case in cases]
    if len(xml_names) != 3 or len(set(xml_names)) != 3 or set(xml_names) != EXPECTED_LEAVES:
        raise AcceptanceError(f"UTF XML leaf set mismatch: {xml_names}")
    if any(case.attrib.get("result") != "Passed" for case in cases):
        raise AcceptanceError("UTF XML contains a non-passed test case")
    return generations


def allocate_free_ports(count: int, excluded: set[int]) -> tuple[int, ...]:
    listeners: list[socket.socket] = []
    selected: list[int] = []
    try:
        while len(selected) < count:
            listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            listener.bind(("127.0.0.1", 0))
            port = int(listener.getsockname()[1])
            if port in excluded or port in selected:
                listener.close()
                continue
            listener.listen(1)
            listeners.append(listener)
            selected.append(port)
        return tuple(selected)
    finally:
        for listener in listeners:
            listener.close()


async def _run_scenario_once(
    project: Path,
    target_reloads: int,
    requested_port: int | None,
    timeout: float,
    poll_interval: float,
    scenario_id: str,
) -> dict[str, Any]:
    request_id = f"domain-reload-{target_reloads}-{uuid.uuid4().hex}"
    evidence_root = project / EVIDENCE_RELATIVE
    control_path = evidence_root / "control.json"
    trace_path = evidence_root / "harness-events.log"
    evidence_root.mkdir(parents=True, exist_ok=True)
    initial_port = await durable.verified_port(project, requested_port)
    allocated = allocate_free_ports(target_reloads * 2, {initial_port})
    replacement_ports = tuple(allocated[index * 2] for index in range(target_reloads))
    replacement_chat_ports = tuple(
        allocated[index * 2 + 1] for index in range(target_reloads)
    )
    _atomic_write_json(
        control_path,
        {
            "schema_version": CONTROL_SCHEMA,
            "scenario_id": scenario_id,
            "target_reloads": target_reloads,
            "allow_second_reload": target_reloads == 1,
            "reload_port_1": replacement_ports[0],
            "reload_chat_port_1": replacement_chat_ports[0],
            "reload_port_2": replacement_ports[1] if target_reloads == 2 else 0,
            "reload_chat_port_2": (
                replacement_chat_ports[1] if target_reloads == 2 else 0
            ),
        },
    )
    trace_path.write_text("", encoding="utf-8")

    before_sources = capture_source_surface(project)
    deadline = time.monotonic() + timeout
    command_args: dict[str, object] = {
        "mode": "EditMode",
        "filter": FIXTURE_NAME,
        "request_id": request_id,
    }
    print(f"  [{target_reloads} reload{'s' if target_reloads != 1 else ''}]")
    print(f"    request_id={request_id}")
    print(f"    initial_port={initial_port}")

    monitor = asyncio.create_task(
        monitor_reload_cycles(
            project,
            initial_port,
            replacement_ports,
            target_reloads,
            scenario_id,
            control_path,
            trace_path,
            deadline,
            poll_interval,
        )
    )
    terminal: asyncio.Task[dict[str, Any]] | None = None
    try:
        initial = ""
        try:
            initial = await durable.call(
                initial_port, "run_tests", command_args, timeout=30.0
            )
        except (
            OSError,
            ConnectionError,
            asyncio.TimeoutError,
            durable.TransportUncertain,
        ):
            print("    start ACK uncertain; resolving the original request_id")

        run_id = await durable.resolve_run_id(
            project,
            None,
            request_id,
            initial,
            deadline,
            poll_interval,
            command_args,
        )
        print(f"    run_id={run_id}")
        terminal = asyncio.create_task(
            durable.wait_for_terminal(
                project,
                None,
                request_id,
                run_id,
                deadline,
                poll_interval,
            )
        )
        port_history = await monitor
        snapshot = await terminal
    except BaseException:
        if not monitor.done():
            monitor.cancel()
        if terminal is not None and not terminal.done():
            terminal.cancel()
        await asyncio.gather(
            monitor,
            *(() if terminal is None else (terminal,)),
            return_exceptions=True,
        )
        raise

    after_sources = capture_source_surface(project)
    if after_sources != before_sources:
        changed = sorted(set(before_sources).symmetric_difference(after_sources))
        changed.extend(
            name
            for name in set(before_sources).intersection(after_sources)
            if before_sources[name] != after_sources[name]
        )
        raise AcceptanceError(
            "Worker source/package surface changed during the run: "
            + ", ".join(sorted(set(changed)))
        )

    durable.validate_terminal(
        snapshot,
        project=project,
        mode="EditMode",
        filter_name=FIXTURE_NAME,
        group="",
        expected_utf=UTF_VERSION,
        allow_empty=False,
    )
    validate_snapshot(snapshot)
    harness_generations = validate_trace(
        parse_trace(trace_path), target_reloads, scenario_id
    )
    observer_generations = validate_durable_evidence(
        project, request_id, run_id, target_reloads
    )
    if len(port_history) != target_reloads + 1 or any(
        left == right for left, right in zip(port_history, port_history[1:], strict=False)
    ):
        raise AcceptanceError(f"MCP port did not change at every reload: {port_history}")
    if tuple(port_history[1:]) != replacement_ports:
        raise AcceptanceError(
            f"MCP port history ignored the worker plan: {port_history}"
        )

    print(f"    ports={' -> '.join(map(str, port_history))}")
    print(f"    harness_generations={len(harness_generations)}")
    print(f"    observer_generations={len(observer_generations)}")
    print("    exact_leaves=3 attempts=3 outcome=passed")
    return snapshot


async def run_scenario(
    project: Path,
    target_reloads: int,
    requested_port: int | None,
    timeout: float,
    poll_interval: float,
) -> dict[str, Any]:
    scenario_id = f"reload-{target_reloads}-{uuid.uuid4().hex}"
    control_path = project / EVIDENCE_RELATIVE / "control.json"
    try:
        return await _run_scenario_once(
            project,
            target_reloads,
            requested_port,
            timeout,
            poll_interval,
            scenario_id,
        )
    except BaseException:
        quarantine_matching_control(control_path, scenario_id)
        raise


async def run(args: argparse.Namespace) -> None:
    project = args.project.resolve()
    if args.prepare_only:
        installed = install_worker_fixture(project)
        print(f"Prepared worker-only reload harness: {installed}")
        print("Launch this disposable worker with Unity 6000.0.65f1, then run again.")
        return

    validate_worker_project(project, require_lock=True)
    validate_installed_fixture(project)
    selected = (
        (1, 2)
        if args.scenario == "all"
        else (1,) if args.scenario == "one" else (2,)
    )
    print(f"worker={project}")
    print(f"unity={UNITY_VERSION}")
    print(f"utf={UTF_VERSION} source=builtin")
    requested_port = args.port
    for target_reloads in selected:
        await run_scenario(
            project,
            target_reloads,
            requested_port,
            args.timeout,
            args.poll_interval,
        )
        requested_port = None
    print("Domain reload acceptance passed.")


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--port", type=int, help="initial worker MCP port")
    parser.add_argument(
        "--scenario", choices=("all", "one", "two"), default="all"
    )
    parser.add_argument("--timeout", type=float, default=600.0)
    parser.add_argument("--poll-interval", type=float, default=0.1)
    parser.add_argument(
        "--prepare-only",
        action="store_true",
        help="install the fixture into a stopped disposable worker and exit",
    )
    parser.add_argument(
        "--confirm-disposable-worker",
        action="store_true",
        help="required acknowledgement that the target worker may be discarded",
    )
    args = parser.parse_args(argv)
    if not args.confirm_disposable_worker:
        parser.error("--confirm-disposable-worker is required")
    if args.timeout <= 0 or args.poll_interval <= 0:
        parser.error("timeouts must be positive")
    return args


def main() -> int:
    try:
        asyncio.run(run(parse_args()))
        return 0
    except TimeoutError as error:
        print(f"TIMEOUT: {error}", file=sys.stderr)
        return 2
    except (AcceptanceError, OSError, asyncio.TimeoutError) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("INTERRUPTED", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
