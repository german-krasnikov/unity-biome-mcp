"""Domain-reload reconnection tests — bridge must survive Unity restarts.

Updated for simplified architecture (Cycle 15): circuit breaker removed.
Heartbeat handles reconnection. Tests focus on: retry on connection failure,
session timeout, DomainReloadError, auto-reconnect on send.
"""
import asyncio
import json
import struct
import time
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge, DomainReloadError
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble

_ORIG_CONNECT = bridge_mod.CONNECT_TIMEOUT
_ORIG_SESSION = bridge_mod.SESSION_TIMEOUT
_ORIG_RETRIES = bridge_mod.MAX_RETRIES


@pytest.fixture(autouse=True)
def _fast_timeouts():
    bridge_mod.CONNECT_TIMEOUT = 0.05
    bridge_mod.SESSION_TIMEOUT = 3.0
    bridge_mod.MAX_RETRIES = 3
    yield
    bridge_mod.CONNECT_TIMEOUT = _ORIG_CONNECT
    bridge_mod.SESSION_TIMEOUT = _ORIG_SESSION
    bridge_mod.MAX_RETRIES = _ORIG_RETRIES


def _make_ok_response(msg_id="0001"):
    r = {"id": msg_id, "ok": True, "data": "ok"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def _make_busy_probe(remaining=2.0):
    from unity_mcp.compile_state import CompileStateProbe
    p = MagicMock(spec=CompileStateProbe)
    p.is_unity_busy.return_value = True
    p.has_strong_busy_signal.return_value = True
    p.estimated_remaining_s.return_value = remaining
    p.has_project = True
    p.mark_recompile_issued = MagicMock()
    p.is_process_dead = MagicMock(return_value=False)
    return p


# ---------------------------------------------------------------------------
# 1. Domain reload: retry succeeds on 2nd attempt
# ---------------------------------------------------------------------------

async def test_domain_reload_retry_succeeds():
    """First attempt fails (server down), second succeeds (server restarted)."""
    call_count = 0
    probe = _make_busy_probe(remaining=1.0)

    async def mock_open(host, port):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            raise ConnectionRefusedError("server down during reload")
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[
            *reconnect_preamble(), hdr, pay,
        ])
        return reader, make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
        result = await bridge.send("test", {})

    assert result["ok"] is True
    assert call_count >= 2, "Should have retried"


# ---------------------------------------------------------------------------
# 2. Domain reload: retry succeeds on 3rd attempt
# ---------------------------------------------------------------------------

async def test_domain_reload_retry_3rd_attempt():
    """First two fail, third succeeds."""
    bridge_mod.SESSION_TIMEOUT = 5.0
    call_count = 0
    probe = _make_busy_probe(remaining=0.05)

    async def mock_open(host, port):
        nonlocal call_count
        call_count += 1
        if call_count <= 2:
            raise ConnectionRefusedError("still reloading")
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[
            *reconnect_preamble(), hdr, pay,
        ])
        return reader, make_writer()

    with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
        with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
            bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
            result = await bridge.send("test", {})

    assert result["ok"] is True
    assert call_count >= 3


# ---------------------------------------------------------------------------
# 3. Dead Unity (probe idle): fails after grace retries
# ---------------------------------------------------------------------------

async def test_dead_unity_fails_fast():
    """Probe says idle (not compiling) → fail after grace retries, not too long."""
    probe = make_idle_probe()

    async def mock_open(host, port):
        raise ConnectionRefusedError("unity is dead")

    with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
        with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
            bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
            with pytest.raises(ConnectionError):
                await bridge.send("test", {})


# ---------------------------------------------------------------------------
# 6. Session timeout aborts retries during long reload
# ---------------------------------------------------------------------------

async def test_session_timeout_during_reload():
    """Even during domain reload, SESSION_TIMEOUT limits total wait."""
    bridge_mod.SESSION_TIMEOUT = 0.5
    probe = _make_busy_probe(remaining=0.05)

    async def mock_open(host, port):
        raise ConnectionRefusedError("always fails")

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
        start = time.monotonic()
        with pytest.raises((TimeoutError, ConnectionError)):
            await bridge.send("test", {})
        elapsed = time.monotonic() - start

    assert elapsed < 5.0, f"SESSION_TIMEOUT should cap, took {elapsed:.2f}s"


