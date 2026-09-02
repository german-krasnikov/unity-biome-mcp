"""Regression test for ARC-4 T1 — run_tests ACK dispatch timeout.

testing.py used to hardcode ``timeout=8.0`` on the run_tests ACK dispatch,
overriding tool_specs.py's 30s default. The C# side budgets 130s for the
full command (environment-prep + UTF.Execute routinely exceeds 8s), so the
8s override caused systematic START-UNKNOWN dispatch failures. See
Plans/consumer-reports/ARC-4-timing-design.md Task T1.
"""
from unittest.mock import AsyncMock

import pytest

import unity_mcp.tools.diagnose as diagnose
import unity_mcp.tools.testing as testing
from unity_mcp.timeout_categories import get_timeout

ACK = "tests-started|request_id=req-1|run_id=run-1|utf_guid=utf-1|state=dispatched"


@pytest.fixture(autouse=True)
def _patch_deps(monkeypatch):
    send = AsyncMock(side_effect=["none", ACK])
    monkeypatch.setattr(testing, "_send", send)
    monkeypatch.setattr(
        testing, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None}
    )
    monkeypatch.setattr(diagnose, "diagnose", AsyncMock(return_value="CLEAN-LIVE"))
    return send


async def test_run_tests_dispatch_uses_spec_timeout(_patch_deps):
    """The run_tests ACK dispatch must resolve to tool_specs's 30s budget.

    No explicit timeout kwarg is passed, so _send_raw's own
    `if timeout <= 0: timeout = get_timeout(cmd)` fallback applies —
    asserting get_timeout("run_tests") == 30.0 pins the effective value.
    """
    await testing.run_tests(request_id="req-1")

    dispatch_call = _patch_deps.call_args_list[-1]
    assert dispatch_call.args[0] == "run_tests"
    assert "timeout" not in dispatch_call.kwargs
    assert get_timeout("run_tests") == 30.0
