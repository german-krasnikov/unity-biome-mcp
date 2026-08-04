import asyncio
import json
import os
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))
import run_unity_domain_reload_acceptance as lane


def trace_entries(target_reloads: int, scenario_id: str) -> list[lane.TraceEntry]:
    values = [lane.TraceEntry("leaf_before", 0, "generation-0", scenario_id)]
    for ordinal in range(1, target_reloads + 1):
        previous = f"generation-{ordinal - 1}"
        current = f"generation-{ordinal}"
        values.append(lane.TraceEntry("queued", ordinal, previous, scenario_id))
        values.append(lane.TraceEntry("resumed", ordinal, current, scenario_id))
    values.append(
        lane.TraceEntry("leaf_after", 0, f"generation-{target_reloads}", scenario_id)
    )
    return values


def passing_snapshot() -> dict[str, object]:
    return {
        "expected_count": 3,
        "declared_expected_count": 3,
        "readable_manifest_count": 3,
        "completed_expected_count": 3,
        "unique_terminal_count": 3,
        "started_attempt_count": 3,
        "finished_attempt_count": 3,
        "test_started_callback_count": 3,
        "passed": 3,
        "unmaterialized_expected_count": 0,
        "finish_without_start_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "failed": 0,
        "skipped": 0,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "utf_xml_scope": "partial",
        "build_fingerprint": "mvid=one;utf=1.6.0",
        "leaves": [
            {
                "full_name": name,
                "expected": True,
                "outcome": "passed",
                "attempt_count": 1,
                "attempts": [{"attempt": 1, "outcome": "passed"}],
            }
            for name in sorted(lane.EXPECTED_LEAVES)
        ],
    }


def make_worker(project: Path, *, lock_source: str = "builtin") -> None:
    for name in ("Assets", "Packages", "ProjectSettings"):
        (project / name).mkdir(parents=True, exist_ok=True)
    (project / "LocalPackages/unity-plugin/Editor").mkdir(parents=True)
    (project / "LocalPackages/unity-plugin-reload/Editor").mkdir(parents=True)
    (project / "LocalPackages/unity-plugin/Editor/Main.cs").write_text(
        "internal class Main {}\n", encoding="utf-8"
    )
    (project / "LocalPackages/unity-plugin-reload/Editor/Reload.cs").write_text(
        "internal class Reload {}\n", encoding="utf-8"
    )
    (project / "ProjectSettings/ProjectVersion.txt").write_text(
        "m_EditorVersion: 6000.0.65f1\n"
        "m_EditorVersionWithRevision: 6000.0.65f1 (a18e2220bd50)\n",
        encoding="utf-8",
    )
    (project / "Packages/manifest.json").write_text(
        json.dumps(
            {"dependencies": {"com.unity.test-framework": lane.UTF_VERSION}}
        ),
        encoding="utf-8",
    )
    (project / "Packages/packages-lock.json").write_text(
        json.dumps(
            {
                "dependencies": {
                    "com.unity.test-framework": {
                        "version": lane.UTF_VERSION,
                        "source": lock_source,
                    }
                }
            }
        ),
        encoding="utf-8",
    )
    marker = project / "Library/UnityMCP/disposable-worker.json"
    marker.parent.mkdir(parents=True)
    marker.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "disposable": True,
                "unity_version": lane.UNITY_VERSION,
                "unity_revision": lane.UNITY_REVISION,
                "utf_version": lane.UTF_VERSION,
            }
        ),
        encoding="utf-8",
    )


@pytest.mark.parametrize("target_reloads", (1, 2))
def test_trace_requires_exact_generation_change(target_reloads: int) -> None:
    scenario_id = "scenario-1"
    generations = lane.validate_trace(
        trace_entries(target_reloads, scenario_id), target_reloads, scenario_id
    )
    assert generations == [
        f"generation-{ordinal}" for ordinal in range(target_reloads + 1)
    ]


def test_trace_rejects_reload_without_new_generation() -> None:
    entries = trace_entries(1, "scenario-1")
    entries[2] = lane.TraceEntry("resumed", 1, "generation-0", "scenario-1")
    entries[3] = lane.TraceEntry("leaf_after", 0, "generation-0", "scenario-1")
    with pytest.raises(lane.AcceptanceError, match="without changing"):
        lane.validate_trace(entries, 1, "scenario-1")


def test_trace_rejects_duplicate_or_partial_sequence() -> None:
    entries = trace_entries(2, "scenario-1")
    entries.insert(3, entries[2])
    with pytest.raises(lane.AcceptanceError, match="trace mismatch"):
        lane.validate_trace(entries, 2, "scenario-1")


