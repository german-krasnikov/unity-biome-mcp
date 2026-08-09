from __future__ import annotations

from unittest.mock import AsyncMock

import pytest
from conformance.workers import ConformanceWorker, _parse_status


def _clean_status(port: int = 9500) -> dict:
    return {"data": f"scene=SampleScene\ndirty=false\nplaying=false\ncompiling=false\nport={port}\naliases=0"}


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


def test_parse_status_normalizes_boolean_values():
    info = _parse_status("dirty=False\nplaying=True\ncompiling=false\nport=9500")

    assert info["dirty"] == "false"
    assert info["playing"] == "true"
    assert info["compiling"] == "false"


async def test_gate_passes_clean_state():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = _clean_status(9500)
    await w.gate(bridge)  # must not raise


# --- gate() failure cases ---


async def test_gate_raises_on_dirty():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = {"data": "scene=SampleScene\ndirty=true\nplaying=false\ncompiling=false\nport=9500\naliases=0"}
    with pytest.raises(AssertionError, match="dirty"):
        await w.gate(bridge)


async def test_gate_raises_on_playing():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = {"data": "scene=SampleScene\ndirty=false\nplaying=true\ncompiling=false\nport=9500\naliases=0"}
    with pytest.raises(AssertionError, match="Play Mode"):
        await w.gate(bridge)


async def test_gate_raises_on_compiling():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = {"data": "scene=SampleScene\ndirty=false\nplaying=false\ncompiling=true\nport=9500\naliases=0"}
    with pytest.raises(AssertionError, match="compiling"):
        await w.gate(bridge)


async def test_gate_raises_on_port_mismatch():
    w = ConformanceWorker(port=9500, project_path="/proj", run_id="x")
    bridge = AsyncMock()
    bridge.send.return_value = _clean_status(port=9999)
    with pytest.raises(AssertionError, match="port mismatch"):
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
