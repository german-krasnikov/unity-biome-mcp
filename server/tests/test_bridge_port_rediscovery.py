"""Port re-discovery on reconnect: bridge updates _port when Unity restarts on new port."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble


@pytest.fixture(autouse=True)
def _fast_timeouts():
    orig = bridge_mod.CONNECT_TIMEOUT
    bridge_mod.CONNECT_TIMEOUT = 0.05
    yield
    bridge_mod.CONNECT_TIMEOUT = orig


def _make_ok_reader(msg_id="0001"):
    ping_hdr, ping_pay = ping_response()
    r = {"id": msg_id, "ok": True, "data": "ok"}
    p = json.dumps(r).encode()
    hdr, pay = struct.pack("!I", len(p)), p
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), hdr, pay])
    return reader


# ---------------------------------------------------------------------------
# 1. Discoverer returns new port — bridge._port updates
# ---------------------------------------------------------------------------

async def test_reconnect_rediscovers_port():
    """port_discoverer returns 9501 → bridge connects on 9501."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        await bridge._reconnect()

    assert bridge._port == 9501
    assert 9501 in connected_to


# ---------------------------------------------------------------------------
# 2. Discoverer raises — falls back to current port
# ---------------------------------------------------------------------------

async def test_reconnect_falls_back_on_discoverer_failure():
    """port_discoverer raises OSError → bridge stays on 9500."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(side_effect=OSError("no port file"))
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        await bridge._reconnect()

    assert bridge._port == 9500
    assert connected_to == [9500]


# ---------------------------------------------------------------------------
# 3. Discoverer returns same port — no probe churn
# ---------------------------------------------------------------------------

async def test_reconnect_same_port_no_change():
    """Discoverer returns same port → bridge._port unchanged, probe not replaced."""
    async def mock_open(host, port):
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9500)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        original_probe = bridge._probe
        await bridge._reconnect()

    assert bridge._port == 9500
    assert bridge._probe is original_probe


# ---------------------------------------------------------------------------
# 4. No discoverer — backward-compat normal reconnect
# ---------------------------------------------------------------------------

async def test_reconnect_without_discoverer():
    """No port_discoverer → normal reconnect on existing port."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        await bridge._reconnect()

    assert bridge._port == 9500
    assert connected_to == [9500]


# ---------------------------------------------------------------------------
# Phase 4a: Reconnect pin — bridge stays on pinned port when PID alive
# ---------------------------------------------------------------------------

async def test_reconnect_stays_on_pinned_port_when_pid_alive():
    """After first connect sets _pinned_port/_pinned_pid, reconnect skips discoverer
    if the Unity process is still alive (PID check)."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    # discoverer would return a different port (simulating wrong Unity)
    discoverer = Mock(return_value=9999)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        # Simulate first successful connect: set pinned port+pid
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345
        await bridge._reconnect()

    # Should stay on 9500, NOT switch to 9999 (discoverer result)
    assert bridge._port == 9500
    assert 9500 in connected_to
    assert 9999 not in connected_to


async def test_reconnect_rediscovers_when_pid_dead():
    """When pinned PID is dead, bridge falls through to full port_discoverer."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=False):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345  # dead PID
        await bridge._reconnect()

    # Dead PID → should rediscover → 9501
    assert bridge._port == 9501
    assert 9501 in connected_to


async def test_reconnect_pins_port_on_first_connect():
    """First successful _reconnect sets _pinned_port = connected port."""
    async def mock_open(host, port):
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9502)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        assert bridge._pinned_port is None  # not yet pinned
        await bridge._reconnect()

    assert bridge._pinned_port == 9502  # pinned to what discoverer returned


async def test_reconnect_no_discoverer_no_pin_logic():
    """No port_discoverer → normal reconnect on existing port.
    Pin logic still runs after successful connect: _pinned_port is set to the
    connected port, _pinned_pid may be None if no port file exists in test env.
    """
    async def mock_open(host, port):
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        assert bridge._pinned_port is None  # not yet connected
        await bridge._reconnect()

    assert bridge._port == 9500           # port unchanged
    assert bridge._pinned_port == 9500    # pin always set after successful connect
    assert bridge._pinned_pid is None     # no port file in test env


# ---------------------------------------------------------------------------
# Phase 4b: is_pid_alive(None) → False → falls through to discoverer
# ---------------------------------------------------------------------------

async def test_reconnect_pid_none_falls_through_to_discoverer():
    """Discoverer returns new port → read_pid_from_port_file returns None →
    _pinned_pid is None → next reconnect is_pid_alive(None) returns False →
    falls through to discoverer again (not stuck on old port).
    """
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9503)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        # Simulate: pinned port set, but pid is None (no port file found)
        bridge._pinned_port = 9500
        bridge._pinned_pid = None
        # is_pid_alive(None) == False → falls through to discoverer
        await bridge._reconnect()

    assert bridge._port == 9503    # discoverer was used
    assert 9503 in connected_to
    assert bridge._pinned_pid is None   # still None (read_pid_from_port_file mocked)
