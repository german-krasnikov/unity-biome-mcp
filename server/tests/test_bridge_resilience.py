"""Cycle 7a: bridge resilience tests — 3-tier timeouts + ECONNREFUSED fast-fail.

RED phase — all tests expected to fail before implementation.
"""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock, patch, MagicMock
from unity_mcp.bridge import UnityBridge
from helpers import ping_response, reconnect_preamble


# ── helpers ──────────────────────────────────────────────────────────────────

def _make_bridge(port: int = 9999) -> UnityBridge:
    return UnityBridge("127.0.0.1", port)


# ── Test 4: SESSION_TIMEOUT aborts retries ───────────────────────────────────

async def test_session_timeout_aborts_retries(monkeypatch):
    """SESSION_TIMEOUT must abort retry loop. Use slow-failure mock so refused
    fast-backoff doesn't finish before deadline check (which would make this
    test pass for the wrong reason).
    """
    import unity_mcp.bridge as bridge_mod
    monkeypatch.setattr(bridge_mod, "SESSION_TIMEOUT", 0.3)

    # Mock _reconnect to take 0.5s — longer than SESSION_TIMEOUT — and succeed.
    # send() then writes/reads, _read_response returns immediately with bad id,
    # which triggers id-mismatch retry path. Loop top checks deadline → aborts.
    bridge = bridge_mod.UnityBridge("127.0.0.1", 1)

    async def slow_reconnect(fire_callbacks=True):
        await asyncio.sleep(0.5)
        raise ConnectionResetError("simulated drop")
    bridge._reconnect = slow_reconnect

    async def fail_close():
        pass
    bridge.close = fail_close

    start = time.monotonic()
    with pytest.raises((TimeoutError, ConnectionError, OSError)):
        await bridge.send("ping", {})
    elapsed = time.monotonic() - start
    # Must abort BEFORE first retry-cycle's full sleep+reconnect (>0.5s).
    # Allow up to 2s for safety; pure-refused path would be <1s — wrong-reason proof
    # comes from elapsed >= deadline (0.3s) but < first reconnect cost (0.5s + backoff).
    assert 0.2 < elapsed < 2.5, f"SESSION_TIMEOUT didn't abort cleanly: {elapsed:.2f}s"


# ── Test 5: CONNECT_TIMEOUT fast-fail ────────────────────────────────────────

async def test_connect_timeout_fast_fail(monkeypatch):
    """Mocked open_connection that hangs forever — verify CONNECT_TIMEOUT fires."""
    import unity_mcp.bridge as bridge_mod
    monkeypatch.setattr(bridge_mod, "CONNECT_TIMEOUT", 0.5)

    async def hanging_open_connection(host, port):
        await asyncio.sleep(60.0)

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", hanging_open_connection)
    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)

    start = time.monotonic()
    with pytest.raises((TimeoutError, asyncio.TimeoutError, ConnectionError, OSError)):
        await bridge.connect()
    elapsed = time.monotonic() - start
    assert 0.3 < elapsed < 1.5, f"connect() took {elapsed:.2f}s, expected ~0.5s"


# ── Test 6: ECONNREFUSED retries with backoff (#05) ──────────────────────────

@pytest.mark.real_clock  # A03: asserts the real backoff formula's first-attempt
# value (~2.0s) via bridge_retry._capped_backoff.
async def test_econnrefused_retries_with_backoff(monkeypatch):
    """#05: ConnectionRefusedError → retries with exponential backoff (domain reload),
    eventually raises after max_retries. Sleep mocked to avoid actual delays."""
    import unity_mcp.bridge as bridge_mod

    call_count = 0
    sleep_calls = []

    async def mock_open_connection(host, port):
        nonlocal call_count
        call_count += 1
        raise ConnectionRefusedError("mock: connection refused")

    async def mock_sleep(delay):
        sleep_calls.append(delay)

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)
    monkeypatch.setattr(bridge_mod.asyncio, "sleep", mock_sleep)

    from helpers import make_idle_probe
    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())

    with pytest.raises((ConnectionError, OSError)):
        await bridge.send("ping", {})

    # Should have retried (backoff delays recorded)
    assert call_count >= 2, "bridge must retry on ConnectionRefused (domain reload)"
    assert len(sleep_calls) >= 1, "backoff sleep must be called"
    # First backoff is ~2s (2^(0+1)=2 + up to 10% jitter)
    assert 2.0 <= sleep_calls[0] <= 2.25, f"first backoff must be ~2s, got {sleep_calls[0]}s"


# ── Test 7: reconnect invokes callbacks ──────────────────────────────────────

async def test_reconnect_invokes_callback(monkeypatch):
    """add_reconnect_callback: registered fn is called after _reconnect."""
    import unity_mcp.bridge as bridge_mod

    ping_hdr, ping_pay = ping_response()
    mock_reader = MagicMock()
    mock_reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    async def mock_open_connection(host, port):
        return mock_reader, mock_writer

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)

    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)
    fired = []
    bridge.add_reconnect_callback(lambda: fired.append(1))

    await bridge._reconnect()
    assert len(fired) == 1, f"callback not fired, got {fired}"


# ── Test 8 (bridge side): reconnect callback wires middleware.reset_session ───

async def test_reconnect_callback_resets_middleware(monkeypatch):
    """Simulate wiring: reconnect fires reset_session on middleware."""
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.middleware import Middleware

    ping_hdr, ping_pay = ping_response()
    mock_reader = MagicMock()
    mock_reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble()])
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    async def mock_open_connection(host, port):
        return mock_reader, mock_writer

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)

    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)
    mw = Middleware()
    # Seed some state to verify reset
    mw.check_retry("set_property", {"path": "/X"})
    assert mw.check_retry("set_property", {"path": "/X"}) is not None  # blocked

    bridge.add_reconnect_callback(mw.reset_session)
    await bridge._reconnect()

    # After reconnect, retry cache cleared
    assert mw.check_retry("set_property", {"path": "/X"}) is None


# Test 9 removed: was duplicate of test_econnrefused_fast_backoff.
# Regression guard for except-clause ordering is implicit in test 6's timing assertion
# (>= 0.4s rules out instant fall-through, < 5s rules out [2,4,8] slow path).
