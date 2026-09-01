"""TDD tests for mcp_status liveness surface (ARC-7 T3).

mcp_status must answer with honest Python-side diagnostics (liveness,
pid_alive, last_contact_s, ping_fail, ping_stall, queue_depth) even when
Unity itself is unreachable, instead of letting the underlying ToolError
propagate. See Plans/consumer-reports/ARC-7-periodic-liveness.md Task 3.

server/tests/test_mcp_status.py stays unedited as the raw-passthrough guard.
"""
import re
from unittest.mock import AsyncMock

from mcp.server.fastmcp.exceptions import ToolError


def _field(result: str, key: str) -> str:
    m = re.search(rf"^{key}=(\S+)$", result, re.MULTILINE)
    assert m is not None, f"no '{key}=' line in: {result!r}"
    return m.group(1)


async def test_includes_liveness_verdict(mock_bridge, bridge_response):
    """liveness= reflects bridge.status exactly (not a coincidental substring)."""
    bridge_response(data="scene=Main")
    mock_bridge.status = "connected"
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert _field(result, "liveness") == "connected"


async def test_survives_unreachable_unity(mock_bridge):
    """mcp_status must not raise when the bounded get_status call fails."""
    mock_bridge.send = AsyncMock(side_effect=ToolError("[UNITY_UNAVAILABLE] gone"))
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()  # must not raise
    assert _field(result, "unity_status") == "unreachable"


async def test_uses_bounded_timeout(mock_bridge, bridge_response):
    """get_status is sent with a 5s bound, not the per-command default."""
    bridge_response(data="scene=Main")
    from unity_mcp.tools.meta import mcp_status
    await mcp_status()
    assert mock_bridge.send.call_args.kwargs["timeout"] == 5.0


async def test_reports_pid_alive_false_when_unity_gone(mock_bridge, bridge_response):
    """pid_alive= is derived from CompileStateProbe.is_process_dead(), inverted."""
    bridge_response(data="scene=Main")
    mock_bridge._probe.is_process_dead.return_value = True
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert _field(result, "pid_alive") == "false"


async def test_last_contact_s_is_na_when_bridge_reports_none(mock_bridge, bridge_response):
    """None from bridge.last_contact_age_s (real semantics: no contact yet,
    e.g. the sub-ping-interval window right after a reconnect) must render as
    the honest 'n/a' placeholder, never a fabricated number."""
    bridge_response(data="scene=Main")
    mock_bridge.last_contact_age_s = None
    from unity_mcp.tools.meta import mcp_status
    result = await mcp_status()
    assert _field(result, "last_contact_s") == "n/a"