def test_trace_retries_partially_written_final_line(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    trace = tmp_path / "harness-events.log"
    trace.write_text(
        "leaf_before|0|generation-0|scenario-1\n"
        "queued|1|generation-0|scen",
        encoding="utf-8",
    )
    sleeps = 0

    def finish_concurrent_append(_seconds: float) -> None:
        nonlocal sleeps
        sleeps += 1
        with trace.open("a", encoding="utf-8") as stream:
            stream.write("ario-1\n")

    monkeypatch.setattr(lane.time, "sleep", finish_concurrent_append)

    entries = lane.parse_trace(trace)

    assert sleeps == 1
    assert [(entry.kind, entry.ordinal) for entry in entries] == [
        ("leaf_before", 0),
        ("queued", 1),
    ]


def test_trace_rejects_permanently_corrupt_final_line(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    trace = tmp_path / "harness-events.log"
    trace.write_text("leaf_before|not-an-ordinal", encoding="utf-8")
    monkeypatch.setattr(lane.time, "sleep", lambda _seconds: None)

    with pytest.raises(lane.AcceptanceError, match="Malformed harness trace"):
        lane.parse_trace(trace)


def test_snapshot_accepts_three_exact_single_attempt_leaves() -> None:
    lane.validate_snapshot(passing_snapshot())


@pytest.mark.parametrize(
    ("field", "value", "message"),
    (
        ("completed_expected_count", 2, "count invariant"),
        ("finish_without_start_count", 1, "count invariant"),
        ("utf_xml_scope", "complete", "partial root XML"),
    ),
)
def test_snapshot_fails_closed(field: str, value: object, message: str) -> None:
    snapshot = passing_snapshot()
    snapshot[field] = value
    with pytest.raises(lane.AcceptanceError, match=message):
        lane.validate_snapshot(snapshot)


def write_durable_evidence(
    project: Path, *, target_reloads: int = 2, duplicate_finish: bool = False
) -> None:
    run_id = "run-1"
    run_dir = project / "Library/UnityMCP/TestRuns/runs" / run_id
    run_dir.mkdir(parents=True)
    (run_dir / "run.json").write_text(
        json.dumps(
            {
                "run_id": run_id,
                "request_id": "request-1",
                "utf_version": lane.UTF_VERSION,
                "build_coherent": True,
                "build_fingerprint": "mvid=one;utf=1.6.0",
            }
        ),
        encoding="utf-8",
    )
    with (run_dir / "expected-tests.jsonl").open("w", encoding="utf-8") as stream:
        for name in sorted(lane.EXPECTED_LEAVES):
            stream.write(
                json.dumps(
                    {
                        "run_id": run_id,
                        "full_name": name,
                        "assembly_name": "UnityMCP.Worker.DomainReloadHarness.dll",
                    }
                )
                + "\n"
            )

    leaves = sorted(lane.EXPECTED_LEAVES)
    events: list[dict[str, object]] = []

    def event(event_type: str, generation: int, **extra: object) -> None:
        events.append(
            {
                "run_id": run_id,
                "event_id": f"event-{len(events)}",
                "event_type": event_type,
                "observer_generation": f"observer-{generation}",
                **extra,
            }
        )

    event("run_started", 0)
    event("manifest_sealed", 0)
    for index, name in enumerate(leaves):
        generation = min(index, target_reloads)
        event("test_started", generation, full_name=name)
        event("test_finished", generation, full_name=name, outcome="passed")
        if duplicate_finish and index == 0:
            event("test_finished", generation, full_name=name, outcome="passed")
        if index < target_reloads:
            event("domain_reloading", generation)
    event("run_finished", target_reloads)
    event("run_finalized", target_reloads)
    with (run_dir / "events.jsonl").open("w", encoding="utf-8") as stream:
        for value in events:
            stream.write(json.dumps(value) + "\n")

    cases = "".join(
        f'<test-case fullname="{name}" result="Passed" />'
        for name in leaves
    )
    (run_dir / "utf-results.xml").write_text(
        f'<test-run result="Passed">{cases}</test-run>', encoding="utf-8"
    )


def test_durable_evidence_requires_one_run_and_exact_leaf_set(tmp_path: Path) -> None:
    write_durable_evidence(tmp_path)
    generations = lane.validate_durable_evidence(
        tmp_path, "request-1", "run-1", 2
    )
    assert generations == ["observer-0", "observer-1", "observer-2"]


def test_durable_evidence_rejects_duplicate_finished_callback(tmp_path: Path) -> None:
    write_durable_evidence(tmp_path, duplicate_finish=True)
    with pytest.raises(lane.AcceptanceError, match="Event cardinality"):
        lane.validate_durable_evidence(tmp_path, "request-1", "run-1", 2)


def test_worker_validation_and_fixture_install_are_idempotent(tmp_path: Path) -> None:
    project = tmp_path / "worker"
    make_worker(project)
    lane.validate_worker_project(project, require_lock=True)
    target = lane.install_worker_fixture(project)
    assert target == project / lane.FIXTURE_RELATIVE
    lane.install_worker_fixture(project)
    lane.validate_installed_fixture(project)


def test_worker_rejects_non_builtin_utf(tmp_path: Path) -> None:
    project = tmp_path / "worker"
    make_worker(project, lock_source="registry")
    with pytest.raises(lane.AcceptanceError, match="builtin source"):
        lane.validate_worker_project(project, require_lock=True)


def test_source_surface_detects_timestamp_only_mutation(tmp_path: Path) -> None:
    project = tmp_path / "worker"
    make_worker(project)
    lane.install_worker_fixture(project)
    before = lane.capture_source_surface(project)
    source = project / "LocalPackages/unity-plugin/Editor/Main.cs"
    stat = source.stat()
    os.utime(source, ns=(stat.st_atime_ns, stat.st_mtime_ns + 1_000_000))
    assert lane.capture_source_surface(project) != before


def test_reload_port_plan_is_distinct() -> None:
    ports = lane.allocate_free_ports(4, {9600, 10600})
    assert len(ports) == 4
    assert len(set(ports)) == 4
    assert not set(ports).intersection({9600, 10600})


@pytest.mark.parametrize(
    "failure",
    (
        TimeoutError("controller timeout"),
        lane.AcceptanceError("controller failure"),
        RuntimeError("unexpected controller exception"),
    ),
)
def test_scenario_failure_quarantines_only_matching_active_control(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    failure: BaseException,
) -> None:
    project = tmp_path / "worker"
    control_path = project / lane.EVIDENCE_RELATIVE / "control.json"

    async def fail_after_control_created(
        _project: Path,
        _target_reloads: int,
        _requested_port: int | None,
        _timeout: float,
        _poll_interval: float,
        scenario_id: str,
    ) -> dict[str, object]:
        lane._atomic_write_json(
            control_path,
            {
                "schema_version": lane.CONTROL_SCHEMA,
                "scenario_id": scenario_id,
                "target_reloads": 1,
            },
        )
        raise failure

    monkeypatch.setattr(lane, "_run_scenario_once", fail_after_control_created)

    with pytest.raises(type(failure), match=str(failure)):
        asyncio.run(lane.run_scenario(project, 1, None, 1.0, 0.01))

    assert not control_path.exists()
    quarantined = list(control_path.parent.glob("control.failed-*.json"))
    assert len(quarantined) == 1
    assert json.loads(quarantined[0].read_text(encoding="utf-8"))[
        "scenario_id"
    ].startswith("reload-1-")


def test_control_cleanup_preserves_another_scenario(tmp_path: Path) -> None:
    control_path = tmp_path / "control.json"
    lane._atomic_write_json(control_path, {"scenario_id": "new-scenario"})

    assert lane.quarantine_matching_control(control_path, "failed-scenario") is None
    assert json.loads(control_path.read_text(encoding="utf-8")) == {
        "scenario_id": "new-scenario"
    }


def test_worker_harness_contains_no_coroutine_test_api() -> None:
    source = (lane.FIXTURE_SOURCE / "DomainReloadHarness.cs").read_text(
        encoding="utf-8"
    )
    for forbidden in (
        "UnityTest",
        "IEnumerator",
        "WaitForDomainReload",
        "Thread.Sleep",
    ):
        assert forbidden not in source
    assert source.count("public async Task Leaf") == 3


def test_worker_harness_only_reloads_after_passed_boundary_in_expected_phase() -> None:
    source = (lane.FIXTURE_SOURCE / "DomainReloadHarness.cs").read_text(
        encoding="utf-8"
    )
    assert "result.TestStatus != TestStatus.Passed" in source
    assert "AcceptanceFiles.IsBoundaryReady(control, ordinal)" in source
    assert "AcceptanceFiles.ArchiveControl(control)" in source
    assert "AcceptanceFiles.QuarantineControl(control)" in source
    assert 'GetMethod("SaveRuntimePorts", flags)' in source
    for forbidden in (
        'GetField("_port"',
        'GetField("_chatPort"',
        'GetField("_portsResolved"',
        "MCPServer.SavePorts",
    ):
        assert forbidden not in source
