"""DI seam for run_tests_wait's disk fallback (ARC-2 D2).

D2 only threads `get_slot` into testing.register so `_resolve_project_path`
can resolve the connected Unity project's path (get_slot -> port ->
CompileStateProbe.autodetect_project_path). No fallback-read logic lives
here yet (D3/D4 add `_read_disk_fallback` and the TIMEOUT wiring) — this
file only proves the DI wiring itself is mockable, per the repo's
"every module mockable via send/args injection" architecture principle.
"""

from types import SimpleNamespace
from unittest.mock import MagicMock, patch

import pytest

import unity_mcp.tools.testing as testing


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
