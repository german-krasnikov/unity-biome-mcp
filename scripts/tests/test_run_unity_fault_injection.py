import asyncio
import json
from pathlib import Path
import sys
import time

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))
import run_unity_fault_injection as lane


def snapshot_for(
    scenario: lane.Scenario,
    project: Path,
    *,
    canary: bool = False,
) -> dict[str, object]:
    filter_name = scenario.canary_filter if canary else scenario.fault_filter
    outcome = "passed" if canary else "failed"
    return {
        "request_id": "request-1",
        "run_id": "run-1",
        "state": "terminal",
        "lifecycle": "terminal",
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": True,
        "build_coherent": True,
        "utf_version": "1.6.0",
        "project_identity": str(project),
        "source": "mcp",
        "mode": "EditMode",
        "filter": filter_name,
        "expected_count": 1,
        "declared_expected_count": 1,
        "readable_manifest_count": 1,
        "completed_expected_count": 1,
        "unique_terminal_count": 1,
        "started_attempt_count": 1,
        "finished_attempt_count": 1,
        "unmaterialized_expected_count": 0,
        "finish_without_start_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "cancelled": 0,
        "invalid": 0,
        "utf_xml_scope": "complete",
        "issues": [],
        "outcome": outcome,
        "failed": 0 if canary else 1,
        "passed": 1 if canary else 0,
        "leaves": [
            {
                "full_name": filter_name,
                "outcome": outcome,
                "attempt_count": 1,
                "message": "" if canary else scenario.failure_marker,
            }
        ],
    }


def test_ack_requires_exact_request_and_durable_id() -> None:
    value = (
        "tests-started|request_id=request-1|run_id=run-1|"
        "utf_guid=utf-1|state=dispatched"
    )
    assert lane.correlated_run_id(value, "request-1") == "run-1"
    assert lane.correlated_run_id(value, "request-2") is None
    assert lane.correlated_run_id(
        "tests-started|request_id=request-1|run_id=run-1|state=dispatched",
        "request-1",
    ) is None


def test_correlated_dispatch_failure_is_terminal_error() -> None:
    value = (
        "test-request|request_id=request-1|run_id=run-1|state=terminal|"
        "outcome=dispatch_failed"
    )
    with pytest.raises(lane.FaultLaneError, match="rejected"):
        lane.correlated_run_id(value, "request-1")


