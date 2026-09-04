import asyncio
import importlib.util
import json
import struct
from pathlib import Path

import pytest

MODULE_PATH = Path(__file__).resolve().parents[2] / "run_unity_tests.py"
SPEC = importlib.util.spec_from_file_location("standalone_unity_tests", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class FakeSocket:
    def __init__(self, response: dict[str, object]) -> None:
        payload = json.dumps(response, separators=(",", ":")).encode("utf-8")
        self._wire = bytearray(struct.pack("!I", len(payload)) + payload)

    def __enter__(self):
        return self

    def __exit__(self, *_args) -> None:
        return None

    def settimeout(self, _timeout: float) -> None:
        return None

    def sendall(self, _payload: bytes) -> None:
        return None

    def recv(self, count: int) -> bytes:
        chunk = bytes(self._wire[:count])
        del self._wire[:count]
        return chunk


def passing_snapshot(project: Path) -> dict[str, object]:
    return {
        "request_id": "request-1",
        "run_id": "run-1",
        "utf_guid": "utf-1",
        "state": "terminal",
        "lifecycle": "terminal",
        "outcome": "passed",
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
        "filter": "Fixture.Test",
        "group": "",
        "expected_count": 2,
        "declared_expected_count": 2,
        "readable_manifest_count": 2,
        "completed_expected_count": 2,
        "unique_terminal_count": 2,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": 1,
        "failed": 0,
        "skipped": 1,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "utf_xml_scope": "partial",
        "issues": [],
    }


def validate(snapshot: dict[str, object], project: Path) -> None:
    runner.validate_terminal(
        snapshot,
        project=project,
        mode="EditMode",
        filter_name="Fixture.Test",
        group="",
        expected_utf="1.6.0",
        allow_empty=False,
    )


def test_start_record_requires_exact_request_run_and_utf_guid() -> None:
    value = (
        "tests-started|request_id=request-1|run_id=run-1|"
        "utf_guid=utf-1|state=dispatched"
    )
    assert runner.correlated_run_id(value, "request-1") == "run-1"
    assert runner.correlated_run_id(value, "request-2") is None
    assert runner.correlated_run_id(
        "tests-started|request_id=request-1|run_id=run-1|state=dispatched",
        "request-1",
    ) is None


def test_request_status_can_resolve_lost_ack() -> None:
    value = "test-request|request_id=request-1|run_id=run-1|state=prepared"
    assert runner.correlated_run_id(value, "request-1") == "run-1"
    status = runner.correlated_status(value, "request-1")
    assert status is not None
    assert runner.is_recoverable_prepared(status)


def test_dispatched_status_is_not_recoverable_prepared() -> None:
    value = "test-request|request_id=request-1|run_id=run-1|state=dispatched"
    status = runner.correlated_status(value, "request-1")
    assert status is not None
    assert not runner.is_recoverable_prepared(status)


def test_port_discovery_is_project_correlated(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    ports = tmp_path / "ports"
    project = tmp_path / "worker"
    other = tmp_path / "other"
    ports.mkdir()
    project.mkdir()
    other.mkdir()
    (ports / "one.port").write_text(f"9501\n{other}\n", encoding="utf-8")
    (ports / "two.port").write_text(f"9502\n{project}\n", encoding="utf-8")
    monkeypatch.setattr(runner, "PORTS_DIR", ports)

    assert runner.advertised_ports(project, None) == [9502]
    with pytest.raises(runner.RunnerError, match="not advertised"):
        runner.advertised_ports(project, 9501)


def test_call_sync_reads_file_backed_text_response(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    output_root = tmp_path / "Temp" / "MCP"
    output_root.mkdir(parents=True)
    snapshot_path = output_root / "output.json"
    snapshot_path.write_text('{"state":"terminal"}', encoding="utf-8")
    fake = FakeSocket(
        {
            "id": "standalone-tests-fixed",
            "ok": True,
            "file": str(snapshot_path),
        }
    )
    monkeypatch.setattr(
        runner.uuid,
        "uuid4",
        lambda: type("Uuid", (), {"hex": "fixed"})(),
    )
    monkeypatch.setattr(
        runner.socket,
        "create_connection",
        lambda *_args, **_kwargs: fake,
    )

    assert runner._call_sync(
        10600,
        "get_test_run",
        {"run_id": "run-1"},
        1.0,
        [output_root],
    ) == '{"state":"terminal"}'


def test_call_sync_classifies_structured_busy_as_retryable(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = FakeSocket(
        {
            "id": "standalone-tests-fixed",
            "ok": False,
            "err": "Server initializing. Retry in 2s.",
            "retry": 2000,
        }
    )
    monkeypatch.setattr(
        runner.uuid,
        "uuid4",
        lambda: type("Uuid", (), {"hex": "fixed"})(),
    )
    monkeypatch.setattr(
        runner.socket,
        "create_connection",
        lambda *_args, **_kwargs: fake,
    )

    with pytest.raises(runner.RetryableServerState) as raised:
        runner._call_sync(10600, "editor", {"action": "project_path"}, 1.0)

    assert raised.value.retry_seconds == 2.0


def test_call_sync_keeps_ordinary_command_error_fatal(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = FakeSocket(
        {
            "id": "standalone-tests-fixed",
            "ok": False,
            "err": "invalid request",
        }
    )
    monkeypatch.setattr(
        runner.uuid,
        "uuid4",
        lambda: type("Uuid", (), {"hex": "fixed"})(),
    )
    monkeypatch.setattr(
        runner.socket,
        "create_connection",
        lambda *_args, **_kwargs: fake,
    )

    with pytest.raises(runner.RunnerError) as raised:
        runner._call_sync(10600, "run_tests", {}, 1.0)

    assert type(raised.value) is runner.RunnerError


def test_file_backed_response_cannot_escape_project_temp(tmp_path: Path) -> None:
    output_root = tmp_path / "Temp" / "MCP"
    output_root.mkdir(parents=True)
    outside = tmp_path / "secret.txt"
    outside.write_text("secret", encoding="utf-8")

    with pytest.raises(runner.RunnerError, match="outside"):
        runner._read_file_backed_text(
            str(outside),
            command="get_test_run",
            response_file_roots=[output_root],
        )


def test_read_file_backed_allows_screenshots_dir(tmp_path: Path) -> None:
    screenshots = tmp_path / "ScreenShots"
    screenshots.mkdir()
    txt = screenshots / "frame.txt"
    txt.write_text("screenshot data", encoding="utf-8")

    result = runner._read_file_backed_text(
        str(txt),
        command="screenshot",
        response_file_roots=[screenshots],
    )
    assert result == "screenshot data"


def test_read_file_backed_rejects_outside_all_roots(tmp_path: Path) -> None:
    root1 = tmp_path / "Temp" / "MCP"
    root1.mkdir(parents=True)
    root2 = tmp_path / "ScreenShots"
    root2.mkdir()
    outside = tmp_path / "secret.txt"
    outside.write_text("secret", encoding="utf-8")

    with pytest.raises(runner.RunnerError, match="outside"):
        runner._read_file_backed_text(
            str(outside),
            command="screenshot",
            response_file_roots=[root1, root2],
        )


def test_rediscovered_text_call_allows_only_project_temp(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    project = tmp_path / "worker"
    expected_root = project / "Temp" / "MCP"

    async def fake_call(
        _port: int,
        command: str,
        _args: dict[str, object],
        timeout: float = 15.0,
        response_file_roots: list[Path] | None = None,
    ) -> str:
        if command == "editor":
            assert timeout == 10.0
            assert response_file_roots is None
            return str(project)
        assert command == "get_test_run"
        assert response_file_roots == [expected_root, project / "ScreenShots"]
        return '{"state":"terminal"}'

    monkeypatch.setattr(runner, "advertised_ports", lambda *_args: [10600])
    monkeypatch.setattr(runner, "call", fake_call)

    result = asyncio.run(
        runner.read_with_rediscovery(
            project,
            10600,
            "get_test_run",
            {"run_id": "run-1"},
        )
    )
    assert result == '{"state":"terminal"}'


def test_rediscovery_prefers_new_advertised_port_over_explicit_old_port(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    project = tmp_path / "worker"
    calls = []

    monkeypatch.setattr(runner, "advertised_ports", lambda *_args: [10700])

    async def fake_call(
        port: int,
        command: str,
        _args: dict[str, object],
        timeout: float = 15.0,
        response_file_roots: list[Path] | None = None,
    ) -> str:
        calls.append((port, command))
        if command == "editor":
            return str(project)
        assert response_file_roots == [project / "Temp" / "MCP", project / "ScreenShots"]
        return "terminal"

    monkeypatch.setattr(runner, "call", fake_call)

    result = asyncio.run(
        runner.read_with_rediscovery(project, 10600, "get_test_run", {"run_id": "run-1"})
    )

    assert result == "terminal"
    assert calls == [(10700, "editor"), (10700, "get_test_run")]


def test_terminal_poll_reconfirms_same_request_after_transient(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    snapshot = passing_snapshot(tmp_path)
    calls = []

    async def fake_read(_project, _port, command, _args):
        calls.append(command)
        if calls == ["get_test_run"]:
            raise runner.TransportUncertain("reload")
        if command == "resolve_test_request":
            return (
                "test-request|request_id=request-1|run_id=run-1|"
                "state=running"
            )
        return json.dumps(snapshot)

    monkeypatch.setattr(runner, "read_with_rediscovery", fake_read)

    result = asyncio.run(
        runner.wait_for_terminal(
            tmp_path,
            10600,
            "request-1",
            "run-1",
            runner.time.monotonic() + 1.0,
            0.001,
        )
    )

    assert result == snapshot
    assert calls == ["get_test_run", "resolve_test_request", "get_test_run"]
    assert "run_tests" not in calls


def test_no_tests_matched_is_terminal(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """A get_test_run snapshot whose outcome is no_tests_matched must
    terminate the poll loop immediately even though state never reaches
    'terminal' -- otherwise the caller spins to its deadline for nothing
    instead of failing fast (validate_terminal then rejects it cleanly:
    'state and lifecycle must both be terminal').

    Double-red: red today (outcome not recognized as an early-exit signal,
    loop spins to the deadline and raises TimeoutError instead of
    returning), red again if the outcome set is emptied.
    """
    snapshot = {
        "request_id": "request-1",
        "run_id": "run-1",
        "state": "running",
        "outcome": "no_tests_matched",
    }

    async def fake_read(_project, _port, _command, _args):
        return json.dumps(snapshot)

    monkeypatch.setattr(runner, "read_with_rediscovery", fake_read)

    result = asyncio.run(
        runner.wait_for_terminal(
            tmp_path, 10600, "request-1", "run-1",
            runner.time.monotonic() + 0.05, 0.001,
        )
    )
    assert result == snapshot


def test_dirty_scene_blocked_is_terminal(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """Same as test_no_tests_matched_is_terminal for dirty_scene_blocked."""
    snapshot = {
        "request_id": "request-1",
        "run_id": "run-1",
        "state": "running",
        "outcome": "dirty_scene_blocked",
    }

    async def fake_read(_project, _port, _command, _args):
        return json.dumps(snapshot)

    monkeypatch.setattr(runner, "read_with_rediscovery", fake_read)

    result = asyncio.run(
        runner.wait_for_terminal(
            tmp_path, 10600, "request-1", "run-1",
            runner.time.monotonic() + 0.05, 0.001,
        )
    )
    assert result == snapshot


def test_poll_interval_default_is_fast() -> None:
    """Default --poll-interval must be the fast named constant, not the old
    5.0s default that made every wait needlessly sluggish.

    Double-red: red if the default reverts to 5.0, red if the named
    constant is removed/renamed (AttributeError)."""
    args = runner.parse_args(["EditMode"])
    assert args.poll_interval == runner.DEFAULT_POLL_INTERVAL_S
    assert runner.DEFAULT_POLL_INTERVAL_S == 1.0


def test_terminal_poll_rejects_changed_run_id_during_recovery(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    calls = []

    async def fake_read(_project, _port, command, _args):
        calls.append(command)
        if command == "get_test_run":
            raise runner.TransportUncertain("reload")
        return "test-request|request_id=request-1|run_id=run-2|state=running"

    monkeypatch.setattr(runner, "read_with_rediscovery", fake_read)

    with pytest.raises(runner.RunnerError, match="changed run_id"):
        asyncio.run(
            runner.wait_for_terminal(
                tmp_path,
                10600,
                "request-1",
                "run-1",
                runner.time.monotonic() + 1.0,
                0.001,
            )
        )

    assert calls == ["get_test_run", "resolve_test_request"]


def test_terminal_validation_accepts_exact_reload_partial_xml(tmp_path: Path) -> None:
    validate(passing_snapshot(tmp_path), tmp_path)


@pytest.mark.parametrize(
    ("mutation", "message"),
    (
        (lambda value: value.update(cleanup_complete=False), "cleanup_complete"),
        (lambda value: value.update(completed_expected_count=1), "count invariant"),
        (lambda value: value.update(missing_count=1), "count invariant"),
        (
            lambda value: value.update(
                issues=[{"severity": "error", "code": "INFRASTRUCTURE_ERROR"}]
            ),
            "infrastructure errors",
        ),
        (lambda value: value.update(outcome="incomplete"), "outcome=incomplete"),
    ),
)
def test_terminal_validation_fails_closed(
    mutation, message: str, tmp_path: Path
) -> None:
    snapshot = passing_snapshot(tmp_path)
    mutation(snapshot)
    with pytest.raises(runner.RunnerError, match=message):
        validate(snapshot, tmp_path)


def test_zero_discovery_requires_explicit_override(tmp_path: Path) -> None:
    snapshot = passing_snapshot(tmp_path)
    for name in (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "passed",
        "skipped",
    ):
        snapshot[name] = 0

    with pytest.raises(runner.RunnerError, match="expected_count"):
        validate(snapshot, tmp_path)

    runner.validate_terminal(
        snapshot,
        project=tmp_path,
        mode="EditMode",
        filter_name="Fixture.Test",
        group="",
        expected_utf="1.6.0",
        allow_empty=True,
    )


def test_unfiltered_editmode_requires_more_than_six_thousand_tests() -> None:
    assert runner.required_minimum_tests(
        "EditMode", "", "", None, False
    ) == 6001
    assert runner.required_minimum_tests(
        "EditMode", "Fixture.Test", "", None, False
    ) == 1
    assert runner.required_minimum_tests(
        "PlayMode", "", "", None, False
    ) == 1


def test_partial_full_editmode_catalog_cannot_be_accepted(tmp_path: Path) -> None:
    snapshot = passing_snapshot(tmp_path)
    snapshot["filter"] = ""
    for name in (
        "expected_count",
        "declared_expected_count",
        "readable_manifest_count",
        "completed_expected_count",
        "unique_terminal_count",
        "passed",
    ):
        snapshot[name] = 3000
    snapshot["skipped"] = 0

    with pytest.raises(runner.RunnerError, match="minimum_tests=6001"):
        runner.validate_terminal(
            snapshot,
            project=tmp_path,
            mode="EditMode",
            filter_name="",
            group="",
            expected_utf="1.6.0",
            allow_empty=False,
            minimum_tests=runner.required_minimum_tests(
                "EditMode", "", "", None, False
            ),
        )
