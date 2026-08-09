from __future__ import annotations

import pytest
from conformance.workers import _parse_status

from unity_mcp.bridge import UnityBridge

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_short_lived_reconnects_preserve_identity_and_primary_session(
    conformance_worker,
):
    worker, primary_bridge = conformance_worker

    for _ in range(3):
        bridge = UnityBridge(
            "127.0.0.1",
            port=worker.port,
            expected_project_path=worker.project_path,
        )
        await bridge.connect()
        try:
            resp = await bridge.send("get_status", {})
            assert resp["ok"], f"get_status failed after reconnect: {resp}"
            assert _parse_status(resp["data"]).get("port") == str(worker.port)
        finally:
            await bridge.close()

    resp = await primary_bridge.send("get_status", {})
    assert resp["ok"], f"primary bridge stopped responding after reconnect churn: {resp}"
    assert _parse_status(resp["data"]).get("port") == str(worker.port)
