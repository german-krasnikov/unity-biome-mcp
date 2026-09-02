"""Timing invariant tests — cross-layer timeout table.

Pins the Python <-> C# timeout relationships declared in
Plans/consumer-reports/ARC-4-timing-design.md Section 2.1. Two independent
sources feed these tests: tool_specs.py (via get_timeout, the Python side)
and MCPServer.cs's CommandTimeouts dict (the C# side, read as source text —
same "text search, no Unity" pattern as test_reload_timing_contracts.py
Group C). A drift on either side turns the relevant test red with the
offending command name.

This design's own summary line: "Invariant: Python timeout < C# timeout for
every command." Two rows are explicit, doc-approved exceptions to that summary
(legacy facades where C# may time out first) — see
test_legacy_facade_timeout_matches_documented_exception.
"""
from pathlib import Path

import pytest

from unity_mcp.timeout_categories import get_timeout
from unity_mcp.tools.batch import _TIMEOUT_MS_CEILING, _UNITY_BATCH_DEFAULT_MS
from unity_mcp.tools.tool_specs import DEFAULT_TIMEOUT
from helpers import CSHARP_TIMEOUT_OVERRIDES as _CSHARP_OVERRIDES

_PROJECT = Path(__file__).parents[2]
_MCP_SERVER_CS_PATH = _PROJECT / "unity-plugin/Editor/MCPServer.cs"
assert _MCP_SERVER_CS_PATH.exists(), f"C# source not found: {_MCP_SERVER_CS_PATH}"
_MCP_SERVER_CS = _MCP_SERVER_CS_PATH.read_text(encoding="utf-8")

_COMMAND_ROUTER_REGISTRATION_CS_PATH = (
    _PROJECT / "unity-plugin/Editor/CommandRouter.Registration.cs"
)
assert _COMMAND_ROUTER_REGISTRATION_CS_PATH.exists(), (
    f"C# source not found: {_COMMAND_ROUTER_REGISTRATION_CS_PATH}"
)
_COMMAND_ROUTER_REGISTRATION_CS = _COMMAND_ROUTER_REGISTRATION_CS_PATH.read_text(encoding="utf-8")

# Mirrors MCPServer.cs's CommandTimeouts dict (only the entry our table needs)
# and its GetCommandTimeout default fallback. Guarded against silent drift by
# test_csharp_command_timeouts_source_matches_fixture below.
_CSHARP_DEFAULT_TIMEOUT = 25


def _csharp_timeout(cmd: str) -> int:
    return _CSHARP_OVERRIDES.get(cmd, _CSHARP_DEFAULT_TIMEOUT)


def test_csharp_command_timeouts_source_matches_fixture():
    """Guards the hardcoded C# mirror above against silent MCPServer.cs drift.

    If either literal below changes in MCPServer.cs, this fails first —
    before the cross-layer tests below can report a misleading pass/fail.
    """
    assert '{ "run_tests", 130 },' in _MCP_SERVER_CS
    assert '{ "batch", 65 },' in _MCP_SERVER_CS
    assert (
        "return CommandTimeouts.TryGetValue(cmd, out var t) ? t : 25;"
        in _MCP_SERVER_CS
    )


def test_batch_inner_timeout_ms_ceiling_under_csharp_batch_watchdog():
    """DEV-55 [B3-#11]: batch's caller-tunable inner timeout_ms (sent as the
    'timeout_ms' arg to Unity's batch executor) must stay strictly below
    C#'s hardcoded outer 'batch' dispatch watchdog (MCPServer.cs
    CommandTimeouts), or the outer watchdog kills the whole command before
    Unity's own soft-timeout can return a graceful partial result.
    """
    assert _TIMEOUT_MS_CEILING == 60000
    assert _TIMEOUT_MS_CEILING < _csharp_timeout("batch") * 1000


def test_batch_default_ms_source_matches_fixture():
    """Guards batch._UNITY_BATCH_DEFAULT_MS against silent drift from
    CommandRouter.Registration.cs's "batch" dispatch default (the value
    Unity uses when the timeout_ms arg is omitted from the wire call).
    """
    assert _UNITY_BATCH_DEFAULT_MS == 25000
    assert "int timeoutMs = 25000;" in _COMMAND_ROUTER_REGISTRATION_CS


# ARC-4 Section 2.1 rows where "Python timeout < C# timeout" holds.
_STRICT_ROWS = [
    ("run_tests", DEFAULT_TIMEOUT),
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
