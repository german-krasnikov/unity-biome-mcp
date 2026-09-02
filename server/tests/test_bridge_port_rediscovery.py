"""Port re-discovery on reconnect: bridge updates _port when Unity restarts on new port."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble

_PORT_DRIFT_NOTICE = "port changed 9500->9501"


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


def _project_identity_chunks(project_path):
    response = {
        "id": "project-identity",
        "ok": True,
        "data": str(project_path),
    }
    payload = json.dumps(response).encode()
    return [struct.pack("!I", len(payload)), payload]


def _make_identity_reader(project_path, *, reconnect=True):
    chunks = []
    if reconnect:
        chunks.extend(reconnect_preamble()[:2])
    chunks.extend(_project_identity_chunks(project_path))
    if reconnect:
        chunks.extend(reconnect_preamble()[2:])
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=chunks)
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


# ---------------------------------------------------------------------------
# Fix 2: ConnectionRefused clears pinned_port so next reconnect rediscovers
# ---------------------------------------------------------------------------

async def test_reconnect_clears_pin_on_refused():
    """ConnectionRefusedError in open_connection must clear _pinned_port/_pinned_pid
    so the next reconnect attempt uses port_discoverer instead of the stale pin."""
    probe = make_idle_probe()
    bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
    bridge._pinned_port = 9500
    bridge._pinned_pid = 12345

    with patch.object(bridge_mod.asyncio, "open_connection",
                      side_effect=ConnectionRefusedError("refused")):
        with pytest.raises(ConnectionRefusedError):
            await bridge._reconnect(fire_callbacks=False)

    assert bridge._pinned_port is None
    assert bridge._pinned_pid is None


# ---------------------------------------------------------------------------
# Fix 3: Full cycle — refused → pin cleared → discoverer → success
# ---------------------------------------------------------------------------

async def test_full_cycle_refused_then_rediscovers():
    """ConnectionRefused clears pin → next reconnect uses discoverer → connects on new port."""
    call_count = 0

    async def mock_open(host, port):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            raise ConnectionRefusedError("old port gone")
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9502)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 99999

        # First: pinned → ConnectionRefused → pin cleared
        with pytest.raises(ConnectionRefusedError):
            await bridge._reconnect(fire_callbacks=False)
        assert bridge._pinned_port is None
        assert bridge._pinned_pid is None

        # Second: no pin → discoverer → 9502 → success
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._port == 9502
    discoverer.assert_called_once()


# ---------------------------------------------------------------------------
# Candidate identity: a live stale PID must not trust a reused TCP port
# ---------------------------------------------------------------------------

async def test_live_pinned_pid_foreign_project_rediscovers_matching_editor(tmp_path):
    """A responsive old port is rejected when another project now owns it."""
    expected_project = tmp_path / "expected-project"
    foreign_project = tmp_path / "foreign-project"
    expected_project.mkdir()
    foreign_project.mkdir()
    connected_to = []
    writers = {}

    async def mock_open(host, port):
        connected_to.append(port)
        writer = make_writer()
        writers[port] = writer
        project = foreign_project if port == 9500 else expected_project
        return _make_identity_reader(project), writer

    discoverer = Mock(return_value=9501)
    bridge = UnityBridge(
        "127.0.0.1",
        9500,
        probe=make_idle_probe(),
        port_discoverer=discoverer,
        expected_project_path=expected_project,
    )
    bridge._pinned_port = 9500
    bridge._pinned_pid = 12345

    def pid_for_port(port, project_path=None):
        return 12345 if port == 9500 else 23456

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", side_effect=pid_for_port), \
         patch.object(bridge, "start_heartbeat"):
        await bridge._reconnect(fire_callbacks=False)

    assert connected_to == [9500, 9501]
    assert writers[9500].close.call_count == 1
    assert bridge._port == 9501
    assert bridge._pinned_port == 9501
    assert bridge._pinned_pid == 23456
    discoverer.assert_called_once()


async def test_discovered_foreign_project_is_rejected_before_accept(tmp_path):
    expected_project = tmp_path / "expected-project"
    foreign_project = tmp_path / "foreign-project"
    expected_project.mkdir()
    foreign_project.mkdir()
    writer = make_writer()

    async def mock_open(host, port):
        return _make_identity_reader(foreign_project), writer

    bridge = UnityBridge(
        "127.0.0.1",
        9500,
        probe=make_idle_probe(),
        port_discoverer=Mock(return_value=9501),
        expected_project_path=expected_project,
    )

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        with pytest.raises(ConnectionError, match="Refusing Unity on port 9501"):
            await bridge._reconnect(fire_callbacks=False)

    assert bridge._port == 9500
    assert bridge._reader is None
    assert bridge._writer is None
    writer.close.assert_called_once()


async def test_initial_connect_rejects_foreign_project_before_assign(tmp_path):
    expected_project = tmp_path / "expected-project"
    foreign_project = tmp_path / "foreign-project"
    expected_project.mkdir()
    foreign_project.mkdir()
    writer = make_writer()
    reader = _make_identity_reader(foreign_project, reconnect=False)
    bridge = UnityBridge(
        "127.0.0.1",
        9500,
        probe=make_idle_probe(),
        expected_project_path=expected_project,
    )

    with patch.object(
        bridge_mod.asyncio, "open_connection", return_value=(reader, writer)
    ):
        with pytest.raises(ConnectionError, match="Refusing Unity on port 9500"):
            await bridge.connect()

    assert bridge._reader is None
    assert bridge._writer is None
    assert bridge._pinned_port is None
    writer.close.assert_called_once()


# ---------------------------------------------------------------------------
# ARC-9 T2: same-pid port rebind fast path — no ConnectionRefused cycle
# ---------------------------------------------------------------------------

async def test_reconnect_fast_path_skips_stale_port_on_same_pid_rebind():
    """Pinned pid is still alive but Unity rebound to a new port on the same
    process (C# bind-conflict fallback). Reconnect must read the pid's
    current port file and connect directly to the new port on the first
    attempt — no ConnectionRefused cycle against the abandoned port."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_port_for_pid", return_value=9501):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345
        await bridge._reconnect(fire_callbacks=False)

    assert connected_to == [9501]
    assert bridge._port == 9501
    assert bridge._port_drift == (9500, 9501)