# ---------------------------------------------------------------------------
# 7. Successful reconnect clears failure state
# ---------------------------------------------------------------------------

async def test_successful_reconnect_resets_state():
    """After failed attempt → successful retry, first_failure_ts resets."""
    call_count = 0
    probe = _make_busy_probe(remaining=0.5)

    async def mock_open(host, port):
        nonlocal call_count
        call_count += 1
        if call_count == 1:
            raise ConnectionRefusedError("reload")
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[
            *reconnect_preamble(), hdr, pay,
        ])
        return reader, make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
        result = await bridge.send("test", {})

    assert result["ok"] is True
    assert bridge._first_failure_ts is None


# ---------------------------------------------------------------------------
# D1. DomainReloadError is a ConnectionError subclass
# ---------------------------------------------------------------------------

def test_domain_reload_error_is_connection_error():
    assert issubclass(DomainReloadError, ConnectionError)


# ---------------------------------------------------------------------------
# D2. _read_response raises DomainReloadError on going_away frame
# ---------------------------------------------------------------------------

async def test_read_response_raises_domain_reload_on_going_away():
    payload = json.dumps({"ev": "going_away", "reason": "domain_reload"}).encode()
    header = struct.pack("!I", len(payload))
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[header, payload])
    bridge = UnityBridge("127.0.0.1", 9999)
    bridge._reader = reader
    with pytest.raises(DomainReloadError):
        await bridge._read_response()


# ---------------------------------------------------------------------------
# D3. going_away forces retry → second attempt succeeds
# ---------------------------------------------------------------------------

async def test_domain_reload_forces_retry():
    conn_count = 0
    probe = make_idle_probe()

    def going_away_frame():
        p = json.dumps({"ev": "going_away", "reason": "domain_reload"}).encode()
        return struct.pack("!I", len(p)), p

    async def mock_open(host, port):
        nonlocal conn_count
        conn_count += 1
        if conn_count == 1:
            ga_hdr, ga_pay = going_away_frame()
            reader = AsyncMock()
            reader.readexactly = AsyncMock(side_effect=[ga_hdr, ga_pay])
            return reader, make_writer()
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), hdr, pay])
        return reader, make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
        result = await bridge.send("test", {})

    assert result["ok"] is True
    assert conn_count >= 2


# ---------------------------------------------------------------------------
# D4. IncompleteReadError → retries then fails
# ---------------------------------------------------------------------------

async def test_regular_incomplete_read_still_works():
    """IncompleteReadError (not going_away) → retries then fails."""
    probe = make_idle_probe()

    async def mock_open(host, port):
        reader = AsyncMock()
        reader.readexactly = AsyncMock(
            side_effect=asyncio.IncompleteReadError(b"", 4)
        )
        return reader, make_writer()

    with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
        with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
            bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
            with pytest.raises(ConnectionError):
                await bridge.send("test", {})


# ---------------------------------------------------------------------------
# 10. Auto-reconnect: bridge reconnects on send when disconnected
# ---------------------------------------------------------------------------

async def test_send_auto_reconnects_transparently():
    """send() reconnects automatically when writer is closed."""
    probe = make_idle_probe()
    call_count = 0

    async def mock_open(host, port):
        nonlocal call_count
        call_count += 1
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[
            *reconnect_preamble(), hdr, pay,
        ])
        return reader, make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
        bridge._writer = MagicMock()
        bridge._writer.is_closing.return_value = True  # marks as disconnected

        result = await bridge.send("test", {})

    assert result["ok"] is True
    assert call_count >= 1, "Should have reconnected via open_connection"


# ---------------------------------------------------------------------------
# 9. ConnectionError becomes ToolError in server
# ---------------------------------------------------------------------------

async def test_connection_error_becomes_tool_error():
    """bridge.send() ConnectionError must be caught by _send_raw → ToolError."""
    from mcp.server.fastmcp.exceptions import ToolError
    from unity_mcp.server import _send_raw
    import unity_mcp.server as server_mod

    class FakeBridge:
        connected = False
        async def send(self, cmd, args, timeout=30.0):
            raise ConnectionError("Unity dead")

    class FakeSlot:
        bridge = FakeBridge()
        def get(self, name):
            return None
        def list(self):
            return {}

    old_slot = server_mod.slot
    server_mod.slot = FakeSlot()
    try:
        with pytest.raises(ToolError, match="connection lost|Unity"):
            await _send_raw("test", {})
    finally:
        server_mod.slot = old_slot


