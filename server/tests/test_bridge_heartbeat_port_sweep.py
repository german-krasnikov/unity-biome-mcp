"""ARC-7 T1: periodic stale-port sweep from HeartbeatMixin._tick_disconnected.

Gate: sweep only fires on the idle branch (not busy, no reload active) —
that is the branch that used to be a no-op `wait = 2.0`. Reload-active is
load-bearing: OnBeforeReload() keeps the port file across a live domain
reload, so sweeping there would delete a live Unity's port file.
"""
import asyncio
from unittest.mock import AsyncMock, call, patch

from helpers import make_bridge_disconnected as _make_bridge_disconnected


async def test_sweeps_stale_ports_when_idle():
    """Idle disconnected tick (not busy, no reload) triggers exactly one sweep call."""
    bridge = _make_bridge_disconnected(busy=False)

    with patch("unity_mcp.bridge_heartbeat.cleanup_stale_port_files") as spy, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert spy.call_args == call(tcp_probe=True)
    assert spy.call_count == 1


async def test_throttles_repeated_sweeps():
    """Two consecutive idle ticks inside the throttle window sweep only once."""
    bridge = _make_bridge_disconnected(busy=False)

    with patch("unity_mcp.bridge_heartbeat.cleanup_stale_port_files") as spy, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)
        await bridge._heartbeat_tick(interval=15.0)

    assert spy.call_count == 1


async def test_skips_sweep_during_reload():
    """Reload active must not sweep — OnBeforeReload keeps the port file mid-reload."""
    bridge = _make_bridge_disconnected(busy=False)
    bridge._reload.mark()

    with patch("unity_mcp.bridge_heartbeat.cleanup_stale_port_files") as spy, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert spy.call_count == 0


async def test_skips_sweep_when_busy():
    """Busy probe (compiling) must not sweep — process is alive and working."""
    bridge = _make_bridge_disconnected(busy=True)

    with patch("unity_mcp.bridge_heartbeat.cleanup_stale_port_files") as spy, \
         patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert spy.call_count == 0
