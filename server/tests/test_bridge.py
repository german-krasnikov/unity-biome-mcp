import asyncio
import json
import struct
from unittest.mock import AsyncMock, Mock, patch

import pytest

from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble


def test_bridge_default_port():
    bridge = UnityBridge()
    assert bridge._port == 9500


def test_bridge_custom_port():
    bridge = UnityBridge(port=9600)
    assert bridge._port == 9600


def test_bridge_env_port(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_PORT", "9700")
    bridge = UnityBridge()
    assert bridge._port == 9700


def test_bridge_explicit_port_overrides_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_PORT", "9700")
    bridge = UnityBridge(port=9600)
    assert bridge._port == 9600


def test_bridge_invalid_env_port_falls_back_to_default(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_PORT", "not_a_number")
    bridge = UnityBridge()
    assert bridge._port == 9500


async def test_send_concurrent_unique_ids(mock_reader, mock_writer):
    """Concurrent sends produce unique message IDs."""
    import json, struct

    def make_response(n):
        r = {"id": f"{n:04x}", "ok": True, "data": "x"}
        p = json.dumps(r).encode()
        return [struct.pack("!I", len(p)), p]

    responses = []
    for i in range(1, 4):
        responses.extend(make_response(i))
    mock_reader.readexactly.side_effect = responses

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(mock_reader, mock_writer)):
        bridge = UnityBridge()
        await bridge.connect()
        results = await asyncio.gather(
            bridge.send("cmd1", {}),
            bridge.send("cmd2", {}),
            bridge.send("cmd3", {}),
        )

    calls = mock_writer.write.call_args_list
    ids = [json.loads(c[0][0][4:])["id"] for c in calls]
    assert len(set(ids)) == 3, f"Expected 3 unique IDs, got: {ids}"


class TestUnityBridge:
    async def test_send_encodes_header_as_big_endian_uint32(
        self, mock_reader, mock_writer
    ):
        """Verify 4-byte BE header is written correctly."""
        with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            # Mock response
            response = {"id": "0001", "ok": True, "data": "test"}
            response_payload = json.dumps(response).encode("utf-8")
            response_header = struct.pack("!I", len(response_payload))
            mock_reader.readexactly.side_effect = [
                response_header,
                response_payload,
            ]

            await bridge.send("test_cmd", {})

            # Verify header is big-endian uint32
            written = mock_writer.write.call_args[0][0]
            header = written[:4]
            payload = written[4:]

            # Header should be 4 bytes
            assert len(header) == 4

            # Unpack as big-endian
            length = struct.unpack("!I", header)[0]
            assert length == len(payload)

    async def test_send_encodes_payload_as_utf8_json(
        self, mock_reader, mock_writer
    ):
        """Verify payload is UTF-8 encoded JSON."""
        with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            # Mock response
            response = {"id": "0001", "ok": True, "data": "test"}
            response_payload = json.dumps(response).encode("utf-8")
            response_header = struct.pack("!I", len(response_payload))
            mock_reader.readexactly.side_effect = [
                response_header,
                response_payload,
            ]

            await bridge.send("test_cmd", {"arg": "value"})

            # Extract payload
            written = mock_writer.write.call_args[0][0]
            payload = written[4:]

            # Decode and parse
            decoded = json.loads(payload.decode("utf-8"))

            assert decoded["cmd"] == "test_cmd"
            assert decoded["args"] == {"arg": "value"}
            assert "id" in decoded

    async def test_send_includes_incremental_id(self, mock_reader, mock_writer):
        """Verify hex IDs increment correctly."""
        with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            # Mock responses
            def make_response(msg_id):
                response = {"id": msg_id, "ok": True}
                payload = json.dumps(response).encode("utf-8")
                header = struct.pack("!I", len(payload))
                return [header, payload]

            mock_reader.readexactly.side_effect = (
                make_response("0001") + make_response("0002") + make_response("0003")
            )

            # Send 3 commands
            await bridge.send("cmd1", {})
            await bridge.send("cmd2", {})
            await bridge.send("cmd3", {})

            # Extract IDs from all calls
            calls = mock_writer.write.call_args_list
            ids = []
            for call in calls:
                written = call[0][0]
                payload = written[4:]
                decoded = json.loads(payload.decode("utf-8"))
                ids.append(decoded["id"])

            assert ids == ["0001", "0002", "0003"]

    async def test_read_response_decodes_json(self, mock_reader, mock_writer):
        """Verify response parsing works."""
        with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            # Mock response
            response = {"id": "0001", "ok": True, "data": "hierarchy"}
            response_payload = json.dumps(response).encode("utf-8")
            response_header = struct.pack("!I", len(response_payload))
            mock_reader.readexactly.side_effect = [
                response_header,
                response_payload,
            ]

            result = await bridge.send("get_hierarchy", {})

            assert result["ok"] is True
            assert result["data"] == "hierarchy"

    async def test_message_too_large_raises_error(self, mock_reader, mock_writer):
        """Verify >10MB message raises ConnectionError (circuit breaker trips)."""
        import unity_mcp.bridge as bmod
        orig = bmod.SESSION_TIMEOUT
        bmod.SESSION_TIMEOUT = 0.1
        idle_probe = make_idle_probe()
        try:
            with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
                bridge = UnityBridge(probe=idle_probe)
                await bridge.connect()

                too_large = 10_000_001
                response_header = struct.pack("!I", too_large)
                mock_reader.readexactly.side_effect = [response_header]

                with pytest.raises((ConnectionError, TimeoutError)):
                    await bridge.send("test", {})
        finally:
            bmod.SESSION_TIMEOUT = orig

    async def test_close_cleans_up_writer(self, mock_reader, mock_writer):
        """Verify writer.close() and wait_closed() are called."""
        with patch("asyncio.open_connection", return_value=(mock_reader, mock_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            await bridge.close()

            mock_writer.close.assert_called_once()
            mock_writer.wait_closed.assert_called_once()

    async def test_send_fails_fast_on_connection_error(self):
        """Circuit breaker: first ConnectionError raises immediately (no retry)."""
        first_reader = AsyncMock()
        first_writer = make_writer()
        first_writer.write = Mock(side_effect=ConnectionError("Connection lost"))

        with patch("unity_mcp.bridge.asyncio.open_connection",
                   return_value=(first_reader, first_writer)):
            bridge = UnityBridge()
            await bridge.connect()

            with pytest.raises(ConnectionError):
                await bridge.send("test", {})

    async def test_send_raises_on_connection_error(self):
        """Circuit breaker: raises ConnectionError on write failure."""
        from unittest.mock import MagicMock

        idle_probe = make_idle_probe()

        def create_failing_mock():
            reader = AsyncMock()
            writer = make_writer()
            writer.write = Mock(side_effect=ConnectionError("Connection lost"))
            return (reader, writer)

        with patch("unity_mcp.bridge.asyncio.open_connection",
                   return_value=create_failing_mock()):
            bridge = UnityBridge(probe=idle_probe)
            await bridge.connect()

            with pytest.raises(ConnectionError):
                await bridge.send("test", {})

    async def test_bridge_auto_retry_on_retry_hint(self):
        """Bridge auto-waits and retries on retry hint."""
        reader = AsyncMock()
        writer = make_writer()

        # First response: busy with retry hint
        busy_response = {"id": "0001", "ok": False, "err": "Unity is compiling", "retry": 100}
        busy_payload = json.dumps(busy_response).encode("utf-8")
        busy_header = struct.pack("!I", len(busy_payload))

        # Second response: success
        ok_response = {"id": "0001", "ok": True, "data": "pong"}
        ok_payload = json.dumps(ok_response).encode("utf-8")
        ok_header = struct.pack("!I", len(ok_payload))

        reader.readexactly.side_effect = [
            busy_header, busy_payload,
            ok_header, ok_payload,
        ]

        with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
            with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
                # A1: hint-path retry now respects is_retry_safe -- get_hierarchy
                # is read-only so this test explicitly opts it in.
                bridge = UnityBridge(is_retry_safe=lambda cmd: True)
                await bridge.connect()
                result = await bridge.send("get_hierarchy", {})

                assert result["ok"] is True
                assert result["data"] == "pong"
                # Verify sleep was called with retry_ms / 1000
                mock_sleep.assert_called_once_with(0.1)  # 100ms = 0.1s

    async def test_bridge_no_retry_on_normal_error(self):
        """Bridge does NOT retry on normal errors (no retry field)."""
        reader = AsyncMock()
        writer = make_writer()

        error_response = {"id": "0001", "ok": False, "err": "Object not found"}
        error_payload = json.dumps(error_response).encode("utf-8")
        error_header = struct.pack("!I", len(error_payload))

        reader.readexactly.side_effect = [error_header, error_payload]

        with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
            bridge = UnityBridge()
            await bridge.connect()
            result = await bridge.send("get_hierarchy", {})

            # Should return error directly without retry
            assert result["ok"] is False
            assert "Object not found" in result["err"]

    async def test_send_raises_timeout_error_after_max_retries(self):
        """Bridge raises error after max retries on asyncio.TimeoutError."""
        call_count = 0

        def create_timeout_mock():
            reader = AsyncMock()
            writer = make_writer()
            # reconnect reads ping directly from reader — provide valid ping response
            ping_hdr, ping_pay = ping_response()
            reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
            return (reader, writer)

        async def open_connection_side_effect(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            return create_timeout_mock()

        idle_probe = make_idle_probe()

        with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_connection_side_effect):
            with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
                bridge = UnityBridge(probe=idle_probe)
                await bridge.connect()
                # Patch _read_response to raise TimeoutError for normal sends
                with patch.object(bridge, "_read_response", side_effect=asyncio.TimeoutError):
                    with pytest.raises((TimeoutError, ConnectionError), match="Unity not responding"):
                        await bridge.send("test", {})

    async def test_bridge_retry_respects_max_retries(self):
        """Bridge doesn't retry forever on compilation busy."""
        reader = AsyncMock()
        writer = make_writer()

        # Always return busy response (simulates endless compilation)
        busy_response = {"id": "0001", "ok": False, "err": "Unity is compiling", "retry": 50}
        busy_payload = json.dumps(busy_response).encode("utf-8")
        busy_header = struct.pack("!I", len(busy_payload))

        # Return busy 5 times (initial + 3 retries + extra to be safe)
        reader.readexactly.side_effect = [
            busy_header, busy_payload,
            busy_header, busy_payload,
            busy_header, busy_payload,
            busy_header, busy_payload,
            busy_header, busy_payload,
        ]

        with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
            with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock) as mock_sleep:
                # A1: hint-path retry now respects is_retry_safe -- get_hierarchy
                # is read-only so this test explicitly opts it in.
                bridge = UnityBridge(is_retry_safe=lambda cmd: True)
                await bridge.connect()
                result = await bridge.send("get_hierarchy", {})

                assert result["ok"] is False
                assert "compiling" in result["err"]
                assert mock_sleep.call_count == 3


# ── Tier 2a: Grace retries for non-busy ──────────────────────────────────────

async def test_idle_retry_gets_one_grace_attempt():
    """Non-busy disconnect retries once (1 grace) before giving up."""
    call_count = 0
    idle_probe = make_idle_probe()

    async def failing_open(*args, **kwargs):
        nonlocal call_count
        call_count += 1
        reader = AsyncMock()
        writer = make_writer()
        # reconnect reads ping directly from reader — provide valid ping response
        ping_hdr, ping_pay = ping_response()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
        return reader, writer

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=failing_open):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=idle_probe)
            await bridge.connect()
            with patch.object(bridge, "_read_response", side_effect=ConnectionError("lost")):
                with pytest.raises(ConnectionError):
                    await bridge.send("test", {})

    # attempt 0 → 1 grace retry → attempt 1 → give up (idle probe, no more retries)
    # initial connect + 1 retry = 2 open_connection calls
    assert call_count == 2  # initial connect + 1 grace retry


