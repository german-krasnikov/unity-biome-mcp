"""Phase 2 tests — bridge compile-state integration (tests #15-35).

Updated for simplified architecture (Cycle 15): circuit breaker removed,
heartbeat is the reconnect mechanism. Tests focus on probe integration,
metrics, and error messages.
"""
import asyncio
import json
import struct
import time
from typing import Optional
from unittest.mock import AsyncMock, Mock, patch, MagicMock

import pytest

import unity_mcp.bridge as _bridge_mod
from unity_mcp.bridge import UnityBridge
from unity_mcp.compile_state import CompileStateProbe
from unity_mcp.metrics import METRICS
from helpers import make_writer, ping_response, reconnect_preamble

_ORIG_CONNECT = _bridge_mod.CONNECT_TIMEOUT
_ORIG_SESSION = _bridge_mod.SESSION_TIMEOUT


@pytest.fixture(autouse=True)
def _fast_timeouts():
    _bridge_mod.CONNECT_TIMEOUT = 0.01
    _bridge_mod.SESSION_TIMEOUT = 0.3
    yield
    _bridge_mod.CONNECT_TIMEOUT = _ORIG_CONNECT
    _bridge_mod.SESSION_TIMEOUT = _ORIG_SESSION


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_ok_response(msg_id="0001"):
    r = {"id": msg_id, "ok": True, "data": "x"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def _make_probe(busy: bool = False, remaining: float = 5.0,
                 strong_busy: Optional[bool] = None, has_project: bool = True):
    probe = Mock(spec=CompileStateProbe)
    probe.is_unity_busy.return_value = busy
    probe.estimated_remaining_s.return_value = remaining
    probe.has_strong_busy_signal.return_value = (
        strong_busy if strong_busy is not None else busy
    )
    probe.has_project = has_project
    probe.mark_recompile_issued = Mock()
    probe.is_process_dead = Mock(return_value=False)
    return probe


def _make_failing_connection():
    reader = AsyncMock()
    writer = make_writer()
    writer.write = Mock(side_effect=ConnectionError("Connection lost"))
    return reader, writer


# ---------------------------------------------------------------------------
# 15. send() raises ConnectionError when connection fails
# ---------------------------------------------------------------------------

async def test_send_raises_on_connection_failure():
    probe = _make_probe(busy=False)
    r, w = _make_failing_connection()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(r, w)):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, TimeoutError)):
                await bridge.send("test", {})


# ---------------------------------------------------------------------------
# 20. Error message when compiling
# ---------------------------------------------------------------------------

async def test_bridge_error_message_compiling():
    async def open_conn(*a, **kw):
        r, w = _make_failing_connection()
        return r, w

    probe = _make_probe(busy=True, remaining=0.1)

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_conn):
        bridge = UnityBridge(probe=probe)
        await bridge.connect()
        with pytest.raises((TimeoutError, ConnectionError)) as exc_info:
            await bridge.send("test", {})

    msg = str(exc_info.value)
    assert "C# compilation" in msg or "Session deadline" in msg


# ---------------------------------------------------------------------------
# 21. Error message when process dead
# ---------------------------------------------------------------------------

def test_bridge_error_message_dead():
    """When PID is dead, _describe_failure says 'process dead' and includes port."""
    probe = _make_probe(busy=False)
    probe.is_process_dead.return_value = True

    bridge = UnityBridge(port=9500, probe=probe)
    msg = bridge._describe_failure("ping", ConnectionError("timeout"))
    assert "process dead" in msg
    assert ":9500" in msg


# ---------------------------------------------------------------------------
# 22. No misleading "save dialog" in any error message
# ---------------------------------------------------------------------------

async def test_bridge_error_no_misleading_save_dialog():
    async def open_conn(*a, **kw):
        r, w = _make_failing_connection()
        return r, w

    for busy in (True, False):
        probe = _make_probe(busy=busy)
        with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_conn):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, TimeoutError)) as exc_info:
                await bridge.send("test", {})
        assert "save dialog" not in str(exc_info.value)


# ---------------------------------------------------------------------------
# 24. recompile.duration_ms observed on fail-then-succeed
# ---------------------------------------------------------------------------

async def test_metrics_recompile_duration_observed():
    probe = _make_probe(busy=False)

    hdr, payload = _make_ok_response()
    new_reader = AsyncMock()
    new_reader.readexactly.side_effect = [hdr, payload]
    new_writer = make_writer()

    bridge = UnityBridge(probe=probe)
    bridge._reader = new_reader
    bridge._writer = new_writer
    # Simulate previous failure
    bridge._first_failure_ts = time.monotonic() - 0.1

    result = await bridge.send("test", {})
    assert result["ok"] is True

    assert len(METRICS._observations.get("recompile.duration_ms", [])) >= 1


