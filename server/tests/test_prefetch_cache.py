"""Tests for PrefetchCache — TTL, LRU, invalidation, GATE_PRIORS."""
import time
from unity_mcp.prefetch_cache import PrefetchCache, GATE_PRIORS, _frozen_args


def test_get_miss_returns_none():
    c = PrefetchCache()
    assert c.get("get_component", {"path": "/A"}) is None


def test_put_then_get_hit():
    c = PrefetchCache()
    c.put("get_component", {"path": "/A"}, "result-A")
    assert c.get("get_component", {"path": "/A"}) == "result-A"
    assert c.stats()["hits"] == 1


def test_ttl_expiry(monkeypatch):
    c = PrefetchCache(ttl=0.1)
    c.put("get_component", {"path": "/A"}, "v1")
    fake_time = [time.monotonic()]
    monkeypatch.setattr("unity_mcp.prefetch_cache.time.monotonic", lambda: fake_time[0])
    fake_time[0] += 0.2  # past TTL
    assert c.get("get_component", {"path": "/A"}) is None


def test_max_size_lru_eviction():
    c = PrefetchCache(max_size=2)
    c.put("a", {"x": "1"}, "r1")
    c.put("b", {"x": "1"}, "r2")
    c.put("c", {"x": "1"}, "r3")  # should evict 'a'
    assert c.get("a", {"x": "1"}) is None
    assert c.get("b", {"x": "1"}) == "r2"
    assert c.get("c", {"x": "1"}) == "r3"


def test_invalidate_path_broad():
    c = PrefetchCache()
    c.put("get_component", {"path": "/A", "type": "Health"}, "rA")
    c.put("get_component", {"path": "/A", "type": "Renderer"}, "rA2")
    c.put("get_component", {"path": "/B", "type": "Health"}, "rB")
    dropped = c.invalidate_path("/A")
    assert dropped == 2
    assert c.get("get_component", {"path": "/A", "type": "Health"}) is None
    assert c.get("get_component", {"path": "/B", "type": "Health"}) == "rB"


def test_invalidate_path_no_match():
    c = PrefetchCache()
    c.put("get_component", {"path": "/A"}, "rA")
    assert c.invalidate_path("/Z") == 0


def test_args_normalization_order_independent():
    c = PrefetchCache()
    c.put("get_component", {"path": "/A", "type": "X"}, "r")
    # Same args, different dict order
    assert c.get("get_component", {"type": "X", "path": "/A"}) == "r"


def test_underscore_args_excluded_from_key():
    c = PrefetchCache()
    c.put("get_component", {"path": "/A", "_internal": "x"}, "r")
    # _internal should not affect key
    assert c.get("get_component", {"path": "/A", "_internal": "different"}) == "r"


def test_gate_priors_set_property():
    fn = GATE_PRIORS["set_property"]
    pred = fn({"path": "/Player", "component": "Health", "prop": "hp", "value": "100"})
    assert pred == ("get_component", {"path": "/Player", "type": "Health"})


def test_gate_priors_excludes_recompile():
    """ARC-5 T1: recompile acks BEFORE Unity compiles (async write) — it must
    never drive this sync-write-assuming prefetch mechanism, or a background
    prefetch fires moments after the ack, populating a global-key cache slot
    with pre-compile data that a later get_compile_errors call could reuse."""
    assert "recompile" not in GATE_PRIORS


def test_stats_tracks_operations():
    c = PrefetchCache()
    c.put("a", {}, "r")
    c.get("a", {})  # hit
    c.get("z", {})  # miss
    c.invalidate_path("/x")
    s = c.stats()
    assert s["puts"] == 1
    assert s["hits"] == 1
    assert s["misses"] == 1


def test_gate_priors_set_property_missing_path_returns_none():
    fn = GATE_PRIORS["set_property"]
    assert fn({"component": "Health", "prop": "hp", "value": "100"}) is None
    assert fn({"path": "", "component": "Health"}) is None
    assert fn({"path": "/A", "component": ""}) is None


def test_gate_priors_wire_event_uses_target_key():
    fn = GATE_PRIORS["wire_event"]
    pred = fn({"path": "/A", "component": "Button", "event": "onClick", "target": "/B", "method": "DoThing"})
    assert pred == ("get_component", {"path": "/B", "type": ""})
    # Missing target → None
    assert fn({"path": "/A", "component": "Button"}) is None


def test_gate_priors_manage_component_remove_returns_none():
    fn = GATE_PRIORS["manage_component"]
    pred = fn({"path": "/A", "type": "Rigidbody", "action": "remove"})
    assert pred is None
    # add with missing path → None
    assert fn({"type": "Rigidbody", "action": "add"}) is None
    # add with path → returns tuple
    result = fn({"path": "/A", "type": "Rigidbody", "action": "add"})
    assert result == ("get_components_list", {"path": "/A"})


# ─── F03-ttl: default TTL is 12s ─────────────────────────────────────────────

def test_default_ttl_is_12s():
    c = PrefetchCache()
    assert c._ttl == 12.0


def test_cache_survives_5s_window(monkeypatch):
    c = PrefetchCache()
    c.put("get_component", {"path": "/A"}, "val")
    base = time.monotonic()
    monkeypatch.setattr("unity_mcp.prefetch_cache.time.monotonic", lambda: base + 5.0)
    assert c.get("get_component", {"path": "/A"}) == "val"


# ─── LRU eviction ORDER ───────────────────────────────────────────────────────

def test_prefetch_cache_evicts_oldest_not_newest():
    """Fill to max_size=3, add 4th; oldest entry evicted, newest kept."""
    c = PrefetchCache(max_size=3)
    c.put("cmd", {"k": "a"}, "r_a")
    c.put("cmd", {"k": "b"}, "r_b")
    c.put("cmd", {"k": "c"}, "r_c")
    c.put("cmd", {"k": "d"}, "r_d")  # overflow — ("cmd",k=a) is oldest
    assert c.get("cmd", {"k": "a"}) is None   # oldest evicted
    assert c.get("cmd", {"k": "b"}) == "r_b"
    assert c.get("cmd", {"k": "c"}) == "r_c"
    assert c.get("cmd", {"k": "d"}) == "r_d"  # newest kept


# ─── P-416: invalidate_by_path ────────────────────────────────────────────────

def test_invalidate_by_path_matches_id_arg():
    """P-416: invalidate_by_path drops entries where ANY arg value == path (incl. id key)."""
    c = PrefetchCache()
    c.put("get_components_list", {"id": "/Player"}, "Transform\nRigidbody")
    c.put("get_component", {"path": "/Player", "type": "Health"}, "hp=100")
    c.put("get_component", {"path": "/Enemy", "type": "Health"}, "hp=50")
    dropped = c.invalidate_by_path("/Player")
    assert dropped == 2
    assert c.get("get_components_list", {"id": "/Player"}) is None
    assert c.get("get_component", {"path": "/Player", "type": "Health"}) is None
    assert c.get("get_component", {"path": "/Enemy", "type": "Health"}) == "hp=50"


def test_invalidate_by_path_empty_path_is_noop():
    """invalidate_by_path with empty path leaves cache untouched."""
    c = PrefetchCache()
    c.put("get_component", {"path": "/A"}, "r")
    assert c.invalidate_by_path("") == 0
    assert c.get("get_component", {"path": "/A"}) == "r"