# ── Fix A: DomainReloadError pins busy for all retries ───────────────────────

def _make_going_away_frame() -> bytes:
    """Minimal going_away frame that triggers DomainReloadError in _read_response."""
    msg = json.dumps({"ev": "going_away", "reason": "domain_reload"}).encode()
    return struct.pack("!I", len(msg)) + msg


async def test_domain_reload_pins_busy_for_all_retries():
    """DomainReloadError: probe clearing mid-retry must NOT stop retries early."""
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = False  # probe already cleared

    frame = _make_going_away_frame()

    call_count = 0

    async def fake_open(*a, **kw):
        nonlocal call_count
        call_count += 1
        reader = AsyncMock()
        writer = make_writer()
        if call_count == 1:
            # First call: server sends going_away → DomainReloadError
            writer.get_extra_info = Mock(return_value=None)
            reader.readexactly.side_effect = [frame[:4], frame[4:]]
        else:
            # Subsequent retries: connection refused
            raise ConnectionRefusedError("refused")
        return reader, writer

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=fake_open):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, TimeoutError)):
                await bridge.send("run_tests", {})

    # domain_reload_in_progress=True must keep retrying past the first ConnectionRefused
    # Initial connect + at least MAX_RETRIES more attempts
    from unity_mcp.bridge import MAX_RETRIES
    assert call_count >= MAX_RETRIES + 1, (
        f"Expected >={MAX_RETRIES + 1} open_connection calls (domain reload pinned), got {call_count}"
    )


