"""Edge cases tests for UnityBridge."""
import asyncio
import json
import struct
import time
from unittest.mock import AsyncMock, Mock, patch, MagicMock
import pytest
from unity_mcp.bridge import UnityBridge, DOMAIN_RELOAD_EXPIRY_S
from helpers import make_writer, make_idle_probe, ping_response, reconnect_preamble, version_response


async def test_send_empty_args(mock_connection):
    """send() works with empty args dict."""
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test_cmd", {})

        assert result["ok"] is True
        written = mock_writer.write.call_args[0][0]
        payload = written[4:]
        message = json.loads(payload.decode("utf-8"))
        assert message["args"] == {}


async def test_send_unicode_args(mock_connection):
    """send() handles unicode (cyrillic, emoji) in args."""
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test", {"name": "Объект 😀", "text": "Привет"})

        assert result["ok"] is True
        written = mock_writer.write.call_args[0][0]
        payload = written[4:]
        message = json.loads(payload.decode("utf-8"))
        assert message["args"]["name"] == "Объект 😀"


async def test_read_response_invalid_json(mock_connection, monkeypatch):
    """Invalid JSON payload → ConnectionError (caught and retried like corrupt frame)."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    bad_payload = b"not json"
    header = struct.pack("!I", len(bad_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[header, bad_payload])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


async def test_read_response_zero_length(mock_connection, monkeypatch):
    """Zero-length payload → ConnectionError (caught and retried like corrupt frame)."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    header = struct.pack("!I", 0)
    empty_payload = b""

    mock_reader.readexactly = AsyncMock(side_effect=[header, empty_payload])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


async def test_read_response_exactly_10mb(mock_connection):
    """_read_response accepts a ~10MB payload (at size limit boundary)."""
    mock_reader, mock_writer = mock_connection

    filler = "x" * (10_000_000 - 40)
    response = {"id": "0001", "ok": True, "data": filler}
    resp_payload = json.dumps(response).encode("utf-8")
    header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[header, resp_payload])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        result = await bridge.send("test", {})
        assert result["ok"] is True


async def test_read_response_exceeds_10mb(mock_connection, monkeypatch):
    """Oversized message (>10MB) raises ConnectionError."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    idle_probe = make_idle_probe()

    header = struct.pack("!I", 10_000_001)
    mock_reader.readexactly = AsyncMock(side_effect=[header])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


async def test_send_rejects_oversized_payload(mock_connection):
    """send() raises ConnectionError before writing if payload > 10MB."""
    mock_reader, mock_writer = mock_connection

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        big_args = {"data": "x" * 10_000_001}
        with pytest.raises(ConnectionError, match="too large"):
            await bridge.send("test", big_args)


async def test_disconnect_during_header_read(mock_connection, monkeypatch):
    """IncompleteReadError during header read → ConnectionError after grace retry."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    mock_reader.readexactly = AsyncMock(side_effect=[
        asyncio.IncompleteReadError(b"", 4),
    ])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