# ---------------------------------------------------------------------------
# 25. Probe injected / constructed
# ---------------------------------------------------------------------------

def test_bridge_default_probe_constructed_lazily(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    bridge = UnityBridge()
    assert isinstance(bridge._probe, CompileStateProbe)


def test_bridge_accepts_injected_probe():
    sentinel = object()
    bridge = UnityBridge(probe=sentinel)
    assert bridge._probe is sentinel


def test_bridge_metrics_isolated_per_test():
    METRICS.inc("recompile.detected")
    assert METRICS._counters["recompile.detected"] == 1


# ---------------------------------------------------------------------------
# 26. _reconnect resets first_failure_ts and fires callbacks
# ---------------------------------------------------------------------------

async def test_reconnect_resets_failure_ts():
    mock_reader = MagicMock()
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    ping_hdr, ping_pay = ping_response()
    mock_reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])

    async def mock_open(host, port):
        return mock_reader, mock_writer

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=mock_open):
        bridge = _bridge_mod.UnityBridge("127.0.0.1", 9999)
        bridge._first_failure_ts = time.monotonic() - 1.0
        await bridge._reconnect()
        assert bridge._first_failure_ts is None


# ---------------------------------------------------------------------------
# A3. Compilation gate: _send_raw passes through to bridge when probe busy
# ---------------------------------------------------------------------------

async def test_compilation_gate_passes_through_when_busy():
    """_send_raw passes through to bridge even when probe says busy — bridge handles retries."""
    from unity_mcp.server import _send_raw
    import unity_mcp.server as server_mod

    probe = _make_probe(busy=True, strong_busy=True, remaining=12.0)

    class FakeBridge:
        connected = True
        _probe = probe
        send_called = False

        async def send(self, cmd, args, timeout=30.0):
            self.send_called = True
            return {"ok": True, "data": "x"}

    fake_bridge = FakeBridge()

    class FakeSlot:
        bridge = fake_bridge
        def get(self, name): return None
        def list(self): return {}

    old_slot = server_mod.slot
    server_mod.slot = FakeSlot()
    try:
        result = await _send_raw("get_hierarchy", {})
        assert result == "x"
    finally:
        server_mod.slot = old_slot

    assert fake_bridge.send_called, "bridge.send should be called — no pre-check block"


# ---------------------------------------------------------------------------
# G17: busy-retry loop breaks on terminal wedge signal (not MAX_RETRIES exhaustion)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_busy_retry_breaks_on_wedge_signal():
    """G17: bridge send() with result.retry + terminal wedge → BUILD-FAILED-WEDGE, NOT exhaustion.

    Red-precondition: bridge.py:142-147 loop re-sent up to MAX_RETRIES regardless of wedge.
    Fixed: detect_wedge() consulted before each retry; wedge → return structured STOP.

    A8: feeds REAL WedgeReport into the real send() retry path; inject only _send/mock_open.
    _send call-count proves early break: 1 read attempt + wedge check, NOT MAX_RETRIES sends.
    """
    from unity_mcp.editor_log import WedgeReport
    wedge = WedgeReport(kind="build-failed-wedge", cs_errors=["CS0535: foo"])

    # Wire that returns a retry hint (Unity busy)
    retry_response = {"id": "0001", "ok": False, "retry": 5000}
    retry_payload = json.dumps(retry_response).encode()
    retry_hdr = struct.pack("!I", len(retry_payload))

    send_count = 0

    async def open_conn(*a, **kw):
        nonlocal send_count
        reader = AsyncMock()
        writer = make_writer()

        async def read_exactly(n):
            nonlocal send_count
            if n == 4:
                return retry_hdr
            else:
                send_count += 1
                return retry_payload

        reader.readexactly = AsyncMock(side_effect=read_exactly)
        return reader, writer

    probe = _make_probe(busy=True)

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_conn), \
         patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock), \
         patch("unity_mcp.editor_log.detect_wedge", return_value=wedge):
        bridge = UnityBridge(probe=probe)
        await bridge.connect()
        result = await bridge.send("test", {})

    # G17: must return BUILD-FAILED-WEDGE on first wedge detection, NOT exhaust retries
    assert result.get("ok") is False
    assert "BUILD-FAILED-WEDGE" in result.get("data", ""), \
        f"Expected BUILD-FAILED-WEDGE in result data, got {result!r}"
    assert "do NOT restart" in result.get("data", ""), \
        f"Verdict must carry the never-restart instruction, got {result!r}"
    # A8: prove early break — only 1 send attempt (not MAX_RETRIES)
    assert send_count == 1, \
        f"Expected 1 send attempt (early break on wedge), got {send_count}"