def test_prepared_intent_is_resumed_once_with_identical_payload(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    calls: list[tuple[int, str, dict[str, object]]] = []
    resume_args: dict[str, object] = {
        "mode": "EditMode",
        "filter": "Fixture.Test",
        "request_id": "request-1",
    }

    async def fake_verified_port(
        project: Path,
        requested_port: int | None,
        timeout: float = 10.0,
    ) -> int:
        assert project == tmp_path
        assert requested_port is None
        assert timeout > 0
        return 9600

    async def fake_call(
        port: int,
        command: str,
        args: dict[str, object],
        timeout: float = 15.0,
    ) -> str:
        calls.append((port, command, dict(args)))
        return (
            "tests-started|request_id=request-1|run_id=run-1|"
            "utf_guid=utf-1|state=dispatched"
        )

    monkeypatch.setattr(lane, "verified_port", fake_verified_port)
    monkeypatch.setattr(lane, "call", fake_call)

    run_id = asyncio.run(
        lane.resolve_run_id(
            tmp_path,
            None,
            "request-1",
            "test-request|request_id=request-1|run_id=run-1|state=prepared",
            time.monotonic() + 1.0,
            0.001,
            resume_args,
        )
    )

    assert run_id == "run-1"
    assert calls == [(9600, "run_tests", resume_args)]


def test_dispatched_intent_is_never_resent(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    async def forbidden(*args, **kwargs):
        raise AssertionError("a dispatched request must never be sent again")

    monkeypatch.setattr(lane, "verified_port", forbidden)
    monkeypatch.setattr(lane, "call", forbidden)
    run_id = asyncio.run(
        lane.resolve_run_id(
            tmp_path,
            None,
            "request-1",
            "tests-started|request_id=request-1|run_id=run-1|"
            "utf_guid=utf-1|state=dispatched",
            time.monotonic() + 1.0,
            0.001,
            {"mode": "EditMode", "request_id": "request-1"},
        )
    )
    assert run_id == "run-1"


def test_explicit_port_is_candidate_during_advertisement_gap(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    ports = tmp_path / "ports"
    worker = tmp_path / "worker"
    ports.mkdir()
    worker.mkdir()
    monkeypatch.setattr(lane, "PORTS_DIR", ports)

    assert lane.connection_candidates(worker, 10900) == [10900]
    with pytest.raises(lane.FaultLaneError, match="No live MCP port file"):
        lane.connection_candidates(worker, None)


def test_start_waits_for_exact_project_before_single_dispatch(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    worker = tmp_path / "worker"
    other = tmp_path / "other"
    worker.mkdir()
    other.mkdir()
    commands: list[tuple[str, dict[str, object]]] = []
    project_probes = 0

    async def fake_call(
        _port: int,
        command: str,
        args: dict[str, object],
        timeout: float = 15.0,
    ) -> str:
        nonlocal project_probes
        assert timeout > 0
        commands.append((command, dict(args)))
        if command == "editor":
            project_probes += 1
            if project_probes == 1:
                raise ConnectionRefusedError("reload gap")
            if project_probes == 2:
                return str(other)
            return str(worker)
        if command == "run_tests":
            return (
                "tests-started|request_id=fault-fixed|run_id=run-fixed|"
                "utf_guid=utf-fixed|state=dispatched"
            )
        raise AssertionError(f"unexpected command {command}")

    async def fake_wait_for_terminal(*_args, **_kwargs) -> dict[str, object]:
        return {"terminal": True}

    monkeypatch.setattr(lane, "call", fake_call)
    monkeypatch.setattr(lane, "wait_for_terminal", fake_wait_for_terminal)
    monkeypatch.setattr(
        lane.uuid,
        "uuid4",
        lambda: type("Uuid", (), {"hex": "fixed"})(),
    )

    result = asyncio.run(
        lane.start_exact_run(worker, 10900, "Fixture.Test", 1.0, 0.001)
    )

    assert result == {"terminal": True}
    assert [command for command, _args in commands] == [
        "editor",
        "editor",
        "editor",
        "run_tests",
    ]
    assert commands[-1][1] == {
        "mode": "EditMode",
        "filter": "Fixture.Test",
        "request_id": "fault-fixed",
    }


def test_uncertain_ack_resolves_same_request_without_redispatch(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    dispatches: list[dict[str, object]] = []
    resolved: list[tuple[str, str, dict[str, object]]] = []

    async def fake_wait_for_verified_port(*_args, **_kwargs) -> int:
        return 10900

    async def fake_call(
        _port: int,
        command: str,
        args: dict[str, object],
        timeout: float = 15.0,
    ) -> str:
        assert command == "run_tests"
        assert timeout == 30.0
        dispatches.append(dict(args))
        raise lane.TransportUncertain("reload before ACK")

    async def fake_resolve_run_id(
        _project: Path,
        _requested_port: int | None,
        request_id: str,
        initial: str,
        _deadline: float,
        _poll_interval: float,
        resume_args: dict[str, object],
    ) -> str:
        resolved.append((request_id, initial, dict(resume_args)))
        return "run-fixed"

    async def fake_wait_for_terminal(*_args, **_kwargs) -> dict[str, object]:
        return {"terminal": True}

    monkeypatch.setattr(lane, "wait_for_verified_port", fake_wait_for_verified_port)
    monkeypatch.setattr(lane, "call", fake_call)
    monkeypatch.setattr(lane, "resolve_run_id", fake_resolve_run_id)
    monkeypatch.setattr(lane, "wait_for_terminal", fake_wait_for_terminal)
    monkeypatch.setattr(
        lane.uuid,
        "uuid4",
        lambda: type("Uuid", (), {"hex": "fixed"})(),
    )

    result = asyncio.run(
        lane.start_exact_run(tmp_path, 10900, "Fixture.Test", 1.0, 0.001)
    )

    expected_args = {
        "mode": "EditMode",
        "filter": "Fixture.Test",
        "request_id": "fault-fixed",
    }
    assert result == {"terminal": True}
    assert dispatches == [expected_args]
    assert resolved == [("fault-fixed", "", expected_args)]


@pytest.mark.parametrize("name", tuple(lane.SCENARIOS))
def test_expected_fault_and_canary_snapshots_are_accepted(
    name: str, tmp_path: Path
) -> None:
    scenario = lane.SCENARIOS[name]
    lane.validate_fault(snapshot_for(scenario, tmp_path), scenario, tmp_path)
    lane.validate_canary(
        snapshot_for(scenario, tmp_path, canary=True), scenario, tmp_path
    )


@pytest.mark.parametrize(
    ("mutation", "message"),
    (
        (lambda value: value.update(outcome="invalid", invalid=1), "invalid evidence"),
        (lambda value: value.update(cleanup_complete=False), "cleanup_complete"),
        (
            lambda value: value.update(
                issues=[{"severity": "error", "code": "INFRASTRUCTURE_ERROR"}]
            ),
            "RunError",
        ),
        (lambda value: value.update(utf_version="1.7.2"), "Expected UTF"),
    ),
)
def test_fault_validation_fails_closed(
    mutation, message: str, tmp_path: Path
) -> None:
    scenario = lane.SCENARIOS["sync"]
    value = snapshot_for(scenario, tmp_path)
    mutation(value)
    with pytest.raises(lane.FaultLaneError, match=message):
        lane.validate_fault(value, scenario, tmp_path)


def test_worker_validation_requires_exact_utf_1_6(tmp_path: Path) -> None:
    (tmp_path / "Assets").mkdir()
    (tmp_path / "Packages").mkdir()
    manifest = tmp_path / "Packages" / "manifest.json"
    manifest.write_text(
        json.dumps(
            {"dependencies": {"com.unity.test-framework": "1.8.0"}}
        ),
        encoding="utf-8",
    )
    with pytest.raises(lane.FaultLaneError, match="pin UTF 1.6.0"):
        lane.validate_worker_project(tmp_path)

    manifest.write_text(
        json.dumps(
            {"dependencies": {"com.unity.test-framework": "1.6.0"}}
        ),
        encoding="utf-8",
    )
    lane.validate_worker_project(tmp_path)


def test_port_discovery_is_correlated_to_worker_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    ports = tmp_path / "ports"
    worker = tmp_path / "worker"
    other = tmp_path / "other"
    ports.mkdir()
    worker.mkdir()
    other.mkdir()
    (ports / "10.port").write_text(f"9601\n{other}\nother\n", encoding="utf-8")
    (ports / "11.port").write_text(f"9602\n{worker}\nworker\n", encoding="utf-8")
    monkeypatch.setattr(lane, "PORTS_DIR", ports)

    assert lane.discover_port(worker, None) == 9602
    with pytest.raises(lane.FaultLaneError, match="not advertised"):
        lane.discover_port(worker, 9601)