async def test_disconnect_during_payload_read(mock_connection, monkeypatch):
    """IncompleteReadError during payload read → ConnectionError after grace retry."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    mock_reader, mock_writer = mock_connection

    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    mock_reader.readexactly = AsyncMock(side_effect=[
        resp_header,
        asyncio.IncompleteReadError(b"", len(resp_payload)),
    ])

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})


async def test_concurrent_sends(mock_connection):
    """Multiple concurrent send() calls are serialized by lock."""
    mock_reader, mock_writer = mock_connection

    def make_response(msg_id):
        resp = {"id": msg_id, "ok": True, "data": f"Response {msg_id}"}
        payload = json.dumps(resp).encode("utf-8")
        header = struct.pack("!I", len(payload))
        return [header, payload]

    responses = []
    for i in range(1, 6):
        responses.extend(make_response(f"{i:04x}"))

    mock_reader.readexactly = AsyncMock(side_effect=responses)

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        tasks = [bridge.send(f"cmd{i}", {}) for i in range(5)]
        results = await asyncio.gather(*tasks)

        assert len(results) == 5
        for result in results:
            assert result["ok"] is True


async def test_reconnect_preserves_counter(mock_connection):
    """_reconnect() does not reset _counter."""
    mock_reader, mock_writer = mock_connection

    response1 = {"id": "0001", "ok": True, "data": "OK"}
    resp1_payload = json.dumps(response1).encode("utf-8")
    resp1_header = struct.pack("!I", len(resp1_payload))

    # _reconnect() increments counter once for ping ID (rc0002), so cmd2 gets 0003
    response2 = {"id": "0003", "ok": True, "data": "OK"}
    resp2_payload = json.dumps(response2).encode("utf-8")
    resp2_header = struct.pack("!I", len(resp2_payload))

    ver_hdr, ver_pay = version_response()
    mock_reader.readexactly = AsyncMock(side_effect=[
        resp1_header, resp1_payload,
        *reconnect_preamble(),
        resp2_header, resp2_payload,
    ])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        await bridge.send("cmd1", {})
        assert bridge._counter == 1

        await bridge._reconnect()
        assert bridge._counter == 2  # ping consumed counter slot

        await bridge.send("cmd2", {})
        assert bridge._counter == 3


# no-assert: crash guard
async def test_close_when_not_connected():
    """close() when never connected does not crash."""
    bridge = UnityBridge()
    await bridge.close()  # Should not raise


async def test_close_when_writer_fails(mock_connection):
    """close() handles exception in wait_closed."""
    mock_reader, mock_writer = mock_connection

    mock_writer.wait_closed = AsyncMock(side_effect=Exception("Close failed"))

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()
        await bridge.connect()

        await bridge.close()  # Should not raise

        assert bridge._writer is None
        assert bridge._reader is None


async def test_send_after_close_auto_reconnects():
    """send() after close() auto-reconnects instead of crashing."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    new_reader = AsyncMock()
    new_reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), resp_header, resp_payload])
    new_writer = make_writer()

    first_reader = AsyncMock()
    first_writer = make_writer()

    with patch("asyncio.open_connection", side_effect=[
        (first_reader, first_writer),
        (new_reader, new_writer),
    ]):
        bridge = UnityBridge()
        await bridge.connect()
        await bridge.close()

        assert not bridge.connected
        result = await bridge.send("test", {})
        assert result["ok"] is True
        assert bridge.connected


async def test_send_auto_reconnects_when_never_connected():
    """send() without prior connect() auto-reconnects."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), resp_header, resp_payload])
    writer = make_writer()

    with patch("asyncio.open_connection", return_value=(reader, writer)) as mock_open:
        bridge = UnityBridge()
        # Never call connect() — send() should handle it
        assert not bridge.connected

        result = await bridge.send("test", {})

        assert result["ok"] is True
        assert bridge.connected
        # open_connection called once by auto-reconnect
        mock_open.assert_called_once()


async def test_connected_property(mock_connection):
    """connected property reflects actual state."""
    mock_reader, mock_writer = mock_connection

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge()

        assert not bridge.connected

        await bridge.connect()
        assert bridge.connected

        mock_writer.is_closing = Mock(return_value=True)
        assert not bridge.connected

        await bridge.close()
        assert not bridge.connected


async def test_max_retries_exhausted(mock_connection):
    """send() raises ConnectionError on IncompleteReadError (circuit breaker, no retry)."""
    mock_reader, mock_writer = mock_connection

    mock_reader.readexactly = AsyncMock(side_effect=asyncio.IncompleteReadError(b"", 4))

    idle_probe = make_idle_probe()

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((asyncio.IncompleteReadError, ConnectionError)):
            await bridge.send("test", {})


async def test_send_timeout_raises():
    """send() raises ConnectionError when Unity doesn't respond (circuit breaker, no retry)."""
    idle_probe = make_idle_probe()

    def create_hanging_mock():
        reader = AsyncMock()
        writer = make_writer()
        async def hang(n):
            await asyncio.Future()  # never resolves
        reader.readexactly = AsyncMock(side_effect=hang)
        return (reader, writer)

    with patch("unity_mcp.bridge.asyncio.open_connection",
               return_value=create_hanging_mock()):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((TimeoutError, ConnectionError), match="Unity not responding"):
            await bridge.send("test", {}, timeout=0.01)


