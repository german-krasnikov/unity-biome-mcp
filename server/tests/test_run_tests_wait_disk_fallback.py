"""DI seam for run_tests_wait's disk fallback (ARC-2 D2) and the
decode/gate/marker glue that reads it (ARC-2 D3).

D2 only threads `get_slot` into testing.register so `_resolve_project_path`
can resolve the connected Unity project's path (get_slot -> port ->
CompileStateProbe.autodetect_project_path). D3 adds `_read_disk_fallback`,
which turns that disk text into either `None` (no usable fallback) or a
wire-shaped snapshot string marked `"read_via": "disk"` — reusing
`_decode_snapshot`/`_terminal_snapshot_error` verbatim, no new validation
logic. D4 (a later task) wires `_read_disk_fallback` into run_tests_wait's
TIMEOUT return; it is not called from anywhere yet.
"""

import json
from types import SimpleNamespace
from unittest.mock import MagicMock, patch

import pytest

import unity_mcp.tools.testing as testing

REQUEST_ID = "req-1"
RUN_ID = "run-1"


def _snapshot(state: str, outcome: str = "", health: str = "") -> str:
    """Copied verbatim from test_run_tests_wait.py:22-66 (ARC-2 D3 spec)."""
    terminal = state == "terminal"
    expected = 6964
    failed = 1 if terminal and outcome == "failed" else 0
    skipped = 1 if terminal else 0
    passed = expected - failed - skipped if terminal else 4
    run_finished_observed = terminal and outcome != "incomplete"
    data = {
        "request_id": REQUEST_ID,
        "run_id": RUN_ID,
        "utf_guid": "utf-1",
        "state": state,
        "lifecycle": state,
        "outcome": outcome,
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "is_terminal": terminal,
        "execution_finished": terminal,
        "cleanup_complete": terminal,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": run_finished_observed,
        "build_coherent": True,
        "utf_xml_scope": "complete" if terminal else "none",
        "expected_count": expected,
        "declared_expected_count": expected,
        "readable_manifest_count": expected,
        "completed_expected_count": expected if terminal else 4,
        "unique_terminal_count": expected if terminal else 4,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
        "counts": {
            "expected": expected,
            "finished": expected if terminal else 4,
        },
    }
    if health:
        data["health"] = health
    return json.dumps(data)


def _zero_match_snapshot() -> str:
    """Terminal, correlated, but expected_count == 0 -- the fail-closed zero-match path."""
    data = json.loads(_snapshot("terminal", "passed"))
    data.update(
        expected_count=0,
        declared_expected_count=0,
        readable_manifest_count=0,
        completed_expected_count=0,
        unique_terminal_count=0,
        passed=0,
        failed=0,
        skipped=0,
    )
    return json.dumps(data)


@pytest.fixture(autouse=True)
def _restore_testing_globals():
    """Isolate testing.py's module globals across tests in this file.

    register() rebinds _send/_args/_get_slot (bind() pattern, same as every
    tools/*.py module) — unity_mcp.server.run_tests is the *same* function
    object, reading these globals at call time, so leaving them mutated here
    corrupts every later test-file's real send() binding (cross-test
    pollution, not just within this file).
    """
    original = (testing._send, testing._args, testing._get_slot)
    yield
    testing._send, testing._args, testing._get_slot = original


def test_resolve_project_path_none_without_get_slot():
    """No get_slot wired in -> no project path, ever (fail-inert, never crash)."""
    testing._get_slot = None

    assert testing._resolve_project_path() is None


def test_resolve_project_path_uses_connected_slot_port():
    """register(get_slot=...) threads the live port into autodetect_project_path."""
    testing.register(
        MagicMock(), MagicMock(), MagicMock(),
        get_slot=lambda: SimpleNamespace(port=4999),
    )

    with patch(
        "unity_mcp.compile_state.CompileStateProbe.autodetect_project_path"
    ) as mock_autodetect:
        mock_autodetect.return_value = "/fake/project"

        result = testing._resolve_project_path()

    assert result == "/fake/project"
    mock_autodetect.assert_called_once_with(port=4999)


def _call_fallback(run_id: str = RUN_ID, *, mode: str = "EditMode", filter_name: str = ""):
    return testing._read_disk_fallback(
        run_id, mode=mode, filter_name=filter_name, expected_request_id=REQUEST_ID,
    )


def test_disk_fallback_none_when_project_path_unresolved():
    """No connected project path -> disk is never even touched."""
    with patch.object(testing, "_resolve_project_path", return_value=None), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary") as mock_read:
        result = _call_fallback()

    assert result is None
    mock_read.assert_not_called()


def test_disk_fallback_none_when_file_missing():
    """No summary.json on disk (run never finalized, or wrong run_id) -> None."""
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value=None):
        result = _call_fallback()

    assert result is None


def test_disk_fallback_none_for_non_terminal_snapshot():
    """A non-terminal disk snapshot (Editor crashed mid-run) is not usable evidence."""
    disk_json = _snapshot("running")
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value=disk_json):
        result = _call_fallback()

    assert result is None


def test_disk_fallback_none_when_terminal_invariants_fail():
    """Terminal on disk but intent mismatch (mode) -> _terminal_snapshot_error rejects it."""
    disk_json = _snapshot("terminal", "passed")
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value=disk_json):
        result = _call_fallback(mode="PlayMode")

    assert result is None


def test_disk_fallback_none_for_zero_match_terminal():
    """Terminal on disk with expected_count == 0 fails closed, same as a wire read."""
    disk_json = _zero_match_snapshot()
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value=disk_json):
        result = _call_fallback()

    assert result is None


def test_disk_fallback_none_for_corrupt_json():
    """Half-written/corrupt summary.json text decodes to nothing -> inert None."""
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value="{not json"):
        result = _call_fallback()

    assert result is None


def test_disk_fallback_none_when_run_id_has_path_separator():
    """A run_id smuggling '../' must never reach the filesystem read."""
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary") as mock_read:
        result = _call_fallback(run_id="../escape")

    assert result is None
    mock_read.assert_not_called()


def test_disk_fallback_returns_marked_snapshot_for_valid_terminal():
    """A valid terminal disk snapshot comes back marked read_via=disk, unmodified otherwise."""
    disk_json = _snapshot("terminal", "passed")
    with patch.object(testing, "_resolve_project_path", return_value="/fake/project"), \
         patch.object(testing.run_disk_fallback, "read_terminal_summary", return_value=disk_json):
        result = _call_fallback()

    decoded = json.loads(result)
    assert decoded["read_via"] == "disk"
    assert decoded["outcome"] == "passed"
