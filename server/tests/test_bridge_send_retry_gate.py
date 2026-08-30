"""send() retry-gate unit tests — RC4 domain_reload path.

Tests:
1. DomainReloadError → gate.clear() called, asyncio.sleep NOT called
2. gate.wait() timeout continues retry loop (does not abort)
3. MAX_RETRIES DomainReloadErrors → ConnectionError + reload still active
4. RetryPolicy.decide() returns (True, 2.0, "domain_reload") at attempt=0
5. Jitter bounded to delay * 0.1 (random.uniform(0, delay*0.1) args verified)
6. ConnectionRefusedError → asyncio.sleep called, gate NOT cleared
7. Gate set by concurrent close hook → send() wakes early (< 1s vs 2s backoff)
"""
import asyncio
import json
import struct
import time
from unittest.mock import AsyncMock, Mock, patch

import pytest

from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.bridge_reload_state import DomainReloadTracker
from unity_mcp.bridge_socket import DomainReloadError
from helpers import make_idle_probe, make_writer


# ── helpers ───────────────────────────────────────────────────────────────────

def _going_away_frames(count: int = 4) -> list[bytes]:
    """Flat list of (header, payload) bytes for `count` going_away frames."""
    body = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    hdr = struct.pack("!I", len(body))
    chunks: list[bytes] = []
    for _ in range(count):
        chunks += [hdr, body]
    return chunks


class _TimeoutGate:
    """Asyncio.Event replacement whose wait() immediately raises TimeoutError.

    Used to simulate gate.wait() timing out without waiting 2+ real seconds.
    """
    def __init__(self) -> None:
        self.clear_count = 0

    def clear(self) -> None:
        self.clear_count += 1

    def set(self) -> None:
        pass

    def is_set(self) -> bool:
        return False

    async def wait(self) -> None:
        raise asyncio.TimeoutError("simulated gate timeout")


def _make_bridge_and_writer():
    # Every scenario in this module exercises retry timing with a mock read
    # command; opt those command names into the real retry-safety predicate.
    bridge = UnityBridge(
        probe=make_idle_probe(),
        is_retry_safe=lambda cmd: cmd == "ping",
    )
    writer = make_writer()
    return bridge, writer


# ── Test 1 ───────────────────────────────────────────────────────────────────

async def test_send_domain_reload_uses_gate_not_sleep():
    """domain_reload reason uses gate.clear()+gate.wait(); asyncio.sleep not called."""
    bridge, writer = _make_bridge_and_writer()
    reconnected_writer = make_writer()

    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    ok_resp = json.dumps({"id": "0001", "ok": True, "data": "ok"}).encode()
    ok_hdr = struct.pack("!I", len(ok_resp))

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ga_hdr, going_away, ok_hdr, ok_resp])

    gate_clears: list[int] = []
    original_clear = bridge._reload_gate.clear

    def spy_clear() -> None:
        gate_clears.append(1)
        original_clear()

    bridge._reload_gate.clear = spy_clear

    sleep_calls: list[float] = []

    async def spy_sleep(delay: float) -> None:
        sleep_calls.append(delay)

    original_close = bridge.close

    async def reconnecting_close() -> None:
        await original_close()
        bridge._writer = reconnected_writer
        bridge._reader = reader
        bridge._reload_gate.set()

    with patch("asyncio.open_connection", return_value=(reader, writer)), \
         patch.object(bridge, "close", new=reconnecting_close), \
         patch("asyncio.sleep", side_effect=spy_sleep):
        bridge._writer = writer
        bridge._reader = reader
        result = await asyncio.wait_for(bridge.send("ping", {}), timeout=5.0)

    assert result["ok"] is True
    assert gate_clears, "gate.clear() must be called for domain_reload reason"
    assert sleep_calls == [], f"asyncio.sleep must NOT be called for domain_reload: {sleep_calls}"


# ── Test 2 ───────────────────────────────────────────────────────────────────

async def test_send_domain_reload_gate_timeout_continues_retry_loop():
    """Gate timeout (TimeoutError) is absorbed; retry loop continues past it."""
    bridge, writer = _make_bridge_and_writer()

    timeout_gate = _TimeoutGate()
    bridge._reload_gate = timeout_gate  # type: ignore[assignment]

    async def fake_reconnect(fire_callbacks: bool = True) -> None:
        bridge._writer = writer

    with patch.object(bridge, "_read_response",
                      side_effect=DomainReloadError("reload")), \
         patch.object(bridge, "_reconnect", side_effect=fake_reconnect), \
         patch("asyncio.sleep", new=AsyncMock()):
        bridge._writer = writer
        with pytest.raises((ConnectionError, RuntimeError)) as exc_info:
            await bridge.send("ping", {})

    # TimeoutError from gate must NOT propagate as the final exception.
    assert not isinstance(exc_info.value, asyncio.TimeoutError)
    # Gate was cleared more than once → loop ran more than one domain_reload iteration.
    assert timeout_gate.clear_count > 1, (
        f"gate.clear() called only {timeout_gate.clear_count}×; "
        "retry loop must continue past gate timeout"
    )


# ── Test 3 ───────────────────────────────────────────────────────────────────

async def test_send_exhausts_domain_reload_retries_raises_connection_error():
    """After MAX_RETRIES DomainReloadErrors, send() raises ConnectionError."""
    bridge, writer = _make_bridge_and_writer()

    bridge._reload_gate = _TimeoutGate()  # type: ignore[assignment]

    async def fake_reconnect(fire_callbacks: bool = True) -> None:
        bridge._writer = writer

    with patch.object(bridge, "_read_response",
                      side_effect=DomainReloadError("reload")), \
         patch.object(bridge, "_reconnect", side_effect=fake_reconnect), \
         patch("asyncio.sleep", new=AsyncMock()):
        bridge._writer = writer
        with pytest.raises(ConnectionError):
            await bridge.send("ping", {})

    assert bridge._reload.is_active() is True


