"""Gate 5: Alias System — resolution and status checks."""

import pytest

pytestmark = [pytest.mark.live, pytest.mark.conformance, pytest.mark.asyncio(loop_scope="session")]


async def test_alias_status_returns_info(conformance_worker):
    """alias_status returns loaded state info."""
    worker, bridge = conformance_worker
    resp = await bridge.send("alias_status", {})
    assert resp["ok"], f"alias_status failed: {resp}"
    data = resp.get("data", "")
    assert "loaded" in data.lower() or "count" in data.lower() or "empty" in data.lower(), \
        f"alias_status missing expected fields: {data[:200]}"


async def test_get_aliases_returns_data(conformance_worker):
    """get_aliases returns alias list or 'no aliases'."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_aliases", {})
    assert resp["ok"], f"get_aliases failed: {resp}"
    data = resp.get("data", "")
    assert data, "get_aliases returned empty"


async def test_batch_smoke(conformance_worker):
    """Batch with a single read command runs without error."""
    worker, bridge = conformance_worker
    resp = await bridge.send("batch", {"commands": "get_status"})
    data = resp.get("data", "") or resp.get("err", "")
    assert "ok:" in data, f"batch smoke failed: {data[:300]}"