# ---------------------------------------------------------------------------
# G1. Grace timer resets while Unity is busy (domain reload)
#
# Bug scenario: Unity busy for 200s → becomes not-busy → grace should NOT
# immediately expire because _reconnect_started_at was never reset during busy.
# Fix: reset _reconnect_started_at each tick while busy so elapsed is measured
# from the moment Unity became idle, not from initial disconnect.
# ---------------------------------------------------------------------------

async def test_grace_not_expired_after_becoming_idle_post_busy():
    """After long busy period, grace countdown starts fresh when Unity goes idle."""
    bridge = UnityBridge("127.0.0.1", 9999)
    bridge._writer = None
    bridge._startup_grace_expired = False

    now = 1000.0  # fixed base time
    # Each tick now makes up to 4 monotonic() calls:
    #   (a) set _reconnect_started_at (first tick only)
    #   (b) set _hard_deadline_started_at (first tick only — never reset while busy)
    #   (c) busy=True → reset _reconnect_started_at
    #   (d) elapsed = monotonic() - _reconnect_started_at
    #   (e) hard_elapsed = monotonic() - _hard_deadline_started_at
    time_seq = iter([
        1000.0,  # tick 1(a): set _reconnect_started_at = 1000
        1000.0,  # tick 1(b): set _hard_deadline_started_at = 1000
        1000.0,  # tick 1(c): busy → reset _reconnect_started_at = 1000
        1000.0,  # tick 1(d): elapsed = 1000-1000 = 0s (under grace)
        1000.0,  # tick 1(e): hard_elapsed = 1000-1000 = 0s (under hard deadline)
        1200.0,  # tick 2(c): busy → reset _reconnect_started_at = 1200
        1200.0,  # tick 2(d): elapsed = 1200-1200 = 0s (just reset)
        1200.0,  # tick 2(e): hard_elapsed = 1200-1000 = 200s (under 450s deadline)
        1250.0,  # tick 3(d): elapsed = 1250-1200 = 50s < 90s → no grace latch
        1250.0,  # tick 3(e): hard_elapsed = 1250-1000 = 250s < 450s → no hard latch
    ])

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", side_effect=lambda: next(time_seq)):
        with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock):
            with patch.object(bridge, "_reconnect_cooldown_ok", return_value=False):
                # tick 1: busy
                bridge._probe_busy = Mock(return_value=True)
                await bridge._heartbeat_tick(15.0)
                # tick 2: busy (200s later in wall time, but timer should reset)
                await bridge._heartbeat_tick(15.0)
                # tick 3: NOT busy, only 50s since last reset → should NOT latch
                bridge._probe_busy = Mock(return_value=False)
                await bridge._heartbeat_tick(15.0)

    assert bridge._startup_grace_expired is False, \
        "Grace should NOT expire — only 50s elapsed since Unity became idle"


# ---------------------------------------------------------------------------
# G2. Grace expires normally when Unity is NOT busy
# ---------------------------------------------------------------------------

async def test_grace_expires_when_not_busy():
    """Grace timer expires after STARTUP_GRACE_S when probe says NOT busy."""
    bridge = UnityBridge("127.0.0.1", 9999)
    bridge._writer = None
    bridge._startup_grace_expired = False

    now = 1000.0
    # tick 1: set _reconnect_started_at=1000, not busy
    # tick 2: elapsed = 100s > STARTUP_GRACE_S=90s, not busy → latch
    time_seq = iter([1000.0, 1000.0, 1100.0, 1100.0])

    with patch("unity_mcp.bridge_heartbeat.time.monotonic", side_effect=lambda: next(time_seq, 1100.0)):
        with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock):
            with patch.object(bridge, "_probe_busy", return_value=False):
                with patch.object(bridge, "_reconnect_cooldown_ok", return_value=False):
                    await bridge._heartbeat_tick(15.0)  # sets _reconnect_started_at=1000
                    await bridge._heartbeat_tick(15.0)  # elapsed=100s > 90s → latch

    assert bridge._startup_grace_expired is True, \
        "Grace SHOULD expire after 90s when Unity is not busy"


