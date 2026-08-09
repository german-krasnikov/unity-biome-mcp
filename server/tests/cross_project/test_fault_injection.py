from __future__ import annotations

"""Fault-injection live tests: verify MCP bridge resilience via TCP fault proxy.

Proxy sits between the test bridge and Unity:
    test bridge → FaultProxy (random port) → Unity (real port)
"""

import asyncio
import contextlib
import socket
import sys
from pathlib import Path

import pytest
import pytest_asyncio

from unity_mcp.bridge import UnityBridge
from unity_mcp.compile_state import CompileStateProbe

# FaultProxy lives in scripts/, not on the installed package path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "scripts"))
from fault_proxy import FaultProxy  # noqa: E402

pytestmark = [pytest.mark.live, pytest.mark.cross_project, pytest.mark.asyncio(loop_scope="session")]


def _free_port() -> int:
    s = socket.socket()
    s.bind(("", 0))
    port = s.getsockname()[1]
    s.close()
    return port


@pytest_asyncio.fixture(loop_scope="session")
async def proxy_bridge(conformance_worker):
    """Provide a factory that starts a FaultProxy and returns a connected bridge.

    make(mode, fault_count=1) -> (proxy, bridge, proxy_port)
    Cleanup closes all bridges and servers on teardown.
    """
    worker, _real_bridge = conformance_worker
    real_port = worker.port

    servers: list[asyncio.AbstractServer] = []
    bridges: list[UnityBridge] = []

    async def make(mode: str, fault_count: int = 1, delay: float = 30.0):
        proxy_port = _free_port()
        proxy = FaultProxy(
            upstream_host="127.0.0.1",
            upstream_port=real_port,
            listen_port=proxy_port,
            mode=mode,
            fault_count=fault_count,
            delay=delay,
        )
        server = await asyncio.start_server(proxy.handle_client, "127.0.0.1", proxy_port)
        servers.append(server)

        # Use an explicit empty probe so UNITY_MCP_PROJECT_PATH does not
        # autodetect a project for the proxy port. The fault must hit the
        # command under test, not connect() identity probing.
        bridge = UnityBridge(
            "127.0.0.1",
            port=proxy_port,
            probe=CompileStateProbe(None, port=proxy_port),
            expected_project_path=None,
        )
        await bridge.connect()
        bridges.append(bridge)
        return proxy, bridge, proxy_port

    yield make

    for bridge in bridges:
        with contextlib.suppress(Exception):
            await bridge.close()
    for server in servers:
        server.close()
        await server.wait_closed()


async def test_passthrough_roundtrip(proxy_bridge):
    """Baseline: passthrough proxy forwards get_status without modification."""
    _, bridge, _ = await proxy_bridge("passthrough")
    resp = await bridge.send("get_status", {})
    assert resp["ok"], f"Expected ok=True through passthrough proxy: {resp}"


async def test_drop_ack_causes_timeout(proxy_bridge):
    """drop_ack on a read should be recovered by the bridge retry path."""
    _, bridge, _ = await proxy_bridge("drop_ack", fault_count=1)
    resp = await asyncio.wait_for(bridge.send("get_status", {}), timeout=10.0)
    assert resp["ok"], f"Expected read retry to recover after dropped ack: {resp}"


async def test_duplicate_frame_handled(proxy_bridge):
    """duplicate_frame sends the response twice; bridge must handle it gracefully."""
    _, bridge, _ = await proxy_bridge("duplicate_frame", fault_count=1)
    resp = await bridge.send("get_status", {})
    # The bridge reads the first frame and returns a valid dict.
    # The duplicate sits in the TCP buffer and is discarded on close.
    assert isinstance(resp, dict), f"Expected dict response, got: {resp!r}"
    assert resp.get("ok") is not None, f"Response missing 'ok' key: {resp}"


async def test_recovery_after_fault(proxy_bridge):
    """After one drop_ack fault the proxy passes subsequent connections through.

    Steps: first bridge faults → second bridge on same proxy port succeeds.
    """
    proxy, first_bridge, proxy_port = await proxy_bridge("drop_ack", fault_count=1)

    # First request triggers the fault and should recover through the bridge.
    first_resp = await asyncio.wait_for(first_bridge.send("get_status", {}), timeout=10.0)
    assert first_resp["ok"], f"Expected first read to recover after dropped ack: {first_resp}"

    # Proxy fault_count exhausted (_faulted=1 >= fault_count=1) — next
    # connection goes straight through.
    second_bridge = UnityBridge(
        "127.0.0.1",
        port=proxy_port,
        probe=CompileStateProbe(None, port=proxy_port),
        expected_project_path=None,
    )
    try:
        await second_bridge.connect()
        resp = await asyncio.wait_for(second_bridge.send("get_status", {}), timeout=10.0)
        assert resp["ok"], f"Expected recovery after fault exhausted: {resp}"
    finally:
        await second_bridge.close()
