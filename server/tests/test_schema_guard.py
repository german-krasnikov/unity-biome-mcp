"""Tests for SchemaGuard pre-flight validator. TDD: Red → Green → Refactor."""
import pytest

from unity_mcp.schema_cache import SchemaCache


@pytest.fixture(autouse=True)
def _enable_validate(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "1")
from unity_mcp.schema_guard import SchemaGuard
from unity_mcp.middleware import Middleware
from helpers import csharp_schema


SCHEMA_HEALTH = csharp_schema("Health", {"hp": "int", "maxHp": "int"})
SCHEMA_EMPTY = "Type not found: NoSuchType"


async def fake_send(cmd, args):
    if cmd == "get_schema":
        type_name = args.get("type", "")
        if type_name == "Health":
            return {"data": SCHEMA_HEALTH}
        return {"data": SCHEMA_EMPTY}
    return {"data": ""}


def make_guard():
    mw = Middleware()
    cache = SchemaCache()
    guard = SchemaGuard(mw, cache)
    return mw, cache, guard


# ── 1. Valid passthrough ──────────────────────────────────────────────────────

async def test_valid_passthrough():
    """comp in cache, prop in schema → None (pass)."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}
    cache.put("Health", frozenset({"hp", "maxHp"}))

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hp", "value": "50"},
        fake_send,
    )
    assert result is None


# ── 2. Invalid component lev≤2 → block ───────────────────────────────────────

async def test_invalid_component_lev2_block():
    """'Healt' (lev=1 from 'Health') → block envelope contains 'Health'."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Healt", "prop": "hp", "value": "50"},
        fake_send,
    )
    assert result is not None
    assert "Health" in result
    assert "[INVALID:" in result
    assert "[BYPASS:" in result


# ── 3. Invalid component lev>5 → pass ────────────────────────────────────────

async def test_invalid_component_lev6_pass():
    """'QuantumFlux' (lev>>5 from 'Health') → None (let Unity error)."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "QuantumFlux", "prop": "hp", "value": "50"},
        fake_send,
    )
    assert result is None


# ── 4. Invalid prop → block ───────────────────────────────────────────────────

async def test_invalid_prop_block():
    """Known comp, prop 'hpp' (lev=1 from 'hp') → block, suggests 'hp'."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}
    cache.put("Health", frozenset({"hp", "maxHp"}))

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hpp", "value": "50"},
        fake_send,
    )
    assert result is not None
    assert "hp" in result
    assert "[INVALID:" in result
    assert "[BYPASS:" in result



# ── 6. UNITY_MCP_VALIDATE=0 → guard not instantiated ─────────────────────────

def test_env_bypass(monkeypatch):
    """UNITY_MCP_VALIDATE=0 → middleware doesn't instantiate guard."""
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    mw = Middleware()
    assert mw.schema_guard is None
    assert mw.schema_cache is None


# ── 7. Cache hit — no fetch ───────────────────────────────────────────────────

async def test_cache_hit_no_fetch():
    """Second validate of same comp → 0 send_fn invocations."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}
    cache.put("Health", frozenset({"hp", "maxHp"}))

    fetch_calls = []
    async def counting_send(cmd, args):
        fetch_calls.append(cmd)
        return {"data": SCHEMA_HEALTH}

    await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hp", "value": "1"},
        counting_send,
    )
    await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "maxHp", "value": "100"},
        counting_send,
    )
    assert fetch_calls == []


# ── 8. Cache invalidate → cache miss on next call ─────────────────────────────

async def test_cache_invalidate_recompile():
    """invalidate_all → next call hits cache_miss (fetch is called)."""
    from unity_mcp.metrics import METRICS
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}
    cache.put("Health", frozenset({"hp", "maxHp"}))

    fetch_calls = []
    async def counting_send(cmd, args):
        fetch_calls.append(cmd)
        return {"data": SCHEMA_HEALTH}

    # warm call
    await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hp", "value": "1"},
        counting_send,
    )
    assert fetch_calls == []

    cache.invalidate_all()
    before = METRICS._counters.get("validate.cache_miss", 0)

    await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hp", "value": "1"},
        counting_send,
    )
    assert "get_schema" in fetch_calls
    assert METRICS._counters.get("validate.cache_miss", 0) > before


# ── 9. ObjectReference value → skip validation ───────────────────────────────

async def test_objectref_value_skipped():
    """value='/Other/Obj' → no validation attempted, returns None."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}

    fetch_calls = []
    async def counting_send(cmd, args):
        fetch_calls.append(cmd)
        return {"data": ""}

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "target", "value": "/Other/Obj"},
        counting_send,
    )
    assert result is None
    assert fetch_calls == []


