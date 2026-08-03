"""Project-aware background-worker liveness integration test."""

import asyncio

import pytest

from unity_mcp.metrics import METRICS


pytestmark = pytest.mark.live


async def test_focus_loss_zero_reconnects(bridge):
    """A batch worker stays responsive without any foreground-window assistance.

    The disposable final-gate worker is already backgrounded by ``-batchmode``.
    Exercising that real condition is deterministic and does not activate Finder,
    another Unity process, or any user-owned window.
    """
    METRICS.reset_counter("reconnect.send_path")
    baseline = METRICS.snapshot()["counters"].get("reconnect.send_path", 0)
    initial_reconnect_at = bridge._last_reconnect_at

    for _ in range(4):
        await asyncio.sleep(5.0)
        response = await asyncio.wait_for(bridge.send("ping", {}), timeout=10.0)
        assert response.get("ok"), f"ping failed: {response}"

    assert bridge._last_reconnect_at == initial_reconnect_at, (
        "Background worker unexpectedly reconnected: "
        f"{initial_reconnect_at:.3f} -> {bridge._last_reconnect_at:.3f}"
    )
    send_path_delta = (
        METRICS.snapshot()["counters"].get("reconnect.send_path", 0) - baseline
    )
    assert send_path_delta == 0, (
        f"reconnect.send_path incremented {send_path_delta} times"
    )