async def test_non_domain_reload_does_not_pin_busy():
    """Non-DomainReloadError: probe returning False stops retries at grace attempt."""
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = False  # always idle

    call_count = 0

    async def fake_open(*a, **kw):
        nonlocal call_count
        call_count += 1
        reader = AsyncMock()
        writer = make_writer()
        # reconnect reads ping directly from reader — provide valid ping response
        ping_hdr, ping_pay = ping_response()
        reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
        return reader, writer

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=fake_open):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with patch.object(bridge, "_read_response", side_effect=ConnectionError("plain")):
                with pytest.raises(ConnectionError):
                    await bridge.send("test", {})

    # idle probe: attempt 0 → 1 grace → give up = 2 open_connection calls total
    assert call_count == 2, (
        f"Expected 2 open_connection calls (grace only), got {call_count}"
    )


# ── Tier 2c: Heartbeat ────────────────────────────────────────────────────────

async def test_heartbeat_detects_zombie_connection():
    """Two consecutive ping timeouts → connection closed (no reconnect)."""
    reader = AsyncMock()
    writer = make_writer()
    closed_event = asyncio.Event()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        orig_close = bridge.close
        async def close_and_signal():
            await orig_close()
            closed_event.set()

        # _raw_ping always times out; prevent reconnect so closed state persists
        with patch.object(bridge, "_raw_ping", side_effect=asyncio.TimeoutError()), \
             patch.object(bridge, "_reconnect", side_effect=ConnectionError("no reconnect")), \
             patch.object(bridge, "close", side_effect=close_and_signal):
            bridge.start_heartbeat(interval=0.01)
            await asyncio.wait_for(closed_event.wait(), timeout=1.0)
            bridge.stop_heartbeat()

    assert not bridge.connected