# ---------------------------------------------------------------------------
# Reconnect spam fix: send() internal _reconnect must NOT fire callbacks
# ---------------------------------------------------------------------------

async def test_send_reconnect_does_not_fire_callbacks():
    """send()'s internal reconnect passes fire_callbacks=False — no callback spam."""
    probe = make_idle_probe()
    callback = Mock()
    calls_to_reconnect = []

    async def mock_open(host, port):
        hdr, pay = _make_ok_response("0001")
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), hdr, pay])
        return reader, make_writer()

    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    bridge.add_reconnect_callback(callback)

    # Wrap _reconnect to capture fire_callbacks argument
    original_reconnect = bridge._reconnect
    async def spy_reconnect(fire_callbacks=True):
        calls_to_reconnect.append(fire_callbacks)
        return await original_reconnect(fire_callbacks=fire_callbacks)
    bridge._reconnect = spy_reconnect

    # Mark disconnected so send() triggers internal reconnect
    bridge._writer = MagicMock()
    bridge._writer.is_closing.return_value = True

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        result = await bridge.send("test", {})

    assert result["ok"] is True
    assert calls_to_reconnect == [False], \
        f"send() must call _reconnect(fire_callbacks=False), got {calls_to_reconnect}"
    callback.assert_not_called(), "Callback must NOT fire on send()-triggered reconnect"


async def test_heartbeat_reconnect_fires_callbacks():
    """Heartbeat _reconnect uses default fire_callbacks=True — callbacks DO fire."""
    probe = make_idle_probe()
    callback = Mock()
    calls_to_reconnect = []

    async def mock_open(host, port):
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
        return reader, make_writer()

    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    bridge.add_reconnect_callback(callback)

    original_reconnect = bridge._reconnect
    async def spy_reconnect(fire_callbacks=True):
        calls_to_reconnect.append(fire_callbacks)
        return await original_reconnect(fire_callbacks=fire_callbacks)
    bridge._reconnect = spy_reconnect

    bridge._writer = None  # disconnected

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        with patch.object(bridge, "_reconnect_cooldown_ok", return_value=True):
            with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", new_callable=AsyncMock):
                with patch("unity_mcp.bridge_heartbeat.time.monotonic", return_value=1000.0):
                    with patch.object(bridge, "_probe_busy", return_value=False):
                        await bridge._heartbeat_tick(15.0)

    assert calls_to_reconnect[-1] == True, \
        "Heartbeat must call _reconnect(fire_callbacks=True) as final call"
    callback.assert_called_once()


def test_min_reconnect_interval_default_is_5s():
    """MIN_RECONNECT_INTERVAL default must be 5.0 (was 2.0) — defense in depth."""
    assert bridge_mod.MIN_RECONNECT_INTERVAL == 5.0


# ---------------------------------------------------------------------------
# Role in reconnect ping (transparent connection labels)
# ---------------------------------------------------------------------------

async def _run_reconnect_and_capture_ping(monkeypatch, env_overrides=None):
    """Helper: run _reconnect with mocked TCP; return parsed ping JSON."""
    if env_overrides:
        for k, v in env_overrides.items():
            monkeypatch.setenv(k, v)

    written_chunks: list[bytes] = []
    writer = make_writer()
    writer.write = Mock(side_effect=lambda data: written_chunks.append(data))

    async def mock_open(host, port):
        reader = AsyncMock()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
        return reader, writer

    bridge = UnityBridge("127.0.0.1", 9999)
    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        await bridge._reconnect(fire_callbacks=False)

    # First write() call is the ping (4-byte header + JSON body)
    ping_raw = written_chunks[0][4:]  # strip length prefix
    return json.loads(ping_raw.decode("utf-8"))


@pytest.mark.asyncio
async def test_reconnect_ping_includes_role_mcp(monkeypatch):
    """Reconnect ping must include role='mcp' by default."""
    ping = await _run_reconnect_and_capture_ping(monkeypatch)
    assert ping.get("role") == "mcp", f"Expected role=mcp, got: {ping}"