async def test_reconnect_fast_path_no_drift_when_port_unchanged():
    """Pinned pid alive, port file reports the same port — behaves exactly
    like before: no drift recorded, connects to the pinned port once."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_port_for_pid", return_value=9500):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345
        await bridge._reconnect(fire_callbacks=False)

    assert connected_to == [9500]
    assert bridge._port == 9500
    assert bridge._port_drift is None


async def test_reconnect_fast_path_none_keeps_old_behavior():
    """read_port_for_pid returns None (no port file for this pid, or a dead
    pid edge case) — falls back to the pre-existing pinned-port path
    unchanged; no drift is ever recorded from a None reading."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open), \
         patch("unity_mcp.bridge.is_pid_alive", return_value=True), \
         patch("unity_mcp.lockfile.read_port_for_pid", return_value=None):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        bridge._pinned_port = 9500
        bridge._pinned_pid = 12345
        await bridge._reconnect(fire_callbacks=False)

    assert connected_to == [9500]
    assert bridge._port == 9500
    assert bridge._port_drift is None


def test_pop_port_drift_notice_consumes_once():
    """pop_port_drift_notice() returns the formatted transition exactly once,
    then clears the state so the next call reports no pending drift."""
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    bridge._port_drift = (9500, 9501)

    assert bridge.pop_port_drift_notice() == _PORT_DRIFT_NOTICE
    assert bridge.pop_port_drift_notice() is None


def test_pop_port_drift_notice_none_when_no_drift():
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    assert bridge.pop_port_drift_notice() is None


def test_peek_port_drift_notice_does_not_consume():
    """peek_port_drift_notice() is non-destructive: repeated calls (and a
    subsequent pop) all still see the pending drift."""
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    bridge._port_drift = (9500, 9501)

    assert bridge.peek_port_drift_notice() == _PORT_DRIFT_NOTICE
    assert bridge.peek_port_drift_notice() == _PORT_DRIFT_NOTICE
    assert bridge.pop_port_drift_notice() == _PORT_DRIFT_NOTICE
    assert bridge.peek_port_drift_notice() is None


def test_peek_port_drift_notice_none_when_no_drift():
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    assert bridge.peek_port_drift_notice() is None


def test_pid_lookup_filters_reused_port_by_canonical_project(tmp_path):
    from unity_mcp.lockfile import read_pid_from_port_file

    expected_project = tmp_path / "expected-project"
    foreign_project = tmp_path / "foreign-project"
    expected_project.mkdir()
    foreign_project.mkdir()
    expected_alias = tmp_path / "expected-alias"
    expected_alias.symlink_to(expected_project, target_is_directory=True)

    foreign_record = tmp_path / "111.port"
    expected_record = tmp_path / "222.port"
    foreign_record.write_text(f"9500\n{foreign_project}\nforeign\n", encoding="utf-8")
    expected_record.write_text(f"9500\n{expected_alias}\nexpected\n", encoding="utf-8")

    with patch(
        "unity_mcp.lockfile._iter_port_files",
        return_value=[foreign_record, expected_record],
    ), patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        pid = read_pid_from_port_file(9500, project_path=expected_project)

    assert pid == 222