async def test_heartbeat_reconnects_when_disconnected():
    """Heartbeat attempts reconnect when not connected and not busy."""
    reader = AsyncMock()
    writer = make_writer()
    idle_probe = make_idle_probe()
    reconnect_calls = [0]
    original_sleep = asyncio.sleep

    async def fast_sleep(t, *a, **kw):
        await original_sleep(0.005)

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=idle_probe)
        bridge._last_reconnect_at = 0.0
        async def mock_reconnect():
            reconnect_calls[0] += 1

        with patch.object(bridge, "_reconnect", side_effect=mock_reconnect), \
             patch("unity_mcp.bridge.asyncio.sleep", side_effect=fast_sleep):
            bridge.start_heartbeat(interval=0.01)
            await original_sleep(0.08)
            bridge.stop_heartbeat()

    assert reconnect_calls[0] >= 1


async def test_heartbeat_reconnects_when_busy():
    """Heartbeat calls _reconnect() even when probe is busy (probe controls timing only)."""
    reader = AsyncMock()
    writer = make_writer()
    busy_probe = make_idle_probe()
    busy_probe.has_strong_busy_signal.return_value = True
    reconnect_calls = [0]
    original_sleep = asyncio.sleep

    async def fast_sleep(t, *a, **kw):
        await original_sleep(0.005)

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=busy_probe)
        bridge._last_reconnect_at = 0.0

        async def mock_reconnect():
            reconnect_calls[0] += 1

        with patch.object(bridge, "_reconnect", side_effect=mock_reconnect), \
             patch("unity_mcp.bridge.asyncio.sleep", side_effect=fast_sleep):
            bridge.start_heartbeat(interval=0.01)
            await original_sleep(0.08)
            bridge.stop_heartbeat()

    assert reconnect_calls[0] >= 1


async def test_heartbeat_stops_on_close():
    """stop_heartbeat cancels the background task."""
    reader = AsyncMock()
    writer = make_writer()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        ping_calls = [0]

        async def counting_ping(timeout=10.0):
            ping_calls[0] += 1

        with patch.object(bridge, "_raw_ping", side_effect=counting_ping):
            bridge.start_heartbeat(interval=0.02)
            await asyncio.sleep(0.08)
            calls_before = ping_calls[0]
            bridge.stop_heartbeat()
            await asyncio.sleep(0.08)
            calls_after = ping_calls[0]

    # After stop, no more pings
    assert calls_after == calls_before


# ── FIX 4: Heartbeat 5s default + immediate close on dead PID ────────────────

def test_heartbeat_default_interval_is_15():
    """start_heartbeat default interval is 15.0 (reverted from 5.0 — too aggressive)."""
    import inspect
    from unity_mcp.bridge import UnityBridge
    sig = inspect.signature(UnityBridge.start_heartbeat)
    assert sig.parameters["interval"].default == 15.0


async def test_heartbeat_immediate_close_when_pid_dead():
    """Single ping failure + PID dead → close after 1 failure (not 2)."""
    reader = AsyncMock()
    writer = make_writer()
    closed_event = asyncio.Event()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        orig_close = bridge.close
        async def close_and_signal():
            await orig_close()
            closed_event.set()

        with patch.object(bridge, "_raw_ping", side_effect=asyncio.TimeoutError()), \
             patch.object(bridge._probe, "is_process_dead", return_value=True), \
             patch.object(bridge, "_reconnect", side_effect=ConnectionError("no reconnect")), \
             patch.object(bridge, "close", side_effect=close_and_signal):
            bridge.start_heartbeat(interval=0.01)
            await asyncio.wait_for(closed_event.wait(), timeout=1.0)
            bridge.stop_heartbeat()

    # Closed after 1 failure (ping_failures == 1, but is_process_dead → immediate)
    assert not bridge.connected


def test_describe_failure_reports_crash_when_pid_dead():
    """When PID is dead, _describe_failure mentions 'crashed'."""
    bridge = UnityBridge(port=9500)
    with patch.object(bridge._probe, "is_process_dead", return_value=True):
        msg = bridge._describe_failure("ping", ConnectionError("timeout"))
    assert "crash" in msg.lower()


# ── Tier 2c: Reconnect cooldown + raw ping ──────────────────────────────────


def test_reconnect_cooldown_default_5s():
    """MIN_RECONNECT_INTERVAL defaults to 5.0s (increased from 2.0 to reduce reconnect spam)."""
    from unity_mcp.bridge import MIN_RECONNECT_INTERVAL
    assert MIN_RECONNECT_INTERVAL == 5.0


def test_reconnect_cooldown_blocks_rapid_reconnect():
    """_reconnect_cooldown_ok() returns False within cooldown window."""
    import time as _time
    bridge = UnityBridge(port=9500)
    bridge._last_reconnect_at = _time.monotonic()  # just happened
    assert not bridge._reconnect_cooldown_ok()


