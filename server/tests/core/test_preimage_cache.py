"""Tests for PrefetchCache.put_synthetic — preimage caching from reflect snapshots."""
import time
from unity_mcp.prefetch_cache import PrefetchCache


def test_put_synthetic_stores_with_marker():
    c = PrefetchCache()
    c.put_synthetic("get_component", {"path": "/A", "type": "Health"}, "[Health]\nhp: 100", source="reflect-snapshot")
    result = c.get("get_component", {"path": "/A", "type": "Health"})
    assert "[CACHED:reflect-snapshot]" in result
    assert "[Health]" in result
    assert "hp: 100" in result


def test_put_synthetic_increments_stats():
    c = PrefetchCache()
    c.put_synthetic("get_component", {"path": "/A"}, "body")
    assert c.stats()["puts_synthetic"] == 1


def test_put_synthetic_same_ttl_as_regular(monkeypatch):
    """Synthetic TTL == regular TTL (0.5s). Expired after TTL elapses."""
    c = PrefetchCache(ttl=0.5)
    fake_time = [time.monotonic()]
    monkeypatch.setattr("unity_mcp.prefetch_cache.time.monotonic", lambda: fake_time[0])
    c.put_synthetic("get_component", {"path": "/A"}, "body")
    # Within TTL — should hit
    fake_time[0] += 0.3
    assert c.get("get_component", {"path": "/A"}) is not None
    # Past TTL — should miss
    fake_time[0] += 0.3  # total 0.6 > 0.5
    assert c.get("get_component", {"path": "/A"}) is None


def test_put_synthetic_eviction_on_full():
    c = PrefetchCache(max_size=2)
    c.put_synthetic("a", {"x": "1"}, "r1")
    c.put_synthetic("b", {"x": "1"}, "r2")
    c.put_synthetic("c", {"x": "1"}, "r3")
    assert c.get("a", {"x": "1"}) is None  # evicted


def test_put_synthetic_invalidate_path():
    c = PrefetchCache()
    c.put_synthetic("get_component", {"path": "/A", "type": "X"}, "body")
    c.invalidate_path("/A")
    assert c.get("get_component", {"path": "/A", "type": "X"}) is None
