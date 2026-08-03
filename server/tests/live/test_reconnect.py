"""Live tests: TCP reconnect after recompile / domain reload.

These tests verify the ACTUAL reconnect flow against a running Unity Editor.
No mocks — real TCP, real domain reload, real state file transitions.

IMPORTANT: Each recompile takes 10-30s. Minimize recompile count.

Run: pytest -m live tests/live/test_reconnect.py -v
"""
import asyncio
import time

import pytest
import pytest_asyncio

from unity_mcp.bridge import UnityBridge
from tests.live.conftest import _connect_with_retry, make_live_bridge

pytestmark = pytest.mark.live

RECOMPILE_TIMEOUT = 180.0


async def _wait_reconnect(bridge: UnityBridge, timeout: float = RECOMPILE_TIMEOUT) -> bool:
    """Poll until bridge reconnects and ping succeeds."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        await asyncio.sleep(2.0)
        if bridge.connected:
            try:
                r = await bridge.send("ping", {})
                if r.get("ok"):
                    return True
            except Exception:
                pass
    return False


async def _worker_ping_once() -> bool:
    """Prove that the project-pinned worker accepts commands."""
    bridge = None
    try:
        bridge = make_live_bridge()
        await bridge.connect()
        response = await bridge.send("ping", {})
        return response.get("ok") is True
    except (OSError, asyncio.TimeoutError, ConnectionError, RuntimeError):
        return False
    finally:
        if bridge is not None:
            await bridge.close()


async def _wait_fresh_connect(timeout: float = RECOMPILE_TIMEOUT) -> bool:
    """Poll until the exact worker accepts a fresh bridge and ping."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if await _worker_ping_once():
            return True
        await asyncio.sleep(2.0)
    return False


@pytest_asyncio.fixture(scope="module", autouse=True)
async def _wait_unity_recovery():
    """After destructive reconnect tests, prove the exact Unity worker is back."""
    yield
    if not await _wait_fresh_connect():
        pytest.fail(
            "Unity worker did not recover after destructive reconnect tests"
        )


async def test_reconnect_after_recompile_with_heartbeat(bridge, ensure_edit_mode):
    """Trigger recompile, verify heartbeat-driven reconnect + commands work after."""
    r = await bridge.send("ping", {})
    assert r.get("ok"), "Ping should succeed before recompile"

    bridge.start_heartbeat(interval=5.0)
    try:
        await bridge.send("recompile", {})
        assert await _wait_reconnect(bridge), \
            "Bridge did not reconnect within timeout after recompile"

        # Verify real commands work (not just ping)
        h = await bridge.send("get_hierarchy", {})
        assert h.get("ok"), f"get_hierarchy failed after reconnect: {h}"
        assert h.get("data"), "Hierarchy should not be empty"
    finally:
        bridge.stop_heartbeat()


async def test_fresh_bridge_connects_after_recompile(ensure_edit_mode):
    """A brand new bridge can connect after Unity has recompiled (no heartbeat)."""
    assert await _wait_fresh_connect(), "Unity not up before fresh-bridge test"

    b1 = make_live_bridge()
    await _connect_with_retry(b1)
    await b1.send("recompile", {})
    await b1.close()

    # Wait for Unity to restart TCP server
    assert await _wait_fresh_connect(), \
        "Fresh bridge could not connect within timeout after recompile"

    # Verify new connection works
    b2 = make_live_bridge()
    await _connect_with_retry(b2)
    r = await b2.send("ping", {})
    assert r.get("ok"), "Ping failed on fresh bridge after recompile"
    await b2.close()
