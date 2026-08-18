"""RC4: _reload_gate asyncio.Event replaces fixed sleep for DomainReloadError retries."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, MagicMock, Mock, patch
import pytest

from unity_mcp.bridge import UnityBridge, DomainReloadError
from helpers import make_writer, make_idle_probe, reconnect_preamble


# ---------------------------------------------------------------------------
# test_reload_gate_clears_on_domain_reload_reason
# ---------------------------------------------------------------------------

async def test_reload_gate_clears_on_domain_reload_reason():
    """_reload_gate starts set; it can be cleared and re-opened as expected.

    Verifies the fundamental gate contract used by the domain_reload retry path:
    - starts open (set) so wait() returns immediately
    - can be cleared to block and then set() to unblock
    """
    bridge = UnityBridge(probe=make_idle_probe())
    assert bridge._reload_gate.is_set(), "_reload_gate must be set on init"

    # Simulate what _send_with_retry does on domain_reload reason
    bridge._reload_gate.clear()
    assert not bridge._reload_gate.is_set(), "gate must be closed after clear()"

    # Simulate what _reconnect does on success
    bridge._reload_gate.set()
    assert bridge._reload_gate.is_set(), "gate must be open after set()"


# ---------------------------------------------------------------------------
# test_reload_gate_early_wakeup_on_reconnect
# ---------------------------------------------------------------------------

async def test_reload_gate_early_wakeup_on_reconnect():
    """Gate set() from reconnect wakes the domain_reload wait early."""
    bridge = UnityBridge(probe=make_idle_probe())

    # Close the gate as if a domain_reload just happened
    bridge._reload_gate.clear()

    woke_early = []

    async def set_gate_after_short_delay():
        await asyncio.sleep(0.01)  # much less than the 2s timeout below
        bridge._reload_gate.set()

    asyncio.create_task(set_gate_after_short_delay())

    # wait_for with 2s timeout — should complete well before that via gate.set()
    import time
    start = time.monotonic()
    try:
        await asyncio.wait_for(bridge._reload_gate.wait(), timeout=2.0)
        elapsed = time.monotonic() - start
        woke_early.append(elapsed < 5.0)
    except asyncio.TimeoutError:
        woke_early.append(False)

    assert woke_early[0], "Gate.set() from reconnect should wake wait early"


# ---------------------------------------------------------------------------
# test_reload_gate_set_after_reconnect
# ---------------------------------------------------------------------------

async def test_reload_gate_set_after_reconnect():
    """_reload_gate is set (opened) after successful _reconnect()."""
    probe = make_idle_probe()
    bridge = UnityBridge(probe=probe)

    # Simulate gate closed (domain reload in progress)
    bridge._reload_gate.clear()
    assert not bridge._reload_gate.is_set()

    reader = AsyncMock()
    writer = make_writer()
    # reconnect_preamble provides ping + version response
    reader.readexactly = AsyncMock(side_effect=reconnect_preamble())

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._reload_gate.is_set(), "_reload_gate must be set after _reconnect success"


# ---------------------------------------------------------------------------
# test_non_reload_reasons_use_sleep_not_gate
# ---------------------------------------------------------------------------

async def test_non_reload_reasons_use_sleep_not_gate():
    """For non-domain-reload reasons, asyncio.sleep is called, not wait_for(gate)."""
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = True  # busy = triggers "busy" reason

    bridge = UnityBridge(probe=probe)
    sleep_calls: list[float] = []

    original_sleep = asyncio.sleep

    async def spy_sleep(delay):
        sleep_calls.append(delay)
        await original_sleep(0)  # don't actually wait

    writer = make_writer()
    reader = AsyncMock()
    # Return ConnectionRefusedError so should_retry gets reason="busy"
    reader.readexactly = AsyncMock(side_effect=ConnectionRefusedError())

    with patch("asyncio.open_connection", return_value=(reader, writer)), \
         patch("asyncio.sleep", side_effect=spy_sleep):
        bridge._writer = writer
        bridge._reader = reader
        with pytest.raises((ConnectionError, Exception)):
            await asyncio.wait_for(bridge.send("ping", {}), timeout=5.0)

    # asyncio.sleep must have been called (not just wait_for(gate))
    assert sleep_calls, "asyncio.sleep should be called for non-domain-reload retry reasons"


# ---------------------------------------------------------------------------
# RC5: Reload gate always cleared on domain_reload — no connected guard
# ---------------------------------------------------------------------------

async def test_reload_gate_always_clears_on_domain_reload():
    """gate.clear() is called unconditionally on domain_reload, even when connected.

    Fix: removed `if not self.connected:` guard — gate always cleared so
    retries wait for the real reconnect signal instead of barrelling through.
    """
    import json, struct

    probe = make_idle_probe()
    bridge = UnityBridge(probe=probe)

    # Track if gate.clear() was called while connected=True
    cleared_when_connected = []
    original_clear = bridge._reload_gate.clear

    def spy_clear():
        cleared_when_connected.append(bridge.connected)
        original_clear()

    bridge._reload_gate.clear = spy_clear

    # Reconnected writer — simulates heartbeat reconnecting during send()
    reconnected_writer = make_writer()

    # First response: going_away (DomainReloadError)
    going_away = json.dumps({"ev": "going_away", "reason": "reload"}).encode()
    ga_hdr = struct.pack("!I", len(going_away))
    # Second response: success (msg_id "0001" — counter=1, no increment on retry)
    ok_resp = json.dumps({"id": "0001", "ok": True, "data": "ok"}).encode()
    ok_hdr = struct.pack("!I", len(ok_resp))

    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[ga_hdr, going_away, ok_hdr, ok_resp])

    original_close = bridge.close

    async def reconnecting_close():
        await original_close()
        # Heartbeat reconnect: bridge appears connected again
        bridge._writer = reconnected_writer
        bridge._reader = reader  # same reader, remaining responses: ok_hdr, ok_resp
        bridge._reload_gate.set()

    with patch("asyncio.open_connection", return_value=(reader, writer)), \
         patch.object(bridge, "close", new=reconnecting_close), \
         patch("asyncio.sleep", new=AsyncMock()):
        bridge._writer = writer
        bridge._reader = reader
        result = await asyncio.wait_for(bridge.send("test", {}), timeout=10.0)

    assert result["ok"] is True

    # After fix: gate.clear() IS called even when bridge.connected=True
    assert any(cleared_when_connected), (
        "gate.clear() was never called when connected=True; "
        "fix: remove `if not self.connected:` guard before gate.clear()"
    )


# ---------------------------------------------------------------------------
# Fast-fail during active domain reload
# ---------------------------------------------------------------------------

async def test_send_fast_fails_during_domain_reload():
    """send() raises DomainReloadError immediately when _reload.is_active()."""
    bridge = UnityBridge(probe=make_idle_probe())
    bridge._reload.mark()

    with pytest.raises(DomainReloadError):
        await bridge.send("ping", {})
