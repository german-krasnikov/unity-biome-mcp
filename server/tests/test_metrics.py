"""TDD tests for metrics.py — MetricsRegistry, helpers, decorators."""
import asyncio
import os
import time
import pytest


# ── 1. inc ────────────────────────────────────────────────────────────────────

def test_metrics_inc_increments():
    from unity_mcp.metrics import METRICS
    METRICS.inc("test.calls")
    METRICS.inc("test.calls")
    assert METRICS.snapshot()["counters"]["test.calls"] == 2


def test_metrics_inc_default_n_is_1():
    from unity_mcp.metrics import METRICS
    METRICS.inc("x")
    assert METRICS.snapshot()["counters"]["x"] == 1


def test_metrics_inc_by_n():
    from unity_mcp.metrics import METRICS
    METRICS.inc("x", 5)
    assert METRICS.snapshot()["counters"]["x"] == 5


# ── 2. observe ────────────────────────────────────────────────────────────────

def test_metrics_observe_appends():
    from unity_mcp.metrics import METRICS
    METRICS.observe("latency", 10.0)
    METRICS.observe("latency", 20.0)
    snap = METRICS.snapshot()["observations"]["latency"]
    assert snap["n"] == 2
    assert snap["avg"] == pytest.approx(15.0)


def test_metrics_observe_maxlen_256():
    from unity_mcp.metrics import METRICS
    for i in range(300):
        METRICS.observe("x", float(i))
    assert METRICS.snapshot()["observations"]["x"]["n"] == 256


# ── 3. timer ──────────────────────────────────────────────────────────────────

def test_metrics_timer_records_ms():
    from unity_mcp.metrics import METRICS
    with METRICS.timer("op"):
        time.sleep(0.01)
    snap = METRICS.snapshot()["observations"]["op"]
    assert snap["n"] == 1
    assert snap["avg"] >= 5.0  # at least 5ms


# ── 4. cost ───────────────────────────────────────────────────────────────────

def test_metrics_cost_calculates_usd():
    from unity_mcp.metrics import METRICS, HAIKU_IN_PER_MTOK, HAIKU_OUT_PER_MTOK
    METRICS.cost("sampling", "haiku", 1_000_000, 1_000_000)
    costs = METRICS.snapshot()["costs_usd"]
    expected = HAIKU_IN_PER_MTOK + HAIKU_OUT_PER_MTOK
    assert costs["sampling"] == pytest.approx(expected)
    assert costs["__total__"] == pytest.approx(expected)


def test_metrics_cost_accumulates():
    from unity_mcp.metrics import METRICS
    METRICS.cost("sampling", "haiku", 0, 0)
    METRICS.cost("sampling", "haiku", 1_000_000, 0)
    costs = METRICS.snapshot()["costs_usd"]
    assert costs["__total__"] > 0


# ── 5. snapshot format ────────────────────────────────────────────────────────

def test_metrics_snapshot_format():
    from unity_mcp.metrics import METRICS
    METRICS.inc("a")
    METRICS.observe("b", 1.0)
    snap = METRICS.snapshot()
    assert "uptime_s" in snap
    assert "counters" in snap
    assert "observations" in snap
    assert "costs_usd" in snap
    assert snap["uptime_s"] >= 0


# ── 6. reset ──────────────────────────────────────────────────────────────────

def test_metrics_reset_clears():
    from unity_mcp.metrics import METRICS
    METRICS.inc("x")
    METRICS.observe("y", 1.0)
    METRICS.cost("f", "haiku", 100, 100)
    METRICS.reset()
    snap = METRICS.snapshot()
    assert snap["counters"] == {}
    assert snap["observations"] == {}
    assert snap["costs_usd"] == {}


# ── 7. jsonl sink disabled by default ────────────────────────────────────────

def test_metrics_jsonl_disabled_by_default():
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    assert m._jsonl_f is None


# ── 8. jsonl sink enabled writes file ────────────────────────────────────────

def test_metrics_jsonl_enabled_writes_file(tmp_path, monkeypatch):
    monkeypatch.setenv("UNITY_MCP_METRICS", "1")
    monkeypatch.setenv("UNITY_MCP_LOG_DIR", str(tmp_path))
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    m.event("test_event", foo="bar")
    m._close()
    log = tmp_path / "metrics.jsonl"
    assert log.exists()
    content = log.read_text(encoding="utf-8")
    assert "test_event" in content
    assert "bar" in content


# ── 9. _p95 with few samples uses max ────────────────────────────────────────

def test_p95_with_few_samples_uses_max():
    from unity_mcp.metrics import _p95, MetricsRegistry
    # When n < 20, snapshot uses max()
    m = MetricsRegistry()
    for v in [1.0, 5.0, 3.0]:
        m.observe("x", v)
    snap = m.snapshot()["observations"]["x"]
    assert snap["p95"] == 5.0  # max of few samples


