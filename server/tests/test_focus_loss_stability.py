"""Live integration test: focus-loss → zero reconnects.

THE system-level end-to-end witness for the ConfigureAwait(false) fix.
macOS only (uses osascript to background Unity).

Run: pytest -m "live" tests/test_focus_loss_stability.py -v
Requires Unity running with MCP plugin loaded.
"""
import asyncio
import subprocess
import sys
import time

import pytest

from unity_mcp.bridge import UnityBridge
from unity_mcp.metrics import METRICS


def _discover_port() -> int:
    import pathlib, os
    port = int(os.environ.get("UNITY_MCP_PORT", "0"))
    if port:
        return port
    for p in pathlib.Path.home().glob(".unity-biome-mcp/ports/*.port"):
        try:
            return int(p.read_text().split("\n")[0])
        except Exception:
            pass
    return 9500


@pytest.mark.live
@pytest.mark.asyncio
@pytest.mark.skipif(sys.platform != "darwin", reason="osascript focus-switch: macOS only")
async def test_focus_loss_zero_reconnects():
    """Background Unity for 30s, send 4 pings, assert all pong + zero reconnects.

    THE end-to-end witness for the whole-system fix. Before ConfigureAwait(false),
    pings would timeout during background, triggering reconnect storm on refocus.
    After fix: pings succeed because socket I/O runs on ThreadPool.

    The unit-level regression witness is ConnectionStabilityTests.PingRespondsWithStalledSyncContext
    (C# NUnit internal-seam test in unity-plugin/Editor/Tests/).
    """
    port = _discover_port()
    bridge = UnityBridge(port=port)

    # Establish baseline via public API — no private _counters access.
    METRICS.reset_counter("reconnect.send_path")
    baseline = METRICS.snapshot()["counters"].get("reconnect.send_path", 0)

    async def ping():
        resp = await bridge.send("ping", {})
        assert resp.get("ok"), f"ping failed: {resp}"

    try:
        # Initial connect
        await asyncio.wait_for(bridge.connect(), timeout=5.0)
        initial_reconnect_at = bridge._last_reconnect_at

        # Background Unity (Finder takes focus)
        subprocess.run(["osascript", "-e", 'tell application "Finder" to activate'],
                       check=False, capture_output=True)

        # Send 3 pings while Unity is in background (5s apart = 15s total)
        for i in range(3):
            await asyncio.sleep(5.0)
            await asyncio.wait_for(ping(), timeout=10.0)

        # Restore Unity focus
        subprocess.run(["osascript", "-e", 'tell application "Unity" to activate'],
                       check=False, capture_output=True)

        # Wait for drain, send 1 more ping
        await asyncio.sleep(5.0)
        await asyncio.wait_for(ping(), timeout=10.0)

        # Assert zero reconnects
        assert bridge._last_reconnect_at == initial_reconnect_at, (
            f"Reconnect triggered during focus-loss test! "
            f"_last_reconnect_at changed from {initial_reconnect_at:.3f} to "
            f"{bridge._last_reconnect_at:.3f}"
        )
        send_path_delta = (
            METRICS.snapshot()["counters"].get("reconnect.send_path", 0) - baseline
        )
        assert send_path_delta == 0, (
            f"reconnect.send_path counter incremented {send_path_delta} times during test"
        )
    finally:
        await bridge.close()
