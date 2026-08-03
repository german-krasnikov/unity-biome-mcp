"""Project-aware real-Unity connection recovery test."""

import asyncio
import os
import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import unity_mcp.bridge as bridge_module
from tests.live.conftest import LIVE_HOST, current_worker_port
from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_heartbeat import BACKOFF_MAX_S, BACKOFF_MIN_S
from unity_mcp.compile_state import CompileStateProbe


pytestmark = pytest.mark.live


async def test_crash_recovery_to_live_unity_no_drift(bridge):
    """Reconnect discovery reaches only the configured disposable worker."""
    target_response = await bridge.send("ping", {})
    assert target_response.get("ok"), f"configured worker did not answer: {target_response}"

    expected_project = os.environ["UNITY_MCP_PROJECT_PATH"]
    live_port = current_worker_port()
    probe = MagicMock(spec=CompileStateProbe)
    probe.is_unity_busy.return_value = False
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    probe.estimated_remaining_s.return_value = 5.0
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()

    dead_attempts = 2
    discovered = [None] * dead_attempts + [live_port]
    discovery_log: list[int | None] = []

    def discoverer(skip_probe=False):
        del skip_probe
        result = discovered.pop(0) if discovered else live_port
        discovery_log.append(result)
        return result

    recovering = UnityBridge(
        LIVE_HOST,
        port=9999,
        probe=probe,
        port_discoverer=discoverer,
        expected_project_path=expected_project,
    )
    recovering._pinned_pid = None
    recovering._reconnect_backoff = BACKOFF_MAX_S

    open_calls = 0
    real_open_connection = asyncio.open_connection

    async def selective_open_connection(host, port, **kwargs):
        nonlocal open_calls
        open_calls += 1
        if open_calls <= dead_attempts:
            raise ConnectionRefusedError(f"simulated dead endpoint #{open_calls}")
        assert host == LIVE_HOST
        assert port == live_port, f"unexpected recovery candidate {host}:{port}"
        return await real_open_connection(host, port, **kwargs)

    try:
        with (
            patch.object(bridge_module, "STARTUP_GRACE_S", 9999.0),
            patch(
                "unity_mcp.bridge_heartbeat.os.getppid",
                return_value=os.getppid(),
            ),
            patch.object(
                bridge_module.asyncio,
                "open_connection",
                side_effect=selective_open_connection,
            ),
        ):
            for _ in range(dead_attempts + 3):
                recovering._last_reconnect_at = 0.0
                recovering._reconnect_started_at = time.monotonic() - 1.0
                with patch(
                    "unity_mcp.bridge_heartbeat.asyncio.sleep",
                    new=AsyncMock(),
                ):
                    await recovering._heartbeat_tick(15.0)
                if recovering._port == live_port:
                    break

        assert recovering._port == live_port, (
            f"recovery did not reach configured port {live_port}: {discovery_log}"
        )
        assert recovering._port != 9500
        assert recovering._reconnect_backoff == BACKOFF_MIN_S
        assert len(discovery_log) >= dead_attempts + 1
    finally:
        await recovering.close()