async def test_concurrent_sends_with_dead_connection():
    """Concurrent sends when writer=None don't race on reconnect.

    Design note: counter increment is sync (outside I/O lock) so IDs are
    assigned before any task yields. I/O lock serialises reconnect + write +
    read. The mock echoes whatever ID it received, making the test
    order-independent — no hardcoded "0001"/"0002"/"0003".
    """
    # Queue of pre-built (header, payload) response pairs, filled on drain()
    response_queue: asyncio.Queue = asyncio.Queue()
    # Partial state for readexactly: None means next call is a header read
    pending_payload: list[bytes | None] = [None]

    async def fake_readexactly(n: int) -> bytes:
        if pending_payload[0] is not None:
            # Second call in pair: return the payload
            pay = pending_payload[0]
            pending_payload[0] = None
            return pay
        # First call in pair (n==4): get next response pair from queue
        hdr, pay = await response_queue.get()
        pending_payload[0] = pay
        return hdr

    frames_buf: list[bytes] = []

    def fake_write(data: bytes) -> None:
        frames_buf.append(data)

    async def fake_drain() -> None:
        # Parse accumulated writes, build echo responses, enqueue them
        buf = b"".join(frames_buf)
        frames_buf.clear()
        offset = 0
        while offset + 4 <= len(buf):
            length = struct.unpack("!I", buf[offset:offset + 4])[0]
            offset += 4
            if offset + length > len(buf):
                break
            frame = buf[offset:offset + length]
            offset += length
            msg = json.loads(frame)
            resp = {"id": msg["id"], "ok": True, "data": "pong" if msg.get("cmd") == "ping" else "OK"}
            pay = json.dumps(resp).encode("utf-8")
            await response_queue.put((struct.pack("!I", len(pay)), pay))

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=fake_readexactly)
    writer = make_writer()
    writer.write = Mock(side_effect=fake_write)
    writer.drain = AsyncMock(side_effect=fake_drain)

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        assert not bridge.connected

        # Fire 3 concurrent sends — all must succeed without AttributeError
        results = await asyncio.gather(
            bridge.send("cmd1", {}),
            bridge.send("cmd2", {}),
            bridge.send("cmd3", {}),
        )

        assert len(results) == 3
        for r in results:
            assert r["ok"] is True


async def test_reconnect_inside_lock_prevents_writer_none():
    """Reconnect inside lock ensures writer is set before write()."""
    response = {"id": "0001", "ok": True, "data": "OK"}
    resp_payload = json.dumps(response).encode("utf-8")
    resp_header = struct.pack("!I", len(resp_payload))

    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), resp_header, resp_payload])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        # Simulate: bridge created but never connected (lifespan failed)
        assert bridge._writer is None

        result = await bridge.send("test", {})
        assert result["ok"] is True
        # Writer must be set after auto-reconnect
        assert bridge._writer is not None


async def test_response_id_mismatch_raises(mock_connection):
    """Mismatched ID → ConnectionError('Response ID mismatch') immediately."""
    wrong_response = {"id": "wrong_id", "ok": True, "data": "x"}

    idle_probe = make_idle_probe()

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()
        with patch.object(bridge, "_read_response", AsyncMock(return_value=wrong_response)):
            with pytest.raises(ConnectionError, match="Response ID mismatch"):
                await bridge.send("test", {})


async def test_probe_raises_does_not_crash_send():
    """Probe methods that raise must not crash send() (degrade-open)."""
    from unity_mcp.compile_state import CompileStateProbe

    exploding_probe = MagicMock(spec=CompileStateProbe)
    exploding_probe.is_unity_busy.side_effect = RuntimeError("probe exploded")
    exploding_probe.has_strong_busy_signal.side_effect = RuntimeError("probe exploded")
    exploding_probe.estimated_remaining_s.side_effect = RuntimeError("probe exploded")
    exploding_probe.mark_recompile_issued.side_effect = RuntimeError("probe exploded")
    exploding_probe.is_process_dead.side_effect = RuntimeError("probe exploded")
    exploding_probe.has_project = True

    async def open_connection_side_effect(*args, **kwargs):
        reader = AsyncMock()
        writer = make_writer()
        async def hang(n):
            await asyncio.Future()
        reader.readexactly = AsyncMock(side_effect=hang)
        return (reader, writer)

    with patch("unity_mcp.bridge.asyncio.open_connection",
               side_effect=open_connection_side_effect):
        bridge = UnityBridge(probe=exploding_probe)
        await bridge.connect()

        with pytest.raises((TimeoutError, ConnectionError), match="Unity not responding"):
            await bridge.send("test", {}, timeout=0.01)


async def test_concurrent_sends_routes_per_caller(mock_unity_server):
    """Each caller's response correctly identified by msg_id (not just 'all return ok')."""
    bridge = UnityBridge(host="127.0.0.1", port=mock_unity_server.port)
    await bridge.connect()

    results = await asyncio.gather(*[
        bridge.send(f"cmd_{i}", {"marker": f"m{i}"}) for i in range(5)
    ])

    # mock_unity_server echoes as "echo:cmd_i" in data field
    for i, r in enumerate(results):
        assert f"cmd_{i}" in str(r), f"Caller {i} got wrong response: {r}"

    await bridge.close()


# ── P0: _domain_reload_in_progress sticky flag ──────────────────────────────

async def test_p0_domain_reload_flag_cleared_on_reconnect():
    """_reload tracker cleared when _reconnect() succeeds."""
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        bridge._reload.mark()

        await bridge._reconnect(fire_callbacks=False)

        assert bridge._reload.is_active() is False
        assert bridge._reload._since is None