# ── 10. _p95 with many samples ────────────────────────────────────────────────

def test_p95_with_many_samples_correct():
    from unity_mcp.metrics import _p95
    # 100 values 1..100; p95 should be around 95-96
    seq = list(range(1, 101))
    result = _p95(seq)
    assert 94 <= result <= 96


# ── 11. timed decorator async ────────────────────────────────────────────────

async def test_timed_decorator_async():
    from unity_mcp.metrics import METRICS, timed

    @timed("async_op")
    async def my_async():
        time.sleep(0.01)
        return "ok"

    result = await my_async()
    assert result == "ok"
    snap = METRICS.snapshot()["observations"]["async_op"]
    assert snap["n"] == 1
    assert snap["avg"] >= 5.0


# ── 12. timed decorator sync ─────────────────────────────────────────────────

def test_timed_decorator_sync():
    from unity_mcp.metrics import METRICS, timed

    @timed("sync_op")
    def my_sync():
        time.sleep(0.01)
        return "done"

    result = my_sync()
    assert result == "done"
    snap = METRICS.snapshot()["observations"]["sync_op"]
    assert snap["n"] == 1
    assert snap["avg"] >= 5.0


# ── 13. counted decorator async ──────────────────────────────────────────────

async def test_counted_decorator_async():
    from unity_mcp.metrics import METRICS, counted

    @counted("my_count")
    async def my_fn():
        return "x"

    await my_fn()
    await my_fn()
    assert METRICS.snapshot()["counters"]["my_count"] == 2


def test_counted_decorator_sync():
    from unity_mcp.metrics import METRICS, counted

    @counted("sync_count")
    def my_fn():
        return "x"

    my_fn()
    assert METRICS.snapshot()["counters"]["sync_count"] == 1


# ── 14. format_report includes caches ────────────────────────────────────────

def test_format_report_includes_caches():
    from unity_mcp.metrics import METRICS
    METRICS.inc("fpcache.hit", 8)
    METRICS.inc("fpcache.miss", 2)
    report = METRICS.format_report()
    assert "fpcache" in report
    assert "80%" in report  # 8/(8+2) = 80%


# ── 15. format_report includes speculation hit rate ──────────────────────────

def test_format_report_includes_speculation_hit_rate():
    from unity_mcp.metrics import METRICS
    METRICS.inc("speculation.predict", 10)
    METRICS.inc("speculation.hit", 7)
    METRICS.inc("speculation.miss", 3)
    report = METRICS.format_report()
    assert "Speculation" in report
    assert "70%" in report  # 7/10 = 70%


# ── Zone #30 gap tests ────────────────────────────────────────────────────────

def test_snapshot_and_reset_returns_correct_snapshot_then_zeros():
    """snapshot_and_reset returns populated snapshot, then metrics are cleared."""
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    m.inc("test.key", 5)
    m.observe("test.obs", 42.0)
    snap = m.snapshot_and_reset()
    # Snapshot has the data
    assert snap["counters"]["test.key"] == 5
    assert snap["observations"]["test.obs"]["n"] == 1
    # After reset, new snapshot is empty
    fresh = m.snapshot()
    assert fresh["counters"] == {}
    assert fresh["observations"] == {}


def test_format_report_sampling_section():
    """format_report includes [Sampling/Haiku] section with calls/success/fail counts."""
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    m.inc("sampling.calls", 10)
    m.inc("sampling.success", 8)
    m.inc("sampling.fail", 2)
    report = m.format_report()
    assert "Sampling" in report
    assert "calls=10" in report
    assert "success=8" in report
    assert "fail=2" in report


def test_format_report_lessons_section():
    """format_report includes [Lessons] section when lessons.* counters present."""
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    m.inc("lessons.recorded", 3)
    m.inc("lessons.hint_emitted", 1)
    report = m.format_report()
    assert "Lessons" in report
    assert "recorded=3" in report
    assert "hint_emitted=1" in report


def test_format_report_latency_percentile_section():
    """format_report includes [Top commands by latency] when cmd.*.ms observations exist."""
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    for _ in range(25):
        m.observe("cmd.get_component.ms", 50.0)
    report = m.format_report()
    assert "latency" in report
    assert "get_component" in report


def test_format_report_empty_data():
    """format_report with no events only contains the header line."""
    from unity_mcp.metrics import MetricsRegistry
    m = MetricsRegistry()
    report = m.format_report()
    assert "Unity Biome MCP Metrics" in report
    assert "Sampling" not in report
    assert "Lessons" not in report
    assert "Hinter" not in report
    assert "Speculation" not in report
