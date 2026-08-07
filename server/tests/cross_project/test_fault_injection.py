from __future__ import annotations

"""Fault-injection live tests: verify MCP bridge resilience via TCP fault proxy.

Proxy sits between the test bridge and Unity:
    test bridge → FaultProxy (random port) → Unity (real port)
"""

import asyncio
import socket
import sys
from pathlib import Path

import pytest

from unity_mcp.bridge import UnityBridge

# FaultProxy lives in scripts/, not on the installed package path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "scripts"))
from fault_proxy import FaultProxy  # noqa: E402

pytestmark = [pytest.mark.live, pytest.mark.cross_project]


def _free_port() -> int:
    s = socket.socket()
    s.bind(("", 0))
    port = s.getsockname()[1]
    s.close()
    return port


@pytest.fixture
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

        # expected_project_path=None skips handshake verification so the
        # proxy's fault_count isn't consumed by the connect() identity probe.
        bridge = UnityBridge("127.0.0.1", port=proxy_port, expected_project_path=None)
        await bridge.connect()
        bridges.append(bridge)
        return proxy, bridge, proxy_port

    yield make

    for bridge in bridges:
        try:
            await bridge.close()
        except Exception:  # noqa: BLE001
            pass
    for server in servers:
        server.close()
        await server.wait_closed()


async def test_passthrough_roundtrip(proxy_bridge):
    """Baseline: passthrough proxy forwards get_status without modification."""
    _, bridge, _ = await proxy_bridge("passthrough")
    resp = await bridge.send("get_status", {})
    assert resp["ok"], f"Expected ok=True through passthrough proxy: {resp}"


async def test_drop_ack_causes_timeout(proxy_bridge):
    """drop_ack silently discards the response and closes the connection.

    Bridge MIN_RECONNECT_INTERVAL=5s > our 3s wait_for, so the send()
    reliably times out before any retry can succeed.
    """
    _, bridge, _ = await proxy_bridge("drop_ack", fault_count=1)
    with pytest.raises((asyncio.TimeoutError, ConnectionError, OSError)):
        await asyncio.wait_for(bridge.send("get_status", {}), timeout=3.0)


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

    # First request triggers the fault (response dropped, connection closed)
    with pytest.raises((asyncio.TimeoutError, ConnectionError, OSError)):
        await asyncio.wait_for(first_bridge.send("get_status", {}), timeout=3.0)

    # Proxy fault_count exhausted (_faulted=1 >= fault_count=1) — next
    # connection goes straight through.
    second_bridge = UnityBridge("127.0.0.1", port=proxy_port, expected_project_path=None)
    try:
        await second_bridge.connect()
        resp = await asyncio.wait_for(second_bridge.send("get_status", {}), timeout=10.0)
        assert resp["ok"], f"Expected recovery after fault exhausted: {resp}"
    finally:
        await second_bridge.close()