# ── #1 manage_component add envelope must have [KNOWN:] line ─────────────────

async def test_manage_add_envelope_has_known_line():
    """_validate_manage_add returns 4-line envelope with [KNOWN:] line (#1)."""
    mw, cache, guard = make_guard()
    # populate component cache so _known_types() returns something
    mw._component_cache["/Player"] = {"Rigidbody", "Transform"}
    # empty props → type not found
    cache.put("NoSuchType", frozenset())

    result = await guard.validate(
        "manage_component",
        {"action": "add", "type": "NoSuchType"},
        fake_send,
    )
    assert result is not None
    assert "[INVALID:" in result
    assert "[FIX:" in result
    assert "[KNOWN:" in result
    assert "[BYPASS:" in result


# ── #2 no_validate (no underscore) does NOT bypass guard ─────────────────────

async def test_manage_add_bare_no_validate_does_not_bypass():
    """no_validate (without underscore) is not a bypass flag — guard still blocks."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Rigidbody"}
    cache.put("NoSuchType", frozenset())

    result = await guard.validate(
        "manage_component",
        {"action": "add", "type": "NoSuchType", "no_validate": True},
        fake_send,
    )
    assert result is not None, "bare no_validate must NOT bypass guard"
    assert "[INVALID:" in result


# ── #4 test_no_validate_arg_bypass also asserts bridge was called ─────────────
# (covered by strengthening test_no_validate_arg_bypass below at #4b)


# ── #5 wire_event valid target passthrough ────────────────────────────────────

async def test_wire_event_valid_target_passthrough():
    """wire_event with valid target_component in cache → returns None (#5)."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Button"] = {"ButtonHandler", "Transform"}

    result = await guard.validate(
        "wire_event",
        {"target_path": "/Button", "target_component": "ButtonHandler"},
        fake_send,
    )
    assert result is None


# ── #4b strengthen test_no_validate_arg_bypass ────────────────────────────────

async def test_no_validate_arg_bypass_calls_bridge():
    """_no_validate=True → SchemaGuard does NOT block; bridge fires for valid component (#4).

    Uses exact component name so component-existence check also passes.
    """
    from unity_mcp.middleware import wrap_send
    mw2 = Middleware()
    mw2._component_cache["/Player"] = {"Health"}
    mw2.schema_cache = SchemaCache()
    # pre-populate schema cache with bad prop so guard WOULD block without bypass
    mw2.schema_cache.put("Health", frozenset({"hp", "maxHp"}))
    mw2.schema_guard = SchemaGuard(mw2, mw2.schema_cache)

    calls = []
    async def tracked_send(cmd, args, timeout=30.0):
        calls.append(cmd)
        return "ok"

    wrapped = wrap_send(tracked_send, mw2)
    # "hpp" would be blocked (lev=1) but _no_validate=True skips guard
    # component "Health" is exact — passes component-existence check
    result = await wrapped(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hpp", "value": "50", "_no_validate": True},
    )
    assert "set_property" in calls
    assert "[INVALID:" not in result


# ─── LRU eviction ORDER: SchemaCache ─────────────────────────────────────────

def test_schema_cache_evicts_oldest_not_newest():
    """Fill SchemaCache to max_size=3, add 4th; oldest type evicted, newest kept."""
    cache = SchemaCache(max_size=3)
    cache.put("TypeA", frozenset({"a"}))
    cache.put("TypeB", frozenset({"b"}))
    cache.put("TypeC", frozenset({"c"}))
    cache.put("TypeD", frozenset({"d"}))  # overflow — TypeA is oldest
    assert cache.get("TypeA") is None          # oldest evicted
    assert cache.get("TypeB") == frozenset({"b"})
    assert cache.get("TypeC") == frozenset({"c"})
    assert cache.get("TypeD") == frozenset({"d"})  # newest kept


# ── P2: SchemaCache.parse positive path ──────────────────────────────────────

def test_schema_cache_parse_valid_schema():
    """parse() on a well-formed schema text returns frozenset of prop names."""
    schema_text = "Schema: Health\n  hp: int\n  maxHp: int\n  shield: float"
    result = SchemaCache.parse(schema_text)
    assert isinstance(result, frozenset)
    assert "hp" in result
    assert "maxHp" in result
    assert "shield" in result
    assert "Schema" not in result  # header line excluded


def test_schema_cache_parse_type_not_found_returns_empty():
    result = SchemaCache.parse("Type not found: NoSuchType")
    assert result == frozenset()


def test_schema_cache_parse_empty_string_returns_empty():
    assert SchemaCache.parse("") == frozenset()


def test_schema_cache_parse_cannot_instantiate_returns_empty():
    result = SchemaCache.parse("Cannot instantiate abstract type Foo")
    assert result == frozenset()


def test_schema_cache_parse_skips_comment_lines():
    schema_text = "Schema: Foo\n  # this is a comment\n  health: int"
    result = SchemaCache.parse(schema_text)
    assert "health" in result
    # comment line should not produce a prop named "# this is a comment"
    assert all(not p.startswith("#") for p in result)


# ── P2: SchemaGuard with unknown command ──────────────────────────────────────

async def test_guard_unknown_command_returns_none():
    """Unknown command (not set_property/manage_component/wire_event) → None (pass-through)."""
    mw, cache, guard = make_guard()
    result = await guard.validate(
        "get_hierarchy",
        {"path": "/"},
        fake_send,
    )
    assert result is None


async def test_guard_batch_command_returns_none():
    """'batch' command → None (pass-through, not a mutation guard target)."""
    mw, cache, guard = make_guard()
    result = await guard.validate(
        "batch",
        {"commands": []},
        fake_send,
    )
    assert result is None


# ── 10. Exception in send_fn → fail-open ─────────────────────────────────────

async def test_exception_fail_open():
    """Broken send_fn raises → returns None, validate.error counter incremented."""
    from unity_mcp.metrics import METRICS
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Health"}
    # No cache for Health → will try to fetch

    before = METRICS._counters.get("validate.error", 0)

    async def broken_send(cmd, args):
        raise RuntimeError("connection lost")

    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Health", "prop": "hp", "value": "50"},
        broken_send,
    )
    assert result is None
    assert METRICS._counters.get("validate.error", 0) == before + 1