async def test_p0_domain_reload_flag_auto_expires(monkeypatch):
    """After DOMAIN_RELOAD_EXPIRY_S, sticky flag auto-clears in send() error path."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.5)
    monkeypatch.setattr(bmod, "MAX_RETRIES", 0)
    idle_probe = make_idle_probe()

    # First call: trigger DomainReloadError to set the flag
    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    # Second call: normal OSError while flag is expired — should NOT get busy retry
    reader1 = AsyncMock()
    reader1.readexactly = AsyncMock(side_effect=[ga_hdr, going_away])
    writer1 = make_writer()

    reader2 = AsyncMock()
    reader2.readexactly = AsyncMock(side_effect=OSError("refused"))
    writer2 = make_writer()

    with patch("asyncio.open_connection", side_effect=[
        (reader1, writer1), (reader2, writer2),
    ]):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})
        # Tracker is active after DomainReloadError
        assert bridge._reload.is_active() is True

    # Now simulate 31s later — tracker should auto-expire in next send()
    bridge._reload._since = time.monotonic() - (DOMAIN_RELOAD_EXPIRY_S + 1)
    reader3 = AsyncMock()
    reader3.readexactly = AsyncMock(side_effect=OSError("refused"))
    writer3 = make_writer()
    with patch("asyncio.open_connection", return_value=(reader3, writer3)):
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test2", {})
    # After expiry, tracker should be inactive
    assert bridge._reload.is_active() is False


async def test_p0_domain_reload_flag_stays_true_within_window(monkeypatch):
    """Within 30s window, domain reload flag keeps send() in busy-retry mode."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.5)
    monkeypatch.setattr(bmod, "MAX_RETRIES", 1)
    idle_probe = make_idle_probe()

    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ga_hdr, going_away, ga_hdr, going_away])
    writer = make_writer()

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()
        # _domain_reload_since will be set to now (within window)
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})
    # Tracker stays active because we're within 30s window
    assert bridge._reload.is_active() is True
    assert bridge._reload._since is not None


async def test_p0_domain_reload_since_set_when_flag_raised(mock_connection, monkeypatch):
    """When DomainReloadError hits, _domain_reload_since is set."""
    import unity_mcp.bridge as bmod
    monkeypatch.setattr(bmod, "SESSION_TIMEOUT", 0.1)
    monkeypatch.setattr(bmod, "MAX_RETRIES", 0)
    from unity_mcp.bridge_socket import DomainReloadError

    mock_reader, mock_writer = mock_connection
    idle_probe = make_idle_probe()

    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    hdr = struct.pack("!I", len(going_away))
    mock_reader.readexactly = AsyncMock(side_effect=[hdr, going_away])

    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("test", {})

    # After the DomainReloadError path, timestamp should have been set
    # (even if flag was later cleared — we test via the probe mock call)
    idle_probe.mark_recompile_issued.assert_called_once()


# ── P2: _startup_grace_expired permanent death latch ────────────────────────

async def test_p2_startup_grace_expired_attempts_reconnect():
    """send() with _state=FAILED attempts reconnect before raising."""
    from unity_mcp.bridge import BridgeState
    ping_hdr, ping_pay = ping_response()
    # _reconnect sends a ping (counter=1, id=rc0001), then send() uses counter=2
    resp = {"id": "0002", "ok": True, "data": "ok"}
    resp_pay = json.dumps(resp).encode()
    resp_hdr = struct.pack("!I", len(resp_pay))

    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(), resp_hdr, resp_pay])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        bridge._state = BridgeState.FAILED

        result = await bridge.send("test", {})

        assert result["ok"] is True
        # state cleared to CONNECTED on reconnect success
        assert bridge._state == BridgeState.CONNECTED


async def test_p2_startup_grace_expired_raises_if_reconnect_fails():
    """send() raises ConnectionError if reconnect fails when grace expired."""
    from unity_mcp.bridge import BridgeState
    with patch("asyncio.open_connection", side_effect=ConnectionRefusedError("refused")):
        bridge = UnityBridge()
        bridge._state = BridgeState.FAILED

        with pytest.raises(ConnectionError):
            await bridge.send("test", {})


# ── Fix 4: CancelledError zombie writer ─────────────────────────────────────

async def test_cancelled_error_closes_writer(mock_connection):
    """CancelledError during send() must close writer (no zombie socket)."""
    mock_reader, mock_writer = mock_connection

    async def hang(n):
        await asyncio.Future()  # never resolves — simulates blocked _read_response

    mock_reader.readexactly = AsyncMock(side_effect=hang)

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        task = asyncio.create_task(bridge.send("test", {}, timeout=60.0))
        await asyncio.sleep(0)   # let task reach the await inside lock
        await asyncio.sleep(0)
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task

    # Writer must be closed (bridge.close() nulls _writer)
    assert bridge._writer is None