def test_reconnect_cooldown_allows_after_interval():
    """_reconnect_cooldown_ok() returns True after enough time."""
    import time as _time
    bridge = UnityBridge(port=9500)
    bridge._last_reconnect_at = _time.monotonic() - 10.0  # 10s ago
    assert bridge._reconnect_cooldown_ok()


async def test_heartbeat_respects_reconnect_cooldown():
    """Heartbeat skips reconnect if cooldown hasn't elapsed."""
    import time as _time
    reader = AsyncMock()
    writer = make_writer()
    idle_probe = make_idle_probe()
    reconnect_calls = [0]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=idle_probe)
        bridge._last_reconnect_at = _time.monotonic()  # just reconnected

        async def mock_reconnect():
            reconnect_calls[0] += 1

        with patch.object(bridge, "_reconnect", side_effect=mock_reconnect):
            bridge.start_heartbeat(interval=0.01)
            await asyncio.sleep(0.08)
            bridge.stop_heartbeat()

    # Should NOT have reconnected due to cooldown
    assert reconnect_calls[0] == 0


async def test_raw_ping_bypasses_send_retry():
    """_raw_ping sends directly without going through send() retry logic."""
    import json as _json
    import struct as _struct
    reader = AsyncMock()
    writer = make_writer()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        # Make _read_response return a valid pong
        async def mock_read():
            return {"id": f"hb{bridge._counter:04x}", "ok": True, "data": "pong"}

        with patch.object(bridge, "_read_response", side_effect=mock_read):
            await bridge._raw_ping(timeout=5.0)
            # Should have written something
            assert writer.write.called
            call_bytes = writer.write.call_args[0][0]
            length = struct.unpack("!I", call_bytes[:4])[0]
            assert b'"cmd": "ping"' in call_bytes[4:4 + length]


async def test_raw_ping_raises_on_disconnected():
    """_raw_ping raises ConnectionError if not connected."""
    bridge = UnityBridge(port=9500)
    with pytest.raises(ConnectionError, match="Not connected"):
        await bridge._raw_ping()


async def test_heartbeat_immediate_close_on_domain_reload_error():
    """DomainReloadError during heartbeat ping → close immediately (no 2-failure wait)."""
    from unity_mcp.bridge import DomainReloadError
    reader = AsyncMock()
    writer = make_writer()
    closed_event = asyncio.Event()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        orig_close = bridge.close
        async def close_and_signal():
            await orig_close()
            closed_event.set()

        with patch.object(bridge, "_raw_ping", side_effect=DomainReloadError("reload")), \
             patch.object(bridge, "close", side_effect=close_and_signal):
            bridge.start_heartbeat(interval=0.01)
            await asyncio.wait_for(closed_event.wait(), timeout=1.0)
            bridge.stop_heartbeat()

    # Closed after FIRST failure (DomainReloadError = immediate close)
    assert closed_event.is_set()


async def test_start_heartbeat_restarts_dead_task():
    """start_heartbeat() starts a new task when old one is done."""
    bridge = UnityBridge(port=9500)
    bridge._heartbeat_task = asyncio.ensure_future(asyncio.sleep(0))
    await asyncio.sleep(0.01)
    assert bridge._heartbeat_task.done()
    bridge.start_heartbeat()
    assert bridge._heartbeat_task is not None
    assert not bridge._heartbeat_task.done()
    bridge.stop_heartbeat()


async def test_heartbeat_survives_tick_exception():
    """Heartbeat loop continues after non-CancelledError exception in tick."""
    tick_count = [0]
    original_sleep = asyncio.sleep

    async def counting_tick(interval):
        tick_count[0] += 1
        await original_sleep(0.005)  # yield so cancel() can land
        if tick_count[0] == 1:
            raise RuntimeError("unexpected boom")

    async def fast_sleep(t, *a, **kw):
        await original_sleep(0.005)

    bridge = UnityBridge(port=9500)
    with patch.object(bridge, "_heartbeat_tick", side_effect=counting_tick), \
         patch("unity_mcp.bridge.asyncio.sleep", side_effect=fast_sleep):
        bridge.start_heartbeat(interval=0.01)
        await original_sleep(0.1)
        bridge.stop_heartbeat()

    assert tick_count[0] >= 2, f"Loop should have continued after exception, got {tick_count[0]} ticks"


# ── Tier 2d: Callback debounce ──────────────────────────────────────────────


def test_reconnect_callback_debounce_skips_rapid_calls():
    """Debounced callback registered on bridge: rapid double-fire calls action once."""
    import time as _time
    refresh_calls = [0]
    _last_refresh_ts = [0.0]

    def _on_reconnect():
        now = _time.monotonic()
        if now - _last_refresh_ts[0] < 5.0:
            return
        _last_refresh_ts[0] = now
        refresh_calls[0] += 1

    bridge = UnityBridge(port=9500)
    bridge.add_reconnect_callback(_on_reconnect)

    for cb in bridge._on_reconnect_callbacks:
        cb()
    assert refresh_calls[0] == 1

    for cb in bridge._on_reconnect_callbacks:
        cb()
    assert refresh_calls[0] == 1


