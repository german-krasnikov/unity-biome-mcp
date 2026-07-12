"""Lifecycle and pipeline tests for alias resolution via wrap_send.

Tests in A/B/E/F categories exercise the two middleware hooks together:
  Hook 1: resolve $name before send
  Hook 2: parse --- ALIASES --- from get_hierarchy response
"""
import pytest
from unity_mcp.middleware_pipeline import wrap_send

# ── Shared fixtures / helpers ─────────────────────────────────────────────────

HIER_WITH_ALIASES = """\
--- ALIASES ---
player=/Player|PlayerController|
hp=/Player|HP|health
enemy=/Enemies/Slime|Enemy|
---
[Scene]
├── Player
└── Enemies"""

HIER_NO_ALIASES = """\
[Scene]
├── Player
└── Enemies"""

HIER_NO_CHANGE = "NO_CHANGE"

HIER_UPDATED_ALIASES = """\
--- ALIASES ---
player=/Player2|PlayerController|
---
[Scene]
└── Player2"""


def _make_mw(monkeypatch):
    """Create Middleware with all noise disabled. monkeypatch MUST be provided."""
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")
    monkeypatch.setenv("UNITY_MCP_DISAMBIG", "0")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.schema_guard = None
    return mw


# ── Category A: Alias Lifecycle ───────────────────────────────────────────────

async def test_lifecycle_A1_first_hierarchy_fills_cache(monkeypatch):
    """P0: get_hierarchy response populates alias cache; next call resolves $name."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})

    assert "player" in mw._alias_cache
    assert mw._alias_cache["hp"] == "/Player|HP|health"

    # $player in path resolves to /Player before TCP send
    await wrapped("get_component", {"path": "$player", "component": "PlayerController"})
    assert captured["get_component"]["path"] == "/Player"


async def test_lifecycle_A2_cache_replaced_on_new_hierarchy(monkeypatch):
    """P0: Second get_hierarchy with changed aliases REPLACES cache (not merges)."""
    mw = _make_mw(monkeypatch)
    responses = {"get_hierarchy": HIER_WITH_ALIASES}

    async def fake_send(cmd, args, timeout=0):
        return responses.get(cmd, "ok")

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    assert mw._alias_cache["player"] == "/Player|PlayerController|"

    responses["get_hierarchy"] = HIER_UPDATED_ALIASES
    await wrapped("get_hierarchy", {})

    assert mw._alias_cache["player"] == "/Player2|PlayerController|"
    assert "hp" not in mw._alias_cache    # old key gone
    assert "enemy" not in mw._alias_cache


async def test_lifecycle_A3_no_change_preserves_cache(monkeypatch):
    """P0: Incremental get_hierarchy (NO_CHANGE) does NOT wipe alias cache."""
    mw = _make_mw(monkeypatch)
    call_n = {"n": 0}

    async def fake_send(cmd, args, timeout=0):
        call_n["n"] += 1
        if cmd == "get_hierarchy":
            return HIER_WITH_ALIASES if call_n["n"] == 1 else HIER_NO_CHANGE
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    assert mw._alias_cache["hp"] == "/Player|HP|health"

    snapshot = dict(mw._alias_cache)
    await wrapped("get_hierarchy", {})   # returns NO_CHANGE
    assert mw._alias_cache == snapshot   # cache unchanged


async def test_lifecycle_A4_no_aliases_block_preserves_cache(monkeypatch):
    """P1: Filtered hierarchy (no ALIASES block) does NOT clear cache."""
    mw = _make_mw(monkeypatch)
    responses = {"get_hierarchy": HIER_WITH_ALIASES}

    async def fake_send(cmd, args, timeout=0):
        return responses.get(cmd, "ok")

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    assert "player" in mw._alias_cache

    responses["get_hierarchy"] = HIER_NO_ALIASES
    await wrapped("get_hierarchy", {})
    assert "player" in mw._alias_cache   # still there


def test_lifecycle_A5_reset_session_clears_cache():
    """P1: reset_session() must clear alias cache (stale aliases after reconnect)."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw._alias_cache = {"player": "/Player|PlayerController|", "hp": "/Player|HP|health"}
    mw.reset_session()
    assert mw._alias_cache == {}


# ── Category B: Multi-tool Alias Resolution ───────────────────────────────────

async def test_multitool_B1_inspect_comma_separated(monkeypatch):
    """P0: inspect(paths='$player,$enemy') resolves both aliases."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("inspect", {"paths": "$player,$enemy"})

    paths = captured["inspect"]["paths"]
    assert "/Player" in paths
    assert "/Enemies/Slime" in paths
    assert "$player" not in paths
    assert "$enemy" not in paths


async def test_multitool_B2_multiple_names_one_call(monkeypatch):
    """P0: Multiple $names in one args dict each resolve independently."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("get_component", {"path": "$player", "component": "$hp"})

    assert captured["get_component"]["path"] == "/Player"
    assert captured["get_component"]["component"] == "HP"   # segment[1]