# ── PY3.test.4: _validate_set_property early-exit branches ───────────────────

async def test_dollar_path_skips_validation():
    """path='$obj' → skip validation, no TCP call."""
    mw, cache, guard = make_guard()
    mw._component_cache["$obj"] = {"Health"}
    result = await guard.validate(
        "set_property",
        {"path": "$obj", "component": "Health", "prop": "hpp", "value": "1"},
        None,  # send_fn=None proves no TCP call attempted
    )
    assert result is None


async def test_m_prefix_prop_skipped():
    """prop='m_LocalPosition' → skip validation (Unity internal field)."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Player"] = {"Transform"}
    result = await guard.validate(
        "set_property",
        {"path": "/Player", "component": "Transform", "prop": "m_LocalPosition", "value": "0,0,0"},
        None,  # send_fn=None proves no TCP call attempted
    )
    assert result is None


# ── PY3.test.5: wire_event early-exit branches ───────────────────────────────

async def test_wire_event_missing_target_path():
    """wire_event without target_path → None (early exit, no TCP)."""
    mw, cache, guard = make_guard()
    result = await guard.validate(
        "wire_event",
        {"target_component": "ButtonHandler"},
        None,
    )
    assert result is None


async def test_wire_event_missing_target_component():
    """wire_event without target_component → None (early exit, no TCP)."""
    mw, cache, guard = make_guard()
    result = await guard.validate(
        "wire_event",
        {"target_path": "/Button"},
        None,
    )
    assert result is None


# ── PY3.test.6: wire_event invalid component blocked ─────────────────────────

async def test_wire_event_invalid_component_blocked():
    """wire_event with typo in target_component (lev≤2) → block envelope."""
    mw, cache, guard = make_guard()
    mw._component_cache["/Button"] = {"ButtonHandler"}
    result = await guard.validate(
        "wire_event",
        {"target_path": "/Button", "target_component": "ButtonHandlr"},  # lev=1
        fake_send,
    )
    assert result is not None
    assert "[INVALID:" in result
    assert "ButtonHandler" in result
    assert "[BYPASS:" in result
