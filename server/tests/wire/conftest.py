"""Wire-level test fixtures: FakeUnityServer, UnityBridge, MitmProxy."""

from pathlib import Path

import pytest
import pytest_asyncio

import unity_mcp.bridge as _bridge_mod
from unity_mcp.bridge import UnityBridge
from tests.wire.helpers.fake_server import FakeUnityServer
from tests.wire.helpers.mitm_proxy import MitmProxy

_CASSETTES = Path(__file__).parent / "cassettes"


@pytest.fixture(autouse=True)
def _no_retries(monkeypatch: pytest.MonkeyPatch) -> None:
    """Disable bridge retries for all wire tests — keeps each test under 100ms."""
    monkeypatch.setattr(_bridge_mod, "MAX_RETRIES", 0)


@pytest_asyncio.fixture
async def wire_server() -> "FakeUnityServer":
    """FakeUnityServer with protocol_baseline cassette pre-loaded."""
    async with FakeUnityServer() as server:
        server.load_cassette(_CASSETTES / "protocol_baseline.jsonl")
        yield server


@pytest_asyncio.fixture
async def wire_bridge(wire_server: FakeUnityServer) -> "UnityBridge":
    """UnityBridge connected to wire_server (no project-identity check)."""
    bridge = UnityBridge(host="127.0.0.1", port=wire_server.port)
    await bridge.connect()
    try:
        yield bridge
    finally:
        await bridge.close()


@pytest.fixture
def mitm_factory(wire_server: FakeUnityServer):
    """Return an async factory that creates a MitmProxy targeting wire_server."""
    async def _make(transforms=()) -> MitmProxy:
        return MitmProxy("127.0.0.1", wire_server.port, transforms)
    return _make