def test_reconnect_callback_debounce_allows_after_cooldown():
    """Debounced callback fires again after cooldown window expires."""
    import time as _time
    refresh_calls = [0]
    _last_refresh_ts = [0.0]

    def _on_reconnect():
        now = _time.monotonic()
        if now - _last_refresh_ts[0] < 5.0:
            return
        _last_refresh_ts[0] = now
        refresh_calls[0] += 1

    bridge = UnityBridge(port=9500)
    bridge.add_reconnect_callback(_on_reconnect)

    for cb in bridge._on_reconnect_callbacks:
        cb()
    assert refresh_calls[0] == 1

    _last_refresh_ts[0] = _time.monotonic() - 10.0
    for cb in bridge._on_reconnect_callbacks:
        cb()
    assert refresh_calls[0] == 2


# ── F04: mark_recompile_issued wired in DomainReloadError handlers ────────────

async def test_send_marks_recompile_on_domain_reload():
    """send() calls probe.mark_recompile_issued() when DomainReloadError occurs."""
    from unittest.mock import MagicMock
    from unity_mcp.bridge import DomainReloadError
    from unity_mcp.compile_state import CompileStateProbe

    idle_probe = make_idle_probe()
    reader = AsyncMock()
    writer = make_writer()
    writer.write = Mock(side_effect=DomainReloadError("going away"))

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=idle_probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, DomainReloadError)):
                await bridge.send("get_hierarchy", {})

    idle_probe.mark_recompile_issued.assert_called()


async def test_heartbeat_marks_recompile_on_domain_reload():
    """_heartbeat_tick() calls probe.mark_recompile_issued() on DomainReloadError."""
    from unity_mcp.bridge import DomainReloadError

    reader = AsyncMock()
    writer = make_writer()
    idle_probe = make_idle_probe()
    closed_event = asyncio.Event()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        orig_close = bridge.close
        async def close_and_signal():
            await orig_close()
            closed_event.set()

        with patch.object(bridge, "_raw_ping", side_effect=DomainReloadError("reload")), \
             patch.object(bridge, "_reconnect", side_effect=ConnectionError("no")), \
             patch.object(bridge, "close", side_effect=close_and_signal):
            bridge.start_heartbeat(interval=0.01)
            await asyncio.wait_for(closed_event.wait(), timeout=2.0)
            bridge.stop_heartbeat()

    idle_probe.mark_recompile_issued.assert_called()


# ── PY1.test.2: lock-held skip-ping branch ───────────────────────────────────

async def test_heartbeat_skips_ping_when_lock_held():
    """_heartbeat_tick skips _raw_ping when _lock is already held."""
    reader = AsyncMock()
    writer = make_writer()
    ping_calls = [0]

    async def counting_ping(timeout=5.0):
        ping_calls[0] += 1

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()
        bridge._raw_ping = counting_ping

        # Hold the lock while one tick fires
        await bridge._lock.acquire()
        try:
            with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
                await bridge._heartbeat_tick(0.01)
        finally:
            bridge._lock.release()

    assert ping_calls[0] == 0, "ping must not fire when _lock is held"


# ── PY1.test.3: 3 consecutive ping failures without dead PID → close ─────────

async def test_heartbeat_closes_after_3_ping_failures():
    """3 consecutive TimeoutError ping failures with dead PID → close after 3rd failure.

    TimeoutError = Unity unresponsive; 3 failures with dead process → close.
    Non-timeout exceptions (OSError etc.) close immediately on first failure.
    """
    reader = AsyncMock()
    writer = make_writer()
    close_calls = [0]

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge.connect()

        async def failing_ping(timeout=5.0):
            raise asyncio.TimeoutError("ping timed out")

        async def counting_close():
            close_calls[0] += 1

        bridge._raw_ping = failing_ping
        with patch.object(bridge._probe, "is_process_dead", return_value=True), \
             patch.object(bridge, "close", side_effect=counting_close):
            with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
                # Tick 1 — failures=1, no close
                await bridge._heartbeat_tick(0.01)
                assert close_calls[0] == 0
                # Tick 2 — failures=2, no close
                await bridge._heartbeat_tick(0.01)
                assert close_calls[0] == 0
                # Tick 3 — failures=3 >= 3 → close
                await bridge._heartbeat_tick(0.01)
                assert close_calls[0] == 1, "should close after 3rd consecutive timeout"


# ── Fix 24: bridge.py preserve exception type ────────────────────────────────

async def test_bridge_connection_error_chains_original():
    """Fix 24: ConnectionError raised in bridge.send must chain the original exception."""
    from unity_mcp.bridge import UnityBridge
    bridge = UnityBridge("127.0.0.1", 19999)  # nothing listening
    try:
        await bridge.send("ping", {}, timeout=1.0)
        pytest.fail("Expected ConnectionError")
    except (ConnectionError, TimeoutError) as ce:
        assert ce.__cause__ is not None, "Exception must chain original via 'from e'"


