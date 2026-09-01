"""Timing invariant tests — ARC-4 cross-layer timeout table.

Pins the Python <-> C# timeout relationships declared in
Plans/consumer-reports/ARC-4-timing-design.md Section 2.1. Two independent
sources feed these tests: tool_specs.py (via get_timeout, the Python side)
and MCPServer.cs's CommandTimeouts dict (the C# side, read as source text —
same "text search, no Unity" pattern as test_reload_timing_contracts.py
Group C). A drift on either side turns the relevant test red with the
offending command name.

ARC-4's own summary line: "Invariant: Python timeout < C# timeout for every
command." Two rows are explicit, doc-approved exceptions to that summary
(legacy facades where C# may time out first) — see
test_legacy_facade_timeout_matches_documented_exception.
"""
from pathlib import Path

import pytest

from unity_mcp.timeout_categories import get_timeout

_PROJECT = Path(__file__).parents[2]
_MCP_SERVER_CS = (_PROJECT / "unity-plugin/Editor/MCPServer.cs").read_text(encoding="utf-8")

# Mirrors MCPServer.cs's CommandTimeouts dict (only the entry our table needs)
# and its GetCommandTimeout default fallback. Guarded against silent drift by
# test_csharp_command_timeouts_source_matches_fixture below.
_CSHARP_DEFAULT_TIMEOUT = 25
_CSHARP_OVERRIDES = {"run_tests": 130}


def _csharp_timeout(cmd: str) -> int:
    return _CSHARP_OVERRIDES.get(cmd, _CSHARP_DEFAULT_TIMEOUT)


def test_csharp_command_timeouts_source_matches_fixture():
    """Guards the hardcoded C# mirror above against silent MCPServer.cs drift.

    If either literal below changes in MCPServer.cs, this fails first —
    before the cross-layer tests below can report a misleading pass/fail.
    """
    assert '{ "run_tests", 130 },' in _MCP_SERVER_CS
    assert (
        "return CommandTimeouts.TryGetValue(cmd, out var t) ? t : 25;"
        in _MCP_SERVER_CS
    )


# ARC-4 Section 2.1 rows where "Python timeout < C# timeout" holds.
_STRICT_ROWS = [
    ("run_tests", 30.0),
    ("get_test_run", 10.0),
    ("resolve_test_request", 10.0),
    ("cancel_test_run", 10.0),
]


@pytest.mark.parametrize("cmd, py_timeout", _STRICT_ROWS)
def test_python_timeout_under_csharp_budget(cmd, py_timeout):
    """ARC-4 invariant: Python gives up strictly before C#'s own timeout, so
    the caller sees C#'s structured error instead of Python closing a live
    TCP read out from under an in-flight command.
    """
    assert get_timeout(cmd) == py_timeout
    assert py_timeout < _csharp_timeout(cmd)


# ARC-4 Section 2.1 documented exceptions: legacy facades where C# is
# allowed to time out before Python ("acceptable, returns timeout error").
# Do not "fix" these by adding a C# override without updating the doc first.
_LEGACY_FACADE_ROWS = [
    ("get_test_results", 30.0),
    ("get_test_progress", 30.0),
]


@pytest.mark.parametrize("cmd, py_timeout", _LEGACY_FACADE_ROWS)
def test_legacy_facade_timeout_matches_documented_exception(cmd, py_timeout):
    assert get_timeout(cmd) == py_timeout
    assert _csharp_timeout(cmd) == _CSHARP_DEFAULT_TIMEOUT
    assert py_timeout > _csharp_timeout(cmd), (
        f"{cmd} no longer violates ARC-4's strict invariant -- update "
        "Plans/consumer-reports/ARC-4-timing-design.md Section 2.1 to move "
        "it out of the documented-exception table before tightening this "
        "assertion"
    )
