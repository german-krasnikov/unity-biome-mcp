"""Gate 1: Connection + Identity — basic TCP roundtrip and identity checks."""
from __future__ import annotations

import pytest

pytestmark = pytest.mark.live


async def test_tcp_roundtrip(conformance_worker):
    """MCP server responds to mcp_status within reasonable time."""
    worker, bridge = conformance_worker
    resp = await bridge.send("mcp_status", {})
    assert "data" in resp
    assert resp.get("ok", True)


async def test_identity_gate_port_matches(conformance_worker):
    """Response port matches the port we connected to."""
    worker, bridge = conformance_worker
    resp = await bridge.send("mcp_status", {})
    data = resp["data"]
    assert data["port"] == worker.port


async def test_identity_gate_project_matches(conformance_worker):
    """Response project_path matches our expected project."""
    worker, bridge = conformance_worker
    resp = await bridge.send("mcp_status", {})
    data = resp["data"]
    assert data["project_path"] == worker.project_path