# ── Test 4 ───────────────────────────────────────────────────────────────────

def test_retry_policy_decide_returns_domain_reload_reason():
    """RetryPolicy.decide(DomainReloadError, attempt=0) → (True, 2.0, 'domain_reload')."""
    probe = make_idle_probe()
    policy = RetryPolicy(
        probe=probe,
        reload=DomainReloadTracker(),
        is_retry_safe=lambda cmd: True,
        max_retries=3,
    )
    deadline = time.monotonic() + 60.0

    do_retry, delay, reason = policy.decide(
        DomainReloadError("reload"), attempt=0, session_deadline=deadline, cmd="ping"
    )
    assert do_retry is True
    assert reason == "domain_reload"
    assert delay == 2.0  # min(2**(0+1), 8.0)

    # Second call at attempt=1 → exponential backoff
    do_retry2, delay2, reason2 = policy.decide(
        DomainReloadError("reload"), attempt=1, session_deadline=deadline, cmd="ping"
    )
    assert do_retry2 is True
    assert reason2 == "domain_reload"
    assert delay2 == 4.0  # min(2**(1+1), 8.0)


# ── Test 5 ───────────────────────────────────────────────────────────────────

async def test_send_jitter_bounded_to_10pct_of_delay():
    """random.uniform(0, delay * 0.1) is called; upper bound ≤ 8.0 * 0.1."""
    bridge, writer = _make_bridge_and_writer()

    bridge._reload_gate = _TimeoutGate()  # type: ignore[assignment]

    uniform_calls: list[tuple[float, float]] = []

    def spy_uniform(a: float, b: float) -> float:
        uniform_calls.append((a, b))
        return 0.0  # deterministic

    async def fake_reconnect(fire_callbacks: bool = True) -> None:
        bridge._writer = writer

    with patch("unity_mcp.bridge.random.uniform", side_effect=spy_uniform), \
         patch.object(bridge, "_read_response",
                      side_effect=DomainReloadError("reload")), \
         patch.object(bridge, "_reconnect", side_effect=fake_reconnect), \
         patch("asyncio.sleep", new=AsyncMock()):
        bridge._writer = writer
        with pytest.raises((ConnectionError, RuntimeError)):
            await bridge.send("ping", {})

    assert uniform_calls, "random.uniform must be called during retry jitter"
    for a, b in uniform_calls:
        assert a == 0, f"random.uniform lower bound must be 0, got {a}"
        assert b <= 8.0 * 0.1 + 1e-9, f"jitter ceiling must be ≤ delay*0.1=0.8, got {b}"


# ── Test 6 ───────────────────────────────────────────────────────────────────

async def test_send_connection_refused_uses_sleep_not_gate():
    """ConnectionRefusedError → asyncio.sleep called; gate.clear() NOT called."""
    bridge, writer = _make_bridge_and_writer()

    gate_clears: list[bool] = []
    original_clear = bridge._reload_gate.clear

    def spy_clear() -> None:
        gate_clears.append(True)
        original_clear()

    bridge._reload_gate.clear = spy_clear

    sleep_calls: list[float] = []

    async def spy_sleep(delay: float) -> None:
        sleep_calls.append(delay)

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=ConnectionRefusedError())

    with patch("asyncio.sleep", side_effect=spy_sleep), \
         patch.object(bridge, "_reconnect",
                      new=AsyncMock(side_effect=ConnectionRefusedError())):
        bridge._writer = writer
        bridge._reader = reader
        with pytest.raises((ConnectionError, Exception)):
            await bridge.send("ping", {})

    assert sleep_calls, "asyncio.sleep must be called for connection_refused reason"
    assert gate_clears == [], (
        "_reload_gate.clear() must NOT be called for connection_refused reason"
    )


# ── Test 7 ───────────────────────────────────────────────────────────────────

async def test_send_wakes_early_when_concurrent_reconnect_sets_gate():
    """Gate set by concurrent task wakes send() well before the 2s backoff timeout."""
    bridge, writer = _make_bridge_and_writer()
    reconnected_writer = make_writer()

    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    ok_resp = json.dumps({"id": "0001", "ok": True, "data": "pong"}).encode()
    ok_hdr = struct.pack("!I", len(ok_resp))

    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ga_hdr, going_away, ok_hdr, ok_resp])

    original_close = bridge.close

    async def reconnecting_close() -> None:
        await original_close()
        # Schedule the "heartbeat reconnect" to fire 20ms later.
        async def _set_gate_later() -> None:
            await asyncio.sleep(0.02)
            bridge._writer = reconnected_writer
            bridge._reader = reader
            bridge._reload_gate.set()

        asyncio.create_task(_set_gate_later())

    with patch("asyncio.open_connection", return_value=(reader, writer)), \
         patch.object(bridge, "close", new=reconnecting_close):
        bridge._writer = writer
        bridge._reader = reader
        t0 = time.monotonic()
        result = await asyncio.wait_for(bridge.send("ping", {}), timeout=5.0)
        elapsed = time.monotonic() - t0

    assert result["ok"] is True
    assert elapsed < 1.0, (
        f"send() took {elapsed:.3f}s; expected < 1.0s (gate was set after 20ms, "
        "not the full 2s backoff)"
    )
