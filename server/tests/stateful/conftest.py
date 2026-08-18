"""Fixtures for stateful Hypothesis tests."""

import pytest_asyncio

from tests.wire.helpers.stateful_server import StatefulFakeServer
from unity_mcp.bridge import UnityBridge


@pytest_asyncio.fixture
async def stateful_server() -> StatefulFakeServer:
    """StatefulFakeServer with real object registry."""
    async with StatefulFakeServer() as server:
        yield server


@pytest_asyncio.fixture
async def stateful_bridge(stateful_server: StatefulFakeServer) -> UnityBridge:
    """UnityBridge connected to stateful_server."""
    bridge = UnityBridge(host="127.0.0.1", port=stateful_server.port)
    await bridge.connect()
    try:
        yield bridge
    finally:
        await bridge.close()
