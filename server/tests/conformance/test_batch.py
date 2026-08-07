"""Gate 6: Batch Operations — multi-command execution."""
from __future__ import annotations

import pytest

pytestmark = pytest.mark.live


async def test_batch_two_reads(conformance_worker):
    """Batch with 2 read commands returns results for both."""
    worker, bridge = conformance_worker
    resp = await bridge.send("batch", {
        "commands": "get_status\nget_compile_errors",
    })
    data = resp.get("data", "") or resp.get("err", "")
    assert "[0]" in data or "ok:" in data, f"batch response malformed: {data[:300]}"
    assert "ok:" in data, f"batch missing summary: {data[:300]}"


async def test_batch_write_then_readback(conformance_worker):
    """Batch create + find verifies mutation in single batch."""
    worker, bridge = conformance_worker
    name = f"{worker.scene_ns}_batch"
    try:
        commands = f"create_object name={name}\nfind_objects name={name}"
        resp = await bridge.send("batch", {"commands": commands})
        data = resp.get("data", "") or resp.get("err", "")
        assert name in data, f"batch didn't reflect create: {data[:300]}"
    finally:
        await bridge.send("delete_object", {"path": f"/{name}"})


async def test_batch_on_error_continue(conformance_worker):
    """Batch with one invalid op continues to next."""
    worker, bridge = conformance_worker
    commands = "nonexistent_cmd_xyz\nget_status"
    resp = await bridge.send("batch", {
        "commands": commands,
        "on_error": "continue",
    })
    data = resp.get("data", "") or resp.get("err", "")
    assert "err:" in data, f"batch missing error marker: {data[:300]}"
    assert "ok:" in data, f"batch didn't continue after error: {data[:300]}"