# ── F01-qw: ping timeout + concurrent message IDs ─────────────────────────────

def test_raw_ping_default_timeout_is_5s():
    """F01: _raw_ping default timeout must be 5.0s (reduced from 10.0)."""
    import inspect
    from unity_mcp.bridge import UnityBridge
    sig = inspect.signature(UnityBridge._raw_ping)
    assert sig.parameters["timeout"].default == 5.0


def test_heartbeat_tick_calls_raw_ping_with_5s_timeout():
    """F01: _heartbeat_tick must call _raw_ping with timeout=5 (not 20)."""
    import inspect
    from unity_mcp.bridge import UnityBridge
    src = inspect.getsource(UnityBridge._heartbeat_tick)
    assert "timeout=5" in src, "heartbeat must use timeout=5"
    assert "timeout=20" not in src, "timeout=20 must be removed"


async def test_concurrent_sends_use_unique_message_ids():
    """F01-behavioral: concurrent send() calls must get unique message IDs."""
    import asyncio
    import json
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.bridge import UnityBridge

    bridge = UnityBridge("127.0.0.1", 19998)
    sent_ids = []

    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.get_extra_info.return_value = None

    def _write(buf):
        sent_ids.append(json.loads(buf[4:])["id"])
    writer.write.side_effect = _write
    writer.drain = AsyncMock()
    bridge._writer = writer
    bridge._reader = MagicMock()

    async def _read_response():
        return {"id": sent_ids[-1], "ok": True, "data": "ok"}
    bridge._read_response = _read_response

    results = await asyncio.gather(*[bridge.send("ping", {}) for _ in range(50)])
    assert len(set(sent_ids)) == 50, "concurrent sends must get unique IDs"
    assert all(r.get("ok") for r in results)


# ---------------------------------------------------------------------------
# P7: ProtocolDesyncError + hard deadline
# ---------------------------------------------------------------------------

async def test_heartbeat_ping_mismatch_raises_protocol_desync():
    """P7: ID mismatch in _raw_ping raises ProtocolDesyncError (not generic ConnectionError)."""
    from unity_mcp.bridge_heartbeat import ProtocolDesyncError

    idle_probe = make_idle_probe()
    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=(AsyncMock(), make_writer())):
        bridge = UnityBridge()
        bridge._probe = idle_probe
        await bridge.connect()

        async def mismatched_read():
            return {"id": "wrong-id", "ok": True, "data": "pong"}

        with patch.object(bridge, "_read_response", side_effect=mismatched_read):
            with pytest.raises(ProtocolDesyncError):
                await bridge._raw_ping(timeout=5.0)


async def test_heartbeat_hard_deadline_latches_grace_expired():
    """P7: HARD_DEADLINE_S reached while disconnected → _startup_grace_expired=True."""
    import time as _time
    import unity_mcp.bridge_heartbeat as _bh
    from unittest.mock import AsyncMock as _AsyncMock

    idle_probe = make_idle_probe()
    idle_probe.has_strong_busy_signal = Mock(return_value=False)  # idle — grace reset doesn't fire

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=(_AsyncMock(), make_writer())):
        bridge = UnityBridge()
        bridge._probe = idle_probe
        bridge._on_unavailable = None
        await bridge.connect()
        await bridge.close()  # start disconnected

        # Patch HARD_DEADLINE_S to near-zero so test completes quickly
        original = _bh.HARD_DEADLINE_S
        _bh.HARD_DEADLINE_S = 0.001
        try:
            # Simulate enough elapsed time by moving deadline clocks far back
            bridge._reconnect_started_at = _time.monotonic() - 1.0
            bridge._hard_deadline_started_at = _time.monotonic() - 1.0
            with patch("asyncio.sleep", new=_AsyncMock(return_value=None)):
                # One tick should latch the deadline
                await bridge._heartbeat_tick(interval=0.01)
        finally:
            _bh.HARD_DEADLINE_S = original

    assert bridge._startup_grace_expired is True


# ── BridgeState enum tests (Fix 3) ──────────────────────────────────────────

def test_bridge_state_initial_disconnected():
    from unity_mcp.bridge import BridgeState
    bridge = UnityBridge()
    assert bridge._state == BridgeState.DISCONNECTED


async def test_bridge_state_connected_after_reconnect():
    from unity_mcp.bridge import BridgeState
    ping_hdr, ping_pay = ping_response()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
    writer = make_writer()
    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge._reconnect(fire_callbacks=False)
    assert bridge._state == BridgeState.CONNECTED


async def test_bridge_state_domain_reloading(monkeypatch):
    """DomainReloadError in send() sets state to DOMAIN_RELOADING."""
    import unity_mcp.bridge as bmod
    from unity_mcp.bridge import BridgeState
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    monkeypatch.setattr(bmod, "MAX_RETRIES", 0)
    idle_probe = make_idle_probe()

    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ga_hdr, going_away])
    writer = make_writer()

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})

    assert bridge._state == BridgeState.DOMAIN_RELOADING