async def test_cancelled_error_reraises(mock_connection):
    """CancelledError is not swallowed — it propagates to the caller."""
    mock_reader, mock_writer = mock_connection

    async def hang(n):
        await asyncio.Future()

    mock_reader.readexactly = AsyncMock(side_effect=hang)

    idle_probe = make_idle_probe()
    with patch("asyncio.open_connection", return_value=mock_connection):
        bridge = UnityBridge(probe=idle_probe)
        await bridge.connect()

        task = asyncio.create_task(bridge.send("test", {}, timeout=60.0))
        await asyncio.sleep(0)
        await asyncio.sleep(0)
        task.cancel()

        with pytest.raises(asyncio.CancelledError):
            await task


# ── Fix 5: _reader/_writer atomic assignment in _reconnect() ─────────────────

async def test_reconnect_atomic_assignment():
    """During reconnect ping read, neither _reader nor _writer is partially set.

    The desync window is: self._reader = reader (line 241) ... await _read_response()
    ... self._writer = writer (line 253). We intercept readexactly to snapshot mid-flight.
    """
    snapshots: list[tuple] = []

    ping_hdr, ping_pay = ping_response()
    ver_hdr, ver_pay = version_response()
    reader = AsyncMock()
    writer = make_writer()

    preamble = [ping_hdr, ping_pay, ver_hdr, ver_pay]
    call_count = [0]

    async def capturing_readexactly(n: int) -> bytes:
        call_count[0] += 1
        snapshots.append((bridge._reader, bridge._writer))
        idx = call_count[0] - 1
        return preamble[idx] if idx < len(preamble) else ping_pay

    reader.readexactly = AsyncMock(side_effect=capturing_readexactly)

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge()
        await bridge._reconnect(fire_callbacks=False)

    # During ping read: _reader is set but _writer is NOT yet (desync window)
    # After fix: both should be None (or both set atomically AFTER ping)
    assert len(snapshots) >= 1
    for mid_reader, mid_writer in snapshots:
        # After fix: _reader should NOT be set during ping read (still None until atomic assign)
        assert mid_reader is None, f"_reader set during ping read (desync window): {mid_reader}"
        assert mid_writer is None, f"_writer set during ping read: {mid_writer}"

    # After success — both must be set atomically
    assert bridge._reader is reader
    assert bridge._writer is writer


async def test_reader_null_on_ping_failure():
    """If ping fails, _reader must be None (not left in temp state)."""
    new_reader = AsyncMock()
    new_reader.readexactly = AsyncMock(
        side_effect=asyncio.TimeoutError("ping timed out")
    )
    new_writer = make_writer()

    with patch("asyncio.open_connection", return_value=(new_reader, new_writer)):
        bridge = UnityBridge()
        with pytest.raises((asyncio.TimeoutError, ConnectionError, Exception)):
            await bridge._reconnect(fire_callbacks=False)

    assert bridge._reader is None
    assert bridge._writer is None


async def test_close_uses_shut_wr_on_windows(monkeypatch):
    """On Windows, close() should use SHUT_WR (not SHUT_RDWR) to avoid RST on recv buffer."""
    import socket as sock_mod
    import unity_mcp.bridge as bmod

    mock_sock = Mock()
    writer = make_writer()
    writer.get_extra_info = Mock(return_value=mock_sock)
    writer.is_closing = Mock(return_value=False)

    bridge = UnityBridge()
    bridge._writer = writer
    bridge._reader = AsyncMock()

    monkeypatch.setattr(bmod.os, "name", "nt")
    await bridge.close()

    mock_sock.shutdown.assert_called_once_with(sock_mod.SHUT_WR)


async def test_close_uses_shut_rdwr_on_non_windows(monkeypatch):
    """On non-Windows, close() should use SHUT_RDWR to unblock pending selector reads."""
    import socket as sock_mod
    import unity_mcp.bridge as bmod

    mock_sock = Mock()
    writer = make_writer()
    writer.get_extra_info = Mock(return_value=mock_sock)
    writer.is_closing = Mock(return_value=False)

    bridge = UnityBridge()
    bridge._writer = writer
    bridge._reader = AsyncMock()

    monkeypatch.setattr(bmod.os, "name", "posix")
    await bridge.close()

    mock_sock.shutdown.assert_called_once_with(sock_mod.SHUT_RDWR)