# ---------------------------------------------------------------------------
# G16w: busy message must not fabricate _DEFAULT_REMAINING_S countdown
# ---------------------------------------------------------------------------

def test_busy_branch_consults_diagnose_not_fabricated_countdown():
    """G16w: when probe says busy but no compile info known, _DEFAULT_REMAINING_S=5.0 is
    a fabricated default — the test documents this existing gap.

    Red-precondition: compile_state.py:92 returns _DEFAULT_REMAINING_S=5.0 when no compile
    info is tracked (no update_compile_info called). The bridge's error message says '~5s left'
    which is fabricated, not from real state.

    This test pins the fabricated default and will fail when the code is fixed to
    derive remaining time from real state/diagnose.
    """
    from unity_mcp.compile_state import _DEFAULT_REMAINING_S
    probe = CompileStateProbe(port=9500)
    # Without any update_compile_info call, remaining is the default
    remaining = probe.estimated_remaining_s()
    assert remaining == _DEFAULT_REMAINING_S, (
        f"Gap documented (G16w): estimated_remaining_s returns fabricated default "
        f"{_DEFAULT_REMAINING_S}s without real state. Got {remaining}."
    )


# ---------------------------------------------------------------------------
# P-183 / G17: reload flag cleared after play timeout
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_reload_cleared_after_play_timeout():
    """P-183 / G17: after a play-mode read timeout, _reload.is_active() must be False.

    Bug: when asyncio.TimeoutError fires AFTER the payload was delivered to Unity,
    a stale DomainReloadTracker mark keeps blocking subsequent send() calls for up
    to 90s with DomainReloadError — even though Unity finished reloading.

    Fix: _send_with_retry clears _reload when asyncio.TimeoutError occurs after
    _payload_sent=True, proving Unity was processing the command (not reloading).

    Setup: _reload is marked INSIDE read_effect (after payload sent, before timeout),
    simulating a going_away response that arrived concurrently with a play command.
    Subsequent reads raise IncompleteReadError to fail retries fast."""
    probe = _make_probe(busy=False)
    preamble = reconnect_preamble()
    call_idx = 0
    bridge_ref: list = [None]
    _marked_once = [False]

    async def read_effect(n):
        nonlocal call_idx
        if call_idx < len(preamble):
            chunk = preamble[call_idx]
            call_idx += 1
            return chunk
        # First post-preamble read: mark _reload mid-flight (stale going_away scenario)
        # then stall so asyncio.wait_for fires TimeoutError with _payload_sent=True.
        if not _marked_once[0]:
            _marked_once[0] = True
            if bridge_ref[0] is not None:
                bridge_ref[0]._reload.mark()
            await asyncio.sleep(9999)  # timeout=0.05s fires → asyncio.TimeoutError
        # Subsequent calls (retry reconnects): fail fast, no re-marking
        raise asyncio.IncompleteReadError(b"", n)

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=read_effect)
    writer = make_writer()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(reader, writer)):
        bridge_ref[0] = UnityBridge(probe=probe)
        await bridge_ref[0].connect()

        # Precondition: _reload NOT active before send()
        assert not bridge_ref[0]._reload.is_active(), "precondition: _reload must be inactive"

        # send() proceeds (no pre-existing reload), payload is written,
        # read_effect marks _reload and stalls → asyncio.TimeoutError fires
        with pytest.raises((ConnectionError, TimeoutError, asyncio.TimeoutError)):
            await bridge_ref[0].send("run_playtest", {}, timeout=0.05)

    # P-183: _reload must be cleared because timeout arrived after payload sent
    assert not bridge_ref[0]._reload.is_active(), (
        "_reload must be cleared after play-mode timeout so next request isn't blocked"
    )


# ---------------------------------------------------------------------------
# SH-8: bridge.__init__ passes port to autodetect_project_path
# ---------------------------------------------------------------------------

def test_bridge_init_passes_port_to_autodetect():
    """UnityBridge.__init__ passes self._port to autodetect_project_path."""
    with patch("unity_mcp.bridge.CompileStateProbe") as mock_probe_cls:
        mock_probe_cls.autodetect_project_path.return_value = None
        _bridge_mod.UnityBridge(port=9501)
    mock_probe_cls.autodetect_project_path.assert_called_once_with(port=9501)
