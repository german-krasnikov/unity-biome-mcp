"""BridgeState machine — observable state during port lifecycle transitions.

- T-ST1: initial state is DISCONNECTED
- T-ST2: after _reconnect() → CONNECTED
- T-ST3: going_away (DomainReloadError ping) → reload marked
- T-ST4: DOMAIN_RELOADING + reload marked → send() raises DomainReloadError
- T-ST5: FAILED + cooldown active → send() raises ConnectionError
"""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge, BridgeState
from unity_mcp.bridge_socket import DomainReloadError
from helpers import make_writer, make_idle_probe, reconnect_preamble


# T-ST1: initial state
def test_initial_state_is_disconnected():
    """Freshly created bridge starts in DISCONNECTED state."""
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    assert bridge._state == BridgeState.DISCONNECTED
    assert bridge.connected is False


# T-ST2: CONNECTED after _reconnect() (state set by _accept_candidate)
async def test_state_connected_after_reconnect():
    """_reconnect() → _accept_candidate() → _state = CONNECTED.

    Note: connect() does not set _state; _accept_candidate() (called by _reconnect())
    is the path that transitions to CONNECTED.
    """
    probe = make_idle_probe()
    bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
    preamble = reconnect_preamble()
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=preamble)
    writer = make_writer()

    with patch.object(bridge_mod.asyncio, "open_connection",
                      new=AsyncMock(return_value=(reader, writer))), \
         patch("unity_mcp.bridge._apply_socket_options"), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=None), \
         patch.object(bridge, "start_heartbeat"):
        await bridge._reconnect()

    assert bridge._state == BridgeState.CONNECTED
    assert bridge.connected is True


# T-ST3: DomainReloadError in connected ping → marks reload state
async def test_domain_reload_error_marks_reload_state():
    """DomainReloadError during connected ping → bridge._reload.is_active().

    Mirrors bridge_heartbeat.py: DomainReloadError except clause in heartbeat tick.
    """
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    bridge._writer = make_writer()
    bridge._reader = MagicMock()

    async def fail_with_reload(*a, **kw):
        raise DomainReloadError("going_away")

    with patch.object(bridge, "_raw_ping", new=fail_with_reload), \
         patch.object(bridge, "close", new=AsyncMock()), \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=0)

    assert bridge._reload.is_active() is True
    bridge._probe.mark_recompile_issued.assert_called_once()


# T-ST4: send() raises when reload is active
async def test_send_rejects_during_domain_reloading():
    """_reload.mark() → send() raises DomainReloadError (fast-fail guard)."""
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    bridge._state = BridgeState.DOMAIN_RELOADING
    bridge._reload.mark()

    with pytest.raises(DomainReloadError):
        await bridge.send("ping", {})


# T-ST5: FAILED state + cooldown active → send() raises ConnectionError
async def test_send_raises_connection_error_on_failed_state_with_cooldown():
    """BridgeState.FAILED + cooldown active → send() raises ConnectionError.

    send() checks _reconnect_cooldown_ok() in the FAILED branch and raises
    ConnectionError("Reconnect cooldown active") when cooldown is still running.
    """
    bridge = UnityBridge("127.0.0.1", 9500, probe=make_idle_probe())
    bridge._state = BridgeState.FAILED
    bridge._last_reconnect_at = time.monotonic()  # just reconnected
    bridge._reconnect_backoff = 60.0              # long cooldown

    with pytest.raises(ConnectionError, match="Reconnect cooldown active"):
        await bridge.send("ping", {})