async def test_multitool_B3_queries_full_pipe(monkeypatch):
    """P0: $name in 'queries' key returns full pipe value."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("query_state", {"queries": "$hp"})

    assert captured["query_state"]["queries"] == "/Player|HP|health"


async def test_multitool_B4_batch_inner_not_resolved(monkeypatch):
    """P1: $name INSIDE batch DSL string is NOT resolved (documented limitation).
    Batch commands string is a single arg value, not a whole-value $name match."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    batch_dsl = "get_component $player PlayerController\nget_component $enemy Enemy"
    await wrapped("batch", {"commands": batch_dsl})

    assert "$player" in captured["batch"]["commands"]   # unchanged — known limitation
    assert "$enemy" in captured["batch"]["commands"]


async def test_multitool_B5_full_llm_flow(monkeypatch):
    """P0: Simulates full LLM task: hierarchy → get_component → set_property via $aliases."""
    mw = _make_mw(monkeypatch)
    log: list = []

    async def fake_send(cmd, args, timeout=0):
        log.append((cmd, dict(args)))
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("get_component", {"path": "$player", "component": "$hp"})
    await wrapped("set_property", {"path": "$player", "component": "$hp",
                                   "prop": "health", "value": "100"})

    get_args = log[1][1]
    set_args = log[2][1]
    assert get_args["path"] == "/Player"
    assert get_args["component"] == "HP"
    assert set_args["path"] == "/Player"
    assert set_args["component"] == "HP"


# ── Category E: Error Scenarios ───────────────────────────────────────────────

async def test_error_E1_unknown_dollar_name_passthrough(monkeypatch):
    """P0: Unknown $name passes through unchanged — Unity handles bad path."""
    mw = _make_mw(monkeypatch)
    captured: dict = {}

    async def fake_send(cmd, args, timeout=0):
        captured[cmd] = dict(args)
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("get_component", {"path": "$ghost"})

    assert captured["get_component"]["path"] == "$ghost"   # unchanged


# ── Category F: Pipeline Integration Order ────────────────────────────────────

async def test_pipeline_F1_resolve_before_path_guard(monkeypatch):
    """P0: $player resolves to /Player BEFORE send (Hook 1 runs early in pipeline)."""
    mw = _make_mw(monkeypatch)
    send_log: list = []

    async def fake_send(cmd, args, timeout=0):
        send_log.append((cmd, dict(args)))
        return HIER_WITH_ALIASES if cmd == "get_hierarchy" else "ok"

    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    await wrapped("get_component", {"path": "$player"})

    gc_call = next(a for c, a in send_log if c == "get_component")
    assert gc_call["path"] == "/Player"


async def test_pipeline_F2_aliases_survive_strip_defaults(monkeypatch):
    """P1: parse_aliases_from_hierarchy sees result AFTER strip_defaults.
    strip_defaults must NOT remove the ALIASES block."""
    from unity_mcp.middleware_alias import parse_aliases_from_hierarchy
    from unity_mcp.compressor import strip_defaults

    compressed = strip_defaults(HIER_WITH_ALIASES)
    cache = parse_aliases_from_hierarchy(compressed)
    # Block must survive compression
    assert cache is not None
    assert "player" in cache
    assert "hp" in cache


async def test_pipeline_F3_hook_order(monkeypatch):
    """P1: Hook 2 (parse) runs after get_hierarchy response; cache ready for NEXT call."""
    mw = _make_mw(monkeypatch)

    async def fake_send(cmd, args, timeout=0):
        return HIER_WITH_ALIASES

    wrapped = wrap_send(fake_send, mw)

    assert mw._alias_cache == {}         # before first call: empty
    await wrapped("get_hierarchy", {})
    assert "player" in mw._alias_cache   # after call: populated
    assert "hp" in mw._alias_cache


# ── Category G: Batch DSL $alias (5.3) ───────────────────────────────────────
# $alias is now expanded C#-side in AliasExpander.ExpandText() — no middleware WARN.

async def test_batch_G1_dollar_in_commands_no_warn(monkeypatch):
    """$alias in batch DSL is expanded by C#; middleware no longer warns."""
    mw = _make_mw(monkeypatch)
    mw._alias_cache = {"player": "/GridPlayer"}

    async def fake_send(cmd, args, timeout=0):
        return "ok"

    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("batch", {"commands": "get_component $player PlayerController"})
    assert "[WARN] $alias" not in result
    assert "batch DSL not supported" not in result


# ── Category H: _warm_alias_cache (S1 — auto-seed on connect) ─────────────────

async def test_warm_alias_cache_seeds_on_connect():
    """_warm_alias_cache sends get_aliases and populates _middleware._alias_cache."""
    import types
    from unittest.mock import AsyncMock, patch
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "player=/Player|PlayerController|\nhp=/Player|HP|health",
    })

    mw = types.SimpleNamespace(_alias_cache={})
    with patch.object(srv, "_middleware", mw):
        await srv._warm_alias_cache(bridge)

    bridge.send.assert_awaited_once_with("get_aliases", {})
    assert mw._alias_cache.get("player") == "/Player|PlayerController|"
    assert mw._alias_cache.get("hp") == "/Player|HP|health"


async def test_warm_alias_cache_silent_on_error():
    """_warm_alias_cache does not raise when bridge.send raises."""
    import types
    from unittest.mock import AsyncMock, patch
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.send = AsyncMock(side_effect=ConnectionError("no connection"))

    mw = types.SimpleNamespace(_alias_cache={})
    with patch.object(srv, "_middleware", mw):
        await srv._warm_alias_cache(bridge)  # must not raise

    assert mw._alias_cache == {}
