"""Wire-level fault injection tests using MitmProxy (no Unity required)."""

import pytest

from tests.wire.helpers.fake_server import FakeUnityServer
from tests.wire.helpers.mitm_proxy import (
    MitmProxy,
    blank_data,
    corrupt_length,
    flip_ok,
    swap_id,
)
from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_socket import DomainReloadError

pytestmark = pytest.mark.wire


async def test_corrupt_length_raises_connection_error(wire_server: FakeUnityServer, mitm_factory):
    """Corrupt frame length (0xFFFFFFFF) → bridge raises ConnectionError."""
    async with await mitm_factory([corrupt_length]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        with pytest.raises(ConnectionError):
            await bridge.send("ping", {})


async def test_flip_ok_passes_to_caller_as_success(wire_server: FakeUnityServer, mitm_factory):
    """flip_ok turns error response into ok:true — caller gets ok:true without raising."""
    wire_server.set_response("trigger_err", ok=False, error="forced error")
    async with await mitm_factory([flip_ok]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        result = await bridge.send("trigger_err", {})
    assert result["ok"] is True  # bridge trusts the wire; no validation layer


async def test_blank_data_returns_empty_string(wire_server: FakeUnityServer, mitm_factory):
    """blank_data clears the data field — bridge returns empty string, no exception."""
    async with await mitm_factory([blank_data]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        result = await bridge.send("ping", {})
    assert result["data"] == ""


async def test_swap_id_raises_connection_error(wire_server: FakeUnityServer, mitm_factory):
    """ID mismatch in response → bridge raises ConnectionError."""
    async with await mitm_factory([swap_id]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        with pytest.raises(ConnectionError, match="Response ID mismatch"):
            await bridge.send("ping", {})


async def test_bridge_not_connected_raises():
    """No server on port → ConnectionError on first send."""
    bridge = UnityBridge(host="127.0.0.1", port=19999)
    with pytest.raises((ConnectionError, OSError)):
        await bridge.send("ping", {})


async def test_going_away_raises_domain_reload_error(wire_server: FakeUnityServer, wire_bridge: UnityBridge):
    """going_away event frame → ConnectionError caused by DomainReloadError."""
    wire_server.inject_going_away()
    with pytest.raises(ConnectionError) as exc_info:
        await wire_bridge.send("ping", {})
    # Bridge wraps DomainReloadError in ConnectionError; original preserved as __cause__.
    assert isinstance(exc_info.value.__cause__, DomainReloadError)


async def test_bridge_closed_after_corrupt_frame(wire_server: FakeUnityServer, mitm_factory):
    """After corrupt frame error, bridge.connected is False."""
    async with await mitm_factory([corrupt_length]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        with pytest.raises(ConnectionError):
            await bridge.send("ping", {})
        assert not bridge.connected


async def test_multiple_transforms_applied_in_order(wire_server: FakeUnityServer, mitm_factory):
    """Transforms are applied left-to-right: flip_ok then blank_data."""
    wire_server.set_response("trigger_err", ok=False, error="e")
    async with await mitm_factory([flip_ok, blank_data]) as proxy:
        bridge = UnityBridge(host="127.0.0.1", port=proxy.port)
        await bridge.connect()
        result = await bridge.send("trigger_err", {})
    assert result["ok"] is True
    assert result["data"] == ""
