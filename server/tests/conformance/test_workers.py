from __future__ import annotations

from unittest.mock import AsyncMock

import pytest
from conformance.workers import ConformanceWorker

def _clean_status(port: int = 9500, project_path: str = "/proj") -> dict:
    return {"data": {"port": port, "project_path": project_path, "dirty": False, "playing": False, "compiling": False}}


def _hierarchy(content: str = "") -> dict:
    return {"data": content}


# --- asset_ns ---


def test_asset_ns_format():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="abc123")
    assert w.asset_ns == "Assets/__MCP_CONF_abc123"


def test_scene_ns_format():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="abc123")
    assert w.scene_ns == "__MCP_CONF_abc123"


# --- gate() happy path ---


async def test_gate_passes_clean_state():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = _clean_status(9500, "/proj")
    await w.gate(bridge)  # must not raise


# --- gate() failure cases ---


async def test_gate_raises_on_dirty():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    status = _clean_status()
    status["data"]["dirty"] = True
    bridge.send.return_value = status
    with pytest.raises(AssertionError, match="dirty"):
        await w.gate(bridge)


async def test_gate_raises_on_playing():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    status = _clean_status()
    status["data"]["playing"] = True
    bridge.send.return_value = status
    with pytest.raises(AssertionError, match="Play Mode"):
        await w.gate(bridge)


async def test_gate_raises_on_compiling():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    status = _clean_status()
    status["data"]["compiling"] = True
    bridge.send.return_value = status
    with pytest.raises(AssertionError, match="compiling"):
        await w.gate(bridge)


async def test_gate_raises_on_port_mismatch():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = _clean_status(port=9999, project_path="/proj")
    with pytest.raises(AssertionError, match="port mismatch"):
        await w.gate(bridge)


async def test_gate_raises_on_project_path_mismatch():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = _clean_status(port=9500, project_path="/other")
    with pytest.raises(AssertionError, match="project_path mismatch"):
        await w.gate(bridge)


# --- prove_absent() ---


async def test_prove_absent_passes_when_not_in_hierarchy():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="abc123")
    bridge = AsyncMock()
    bridge.send.return_value = _hierarchy("SomeOtherObject\nAnotherObject")
    await w.prove_absent(bridge)  # must not raise


async def test_prove_absent_raises_when_scene_ns_present():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="abc123")
    bridge = AsyncMock()
    bridge.send.return_value = _hierarchy("SomeObject\n__MCP_CONF_abc123\nOther")
    with pytest.raises(AssertionError, match="cleanup failed"):
        await w.prove_absent(bridge)
