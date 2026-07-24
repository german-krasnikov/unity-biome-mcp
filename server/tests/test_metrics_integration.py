"""Integration tests: sampling, speculation, visual_diff, get_metrics tool."""
import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


@pytest.fixture(autouse=True)
def reset_metrics():
    from unity_mcp.metrics import METRICS
    METRICS.reset()
    yield
    METRICS.reset()


# ── 1. sampling increments metrics ───────────────────────────────────────────

def _make_mock_proc(stdout: bytes):
    """Create a mock asyncio Process whose communicate() returns (stdout, b'')."""
    proc = MagicMock()
    proc.returncode = 0
    proc.communicate = AsyncMock(return_value=(stdout, b""))
    proc.kill = MagicMock()
    proc.wait = AsyncMock(return_value=0)
    return proc


async def test_sampling_increments_calls():
    from unity_mcp.sampling import SamplingService
    from unity_mcp.metrics import METRICS
    svc = SamplingService()
    proc = _make_mock_proc(b"PASS")
    with patch("asyncio.create_subprocess_exec", new=AsyncMock(return_value=proc)):
        await svc._run(["claude", "-p", "hi"], 5.0)
    assert METRICS.snapshot()["counters"]["sampling.calls"] == 1


async def test_sampling_increments_success():
    from unity_mcp.sampling import SamplingService
    from unity_mcp.metrics import METRICS
    svc = SamplingService()
    proc = _make_mock_proc(b"PASS")
    with patch("asyncio.create_subprocess_exec", new=AsyncMock(return_value=proc)):
        await svc._run(["claude", "-p", "hi"], 5.0)
    assert METRICS.snapshot()["counters"]["sampling.success"] == 1


async def test_sampling_increments_fail_on_none():
    from unity_mcp.sampling import SamplingService
    from unity_mcp.metrics import METRICS
    svc = SamplingService()
    proc = _make_mock_proc(b"")  # empty stdout → None result → fail
    with patch("asyncio.create_subprocess_exec", new=AsyncMock(return_value=proc)):
        await svc._run(["claude", "-p", "hi"], 5.0)
    assert METRICS.snapshot()["counters"]["sampling.fail"] == 1


# ── 2. speculation records hit/miss ──────────────────────────────────────────

def test_speculation_records_hit():
    from unity_mcp.speculation import SpeculativeLayer
    from unity_mcp.metrics import METRICS
    layer = SpeculativeLayer(send_fn=AsyncMock(), enabled=True)
    layer._last_prediction = "get_component"
    layer.record_actual_next("get_component")
    assert METRICS.snapshot()["counters"]["speculation.hit"] == 1
    assert METRICS.snapshot()["counters"].get("speculation.miss", 0) == 0


def test_speculation_records_miss():
    from unity_mcp.speculation import SpeculativeLayer
    from unity_mcp.metrics import METRICS
    layer = SpeculativeLayer(send_fn=AsyncMock(), enabled=True)
    layer._last_prediction = "get_component"
    layer.record_actual_next("get_console")
    assert METRICS.snapshot()["counters"]["speculation.miss"] == 1
    assert METRICS.snapshot()["counters"].get("speculation.hit", 0) == 0


def test_speculation_records_predict():
    from unity_mcp.speculation import SpeculativeLayer
    from unity_mcp.metrics import METRICS
    layer = SpeculativeLayer(send_fn=AsyncMock(), enabled=True)
    layer._last_prediction = "get_component"
    layer.record_actual_next("get_component")
    assert METRICS.snapshot()["counters"]["speculation.predict"] == 1


# ── 3. visual_diff records cache hit/miss ────────────────────────────────────

def test_visual_diff_cache_miss_increments():
    """DiffCache.get increments diffcache.miss when key absent."""
    from unity_mcp.visual_diff import DiffCache
    from unity_mcp.metrics import METRICS
    cache = DiffCache()
    cache.get("nonexistent_key")
    assert METRICS.snapshot()["counters"]["diffcache.miss"] == 1


def test_visual_diff_cache_hit_increments():
    """DiffCache.get increments diffcache.hit after put."""
    from unity_mcp.visual_diff import DiffCache
    from unity_mcp.metrics import METRICS
    cache = DiffCache()
    cache.put("k", "v")
    cache.get("k")
    assert METRICS.snapshot()["counters"]["diffcache.hit"] == 1


# ── 4. get_metrics tool returns text ─────────────────────────────────────────

async def test_get_metrics_tool_returns_text():
    from unity_mcp.tools.metrics_tool import get_metrics
    from unity_mcp.metrics import METRICS
    METRICS.inc("sampling.calls", 3)
    result = await get_metrics(format="text")
    assert "Unity Biome MCP Metrics" in result
    assert isinstance(result, str)


async def test_get_metrics_tool_returns_json():
    from unity_mcp.tools.metrics_tool import get_metrics
    import json
    result = await get_metrics(format="json")
    data = json.loads(result)
    assert "counters" in data
    assert "observations" in data


async def test_get_metrics_tool_reset_clears():
    from unity_mcp.tools.metrics_tool import get_metrics
    from unity_mcp.metrics import METRICS
    METRICS.inc("x", 5)
    await get_metrics(reset=True)
    assert METRICS.snapshot()["counters"] == {}
