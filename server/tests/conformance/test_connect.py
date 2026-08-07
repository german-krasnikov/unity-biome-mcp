"""Gate 1: Connection + Identity — basic TCP roundtrip and identity checks."""
from __future__ import annotations

import pytest

from conformance.workers import _parse_status

pytestmark = pytest.mark.live


async def test_tcp_roundtrip(conformance_worker):
    """Unity TCP bridge responds to get_status within reasonable time."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_status", {})
    assert "data" in resp
    assert resp.get("ok", True)


async def test_identity_gate_port_matches(conformance_worker):
    """Response port matches the port we connected to."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_status", {})
    info = _parse_status(resp["data"])
    assert info["port"] == str(worker.port)


async def test_identity_gate_clean_state(conformance_worker):
    """Editor is not dirty, not playing, not compiling."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_status", {})
    info = _parse_status(resp["data"])
    assert info.get("dirty", "false") == "false", "scene is dirty"
    assert info.get("playing", "false") == "false", "editor is in Play Mode"
    assert info.get("compiling", "false") == "false", "editor is compiling"


async def test_connection_still_alive(conformance_worker):
    """Bridge is still responsive after Gate 1 tests ran."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_status", {})
    assert resp.get("ok", True), f"bridge unresponsive: {resp}"
    assert "data" in resp