async def test_bridge_state_failed_on_grace_expired():
    """_startup_grace_expired=True maps to FAILED state."""
    from unity_mcp.bridge import BridgeState
    bridge = UnityBridge()
    bridge._state = BridgeState.FAILED
    assert bridge._state == BridgeState.FAILED


async def test_bridge_state_connected_clears_failed():
    """Successful _reconnect() transitions from FAILED → CONNECTED."""
    from unity_mcp.bridge import BridgeState
    ping_hdr, ping_pay = ping_response()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
    writer = make_writer()
    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        bridge._state = BridgeState.FAILED
        await bridge._reconnect(fire_callbacks=False)
    assert bridge._state == BridgeState.CONNECTED


# ── Fix 2 regression: heartbeat DomainReloadError must mark _reload ──────────

async def test_heartbeat_domain_reload_sets_reload_tracker():
    """REGRESSION: DomainReloadError in heartbeat must mark _reload so
    send() gets extended retry window (was: only set _domain_reload_in_progress
    in send() path, heartbeat path was missing the mark)."""
    import time as _time
    from unity_mcp.bridge import UnityBridge
    from unittest.mock import AsyncMock as _AsyncMock

    idle_probe = make_idle_probe()

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=(_AsyncMock(), make_writer())):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        # Simulate _raw_ping raising DomainReloadError
        from unity_mcp.bridge_socket import DomainReloadError
        with patch.object(bridge, "_raw_ping", side_effect=DomainReloadError("reload")):
            await bridge._heartbeat_tick(interval=0.0)

    # _reload tracker must be active after heartbeat catches DomainReloadError
    assert bridge._reload.is_active() is True


# ── Fix 1: retry delays must be 2s→4s→8s (not 1s→2s→4s) ────────────────────

def test_should_retry_domain_reload_delay_sequence():
    """DomainReloadError: delay must be 2**( attempt+1) → 2s, 4s, 8s."""
    from unity_mcp.bridge import DomainReloadError
    import time
    bridge = UnityBridge(probe=make_idle_probe())
    deadline = time.monotonic() + 999.0
    err = DomainReloadError("reload")
    expected = [2.0, 4.0, 8.0]
    for attempt, exp_delay in enumerate(expected):
        ok, delay, reason = bridge.should_retry(err, attempt, deadline)
        assert ok is True, f"attempt {attempt}: expected retry=True"
        assert delay == exp_delay, f"attempt {attempt}: expected delay={exp_delay}, got {delay}"


def test_should_retry_busy_delay_sequence():
    """Busy path: delay must be 2**(attempt+1) → 2s, 4s, 8s."""
    import time
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = True
    bridge = UnityBridge(probe=probe)
    deadline = time.monotonic() + 999.0
    err = ConnectionError("transient")
    expected = [2.0, 4.0, 8.0]
    for attempt, exp_delay in enumerate(expected):
        ok, delay, reason = bridge.should_retry(err, attempt, deadline)
        assert ok is True, f"attempt {attempt}: expected retry=True"
        assert delay == exp_delay, f"attempt {attempt}: expected delay={exp_delay}, got {delay}"


# ── Item 3: _send_with_retry exhausted must raise RuntimeError, not return None ─

async def test_send_with_retry_exhausted_raises():
    """Retry loop exiting with result=None must raise RuntimeError, not return None.

    Path: every attempt throws + do_retry=True → attempt increments past MAX_RETRIES
    → while-loop exits → hits `return result` with result=None.
    Fix: replace that line with `raise RuntimeError(...)`.
    """
    import unity_mcp.bridge as bmod

    bridge = UnityBridge(probe=make_idle_probe())
    writer = make_writer()
    bridge._reader = AsyncMock()
    bridge._writer = writer
    bridge._writer.is_closing = Mock(return_value=False)

    header = struct.pack("!I", 2)
    payload = b"{}"

    async def fake_reconnect(fire_callbacks=True):
        bridge._reader = AsyncMock()
        bridge._writer = writer

    async def no_sleep(delay):
        pass  # skip actual sleep to keep test fast

    # Force do_retry=True so attempt increments past MAX_RETRIES, exiting while loop
    # with result still None — the buggy `return result` returns None; fix raises RuntimeError.
    with patch.object(bmod, "MAX_RETRIES", 1):
        with patch("unity_mcp.bridge.asyncio.sleep", side_effect=no_sleep):
            with patch.object(bridge, "_reconnect", side_effect=fake_reconnect):
                with patch.object(bridge, "_read_response",
                                  side_effect=ConnectionError("persistent fail")):
                    with patch.object(bridge, "should_retry",
                                      return_value=(True, 0.0, "domain_reload")):
                        with pytest.raises((RuntimeError, ConnectionError)):
                            await bridge._send_with_retry(
                                "ping", header, payload, "0001", 5.0,
                                asyncio.get_event_loop().time() + 30.0
                            )
