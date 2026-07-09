"""Unit tests for middleware_alias pure functions.

All tests are $0 cost — no bridge, no Unity required.
"""
import pytest
from unity_mcp.middleware_alias import parse_aliases_from_hierarchy, resolve_aliases_in_args

# ── GridTest data ─────────────────────────────────────────────────────────────

GRIDTEST_HIER = (
    "--- ALIASES ---\n"
    "player=/GridPlayer\n"
    "c1=/Collectible_1\n"
    "c2=/Collectible_2\n"
    "c3=/Collectible_3\n"
    "light=/Directional Light\n"
    "cam=/Main Camera\n"
    "---\n"
    "GridPlayer\nCollectible_1\nCollectible_2\nCollectible_3\n"
    "Plane\nDirectional Light\nMain Camera\n"
)

GRIDTEST_CACHE: dict[str, str] = {
    "player": "/GridPlayer",
    "c1": "/Collectible_1",
    "c2": "/Collectible_2",
    "c3": "/Collectible_3",
    "light": "/Directional Light",
    "cam": "/Main Camera",
}

# ── parse: no-block cases (return None) ──────────────────────────────────────

def test_parse_empty_string_returns_none():
    assert parse_aliases_from_hierarchy("") is None


def test_parse_no_block_returns_none():
    assert parse_aliases_from_hierarchy("[Scene]\n├── Player") is None


def test_parse_no_change_returns_none():
    assert parse_aliases_from_hierarchy("NO_CHANGE") is None


# ── parse: block found cases ─────────────────────────────────────────────────

def test_parse_basic_single_alias():
    text = "--- ALIASES ---\nhp=/Player|HP|health\n---\n[tree]"
    assert parse_aliases_from_hierarchy(text) == {"hp": "/Player|HP|health"}


def test_parse_multiple_aliases():
    text = "--- ALIASES ---\na=p|c|f\nb=q|d|g\n---"
    assert parse_aliases_from_hierarchy(text) == {"a": "p|c|f", "b": "q|d|g"}


def test_parse_cached_prefix_handled():
    """[CACHED] prefix before the ALIASES block must still parse correctly."""
    text = "[CACHED]\n--- ALIASES ---\nhp=/P|C|f\n---\n[tree]"
    assert parse_aliases_from_hierarchy(text) == {"hp": "/P|C|f"}


def test_parse_empty_block_returns_empty_dict():
    text = "--- ALIASES ---\n---\n[tree]"
    assert parse_aliases_from_hierarchy(text) == {}


def test_parse_missing_equals_skipped():
    text = "--- ALIASES ---\nbroken_line\nhp=/P|C|f\n---"
    result = parse_aliases_from_hierarchy(text)
    assert result == {"hp": "/P|C|f"}


def test_parse_unicode_path():
    text = "--- ALIASES ---\nhp=/Игрок|HP|health\n---"
    assert parse_aliases_from_hierarchy(text) == {"hp": "/Игрок|HP|health"}


def test_parse_partition_first_equals():
    """Value may itself contain '=' — partition on FIRST '=' only."""
    text = "--- ALIASES ---\nobj=path=weird/name|Comp|\n---\n[Scene]"
    result = parse_aliases_from_hierarchy(text)
    assert result == {"obj": "path=weird/name|Comp|"}


def test_parse_malformed_skipped_gracefully():
    """Mix of valid, no-equals, and empty-name lines."""
    text = (
        "--- ALIASES ---\n"
        "player=/Player|PlayerController|\n"
        "NO_EQUALS_HERE\n"
        "=empty_name\n"
        "valid=/V|C|f\n"
        "---\n"
        "[Scene]"
    )
    result = parse_aliases_from_hierarchy(text)
    assert result.get("player") == "/Player|PlayerController|"
    assert result.get("valid") == "/V|C|f"
    assert "" not in result
    assert "NO_EQUALS_HERE" not in result


# ── parse: GridTest scenarios ─────────────────────────────────────────────────

def test_parse_gridtest_all_entries():
    result = parse_aliases_from_hierarchy(GRIDTEST_HIER)
    assert result["player"] == "/GridPlayer"
    assert result["c1"] == "/Collectible_1"
    assert result["c2"] == "/Collectible_2"
    assert result["c3"] == "/Collectible_3"
    assert result["cam"] == "/Main Camera"


def test_parse_space_in_path():
    """'/Directional Light' has space — must not be split."""
    result = parse_aliases_from_hierarchy(GRIDTEST_HIER)
    assert result["light"] == "/Directional Light"


def test_parse_unclosed_block_no_crash():
    """No '---' footer — must not raise, returns dict."""
    text = "--- ALIASES ---\nhp=/Player|HP|health\n[Scene]\n├── Player"
    result = parse_aliases_from_hierarchy(text)
    assert isinstance(result, dict)


def test_parse_alias_name_underscore_digits():
    text = "--- ALIASES ---\nplayer_1=/P1|C|f\n_hidden=/H|C|f\n---\n[Scene]"
    result = parse_aliases_from_hierarchy(text)
    assert "player_1" in result
    assert "_hidden" in result


# ── resolve: empty / passthrough ─────────────────────────────────────────────

def test_resolve_empty_cache_noop():
    args = {"path": "$player", "component": "$hp"}
    result = resolve_aliases_in_args(args, {})
    assert result == args


def test_resolve_empty_args_noop():
    result = resolve_aliases_in_args({}, {"hp": "/P|C|f"})
    assert result == {}


def test_resolve_unknown_passthrough():
    cache = {"speed": "/P|C|s"}
    result = resolve_aliases_in_args({"path": "$hp"}, cache)
    assert result["path"] == "$hp"


def test_resolve_non_string_values_untouched():
    cache = {"hp": "/P|C|f"}
    result = resolve_aliases_in_args({"timeout": 30.0, "count": 5}, cache)
    assert result == {"timeout": 30.0, "count": 5}


# ── resolve: per-key segment extraction ──────────────────────────────────────

def test_resolve_path_segment0():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"path": "$hp"}, cache)["path"] == "/Player"


def test_resolve_component_segment1():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"component": "$hp"}, cache)["component"] == "HP"


def test_resolve_field_segment2():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"field": "$hp"}, cache)["field"] == "health"


def test_resolve_prop_segment2():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"prop": "$hp"}, cache)["prop"] == "health"


def test_resolve_query_full_pipe():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"query": "$hp"}, cache)["query"] == "/Player|HP|health"


def test_resolve_queries_single_full_pipe():
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"queries": "$hp"}, cache)["queries"] == "/Player|HP|health"


def test_resolve_three_segment_all_keys():
    """All four arg key variants work correctly with 3-segment alias."""
    cache = {"hp": "/Player|HP|health"}
    assert resolve_aliases_in_args({"path": "$hp"}, cache)["path"] == "/Player"
    assert resolve_aliases_in_args({"component": "$hp"}, cache)["component"] == "HP"
    assert resolve_aliases_in_args({"field": "$hp"}, cache)["field"] == "health"
    assert resolve_aliases_in_args({"prop": "$hp"}, cache)["prop"] == "health"


# ── resolve: whole-value rule ─────────────────────────────────────────────────

def test_resolve_not_whole_value_ignored():
    """Embedded $name is NOT a whole-value match."""
    cache = {"hp": "/P|C|f"}
    result = resolve_aliases_in_args({"path": "/prefix/$hp/suffix"}, cache)
    assert result["path"] == "/prefix/$hp/suffix"


def test_resolve_partial_dollar_not_matched():
    """'$hp_extended' is not in cache → pass through."""
    cache = {"hp": "/P|C|f"}
    result = resolve_aliases_in_args({"path": "$hp_extended"}, cache)
    assert result["path"] == "$hp_extended"


def test_resolve_path_no_pipe_single_segment():
    """Single-segment alias (no pipes) → path gets full value."""
    cache = {"player": "/Player/Character"}
    result = resolve_aliases_in_args({"path": "$player"}, cache)
    assert result["path"] == "/Player/Character"


# ── resolve: comma-separated ──────────────────────────────────────────────────

def test_resolve_inspect_comma_separated():
    """inspect(paths='$c1,$c2,$c3') — all three resolved."""
    result = resolve_aliases_in_args({"paths": "$c1,$c2,$c3"}, GRIDTEST_CACHE)
    assert result["paths"] == "/Collectible_1,/Collectible_2,/Collectible_3"


def test_resolve_partial_alias_in_comma_list():
    """Non-alias token in comma list passes through unchanged."""
    result = resolve_aliases_in_args({"paths": "$player,/Plane"}, GRIDTEST_CACHE)
    assert result["paths"] == "/GridPlayer,/Plane"


def test_resolve_checks_before_comma_separated():
    """checks_before='$c1,$c2' — both aliases resolved independently."""
    cache = {"c1": "/A", "c2": "/B"}
    result = resolve_aliases_in_args({"checks_before": "$c1,$c2"}, cache)
    assert result["checks_before"] == "/A,/B"


def test_resolve_checks_after_comma_separated():
    """checks_after='$c1,$c2,$c3' — all three resolved."""
    result = resolve_aliases_in_args({"checks_after": "$c1,$c2,$c3"}, GRIDTEST_CACHE)
    assert result["checks_after"] == "/Collectible_1,/Collectible_2,/Collectible_3"


def test_resolve_queries_pipe_value_not_whole():
    """'$player|GridPlayer|PosX' is NOT a whole-value match → unchanged."""
    result = resolve_aliases_in_args({"queries": "$player|GridPlayer|PosX"}, GRIDTEST_CACHE)
    assert "$player" in result["queries"]
    assert result["queries"] == "$player|GridPlayer|PosX"


# ── resolve: GridTest scenarios ────────────────────────────────────────────────

def test_resolve_gridtest_player():
    result = resolve_aliases_in_args({"path": "$player", "component": "GridPlayer"}, GRIDTEST_CACHE)
    assert result["path"] == "/GridPlayer"
    assert result["component"] == "GridPlayer"  # literal, not an alias


def test_resolve_gridtest_light_space_in_path():
    """Alias with space in resolved path must survive round-trip."""
    result = resolve_aliases_in_args({"path": "$light", "component": "Light"}, GRIDTEST_CACHE)
    assert result["path"] == "/Directional Light"


def test_resolve_non_alias_path_unchanged():
    """Literal path with no '$' is never touched."""
    result = resolve_aliases_in_args({"path": "/GridPlayer", "component": "GridPlayer"}, GRIDTEST_CACHE)
    assert result["path"] == "/GridPlayer"


def test_resolve_deleted_object_alias_passthrough():
    """Stale alias still resolves (Unity will report 'not found' — graceful)."""
    cache = {"c1": "/Collectible_1"}
    result = resolve_aliases_in_args({"path": "$c1"}, cache)
    assert result["path"] == "/Collectible_1"


def test_resolve_equals_in_alias_value():
    """Alias value parsed from ALIASES block may contain '='."""
    cache = {"obj": "path=weird/name|Comp|"}
    result = resolve_aliases_in_args({"path": "$obj"}, cache)
    assert result["path"] == "path=weird/name"


def test_resolve_list_arg_not_resolved():
    """List values are not resolved — only str whole-value."""
    cache = {"hp": "/Player|HP|health"}
    args = {"paths": ["$hp", "$player"], "timeout": 5.0}
    result = resolve_aliases_in_args(args, cache)
    assert result["paths"] == ["$hp", "$player"]
    assert result["timeout"] == 5.0


def test_resolve_large_cache_o1_performance():
    """100-alias cache resolve must be fast (dict lookup O(1))."""
    import time
    cache = {f"alias{i}": f"/Path{i}|Comp{i}|field{i}" for i in range(100)}
    args = {"path": "$alias99"}
    start = time.perf_counter()
    for _ in range(10_000):
        resolve_aliases_in_args(args, cache)
    elapsed = time.perf_counter() - start
    assert elapsed < 1.0, f"100-alias resolve too slow: {elapsed:.3f}s for 10k calls"
    result = resolve_aliases_in_args(args, cache)
    assert result["path"] == "/Path99"


# ── Wave 1: Unicode sigil must not resolve ────────────────────────────────────

def test_sigil_unicode_no_match():
    """Unicode sigil must not resolve — asymmetry with C# SigilRegex."""
    cache = {"h_игрок": "/Player|HP|health"}
    result = resolve_aliases_in_args({"path": "$h_игрок"}, cache)
    assert result["path"] == "$h_игрок"  # pass-through unchanged


# ── Wave 3.2: strip_alias_block ───────────────────────────────────────────────

from unity_mcp.middleware_alias import strip_alias_block


def test_strip_alias_block_removes_block():
    text = "--- ALIASES ---\nhp=/Player|HP|health\n---\n[Scene]\n├── Player"
    result = strip_alias_block(text)
    assert "--- ALIASES ---" not in result
    assert "[Scene]" in result


def test_strip_alias_block_no_block():
    text = "[Scene]\n├── Player"
    assert strip_alias_block(text) == text


def test_strip_alias_block_at_start_no_leading_newline():
    """Block at position 0 must not leave a leading newline in result."""
    text = "--- ALIASES ---\nhp=/Player|HP|health\n---\n[Scene]\n├── Player"
    result = strip_alias_block(text)
    assert not result.startswith("\n"), f"Leading newline: {result!r}"
    assert result.startswith("[Scene]")


def test_strip_alias_block_malformed_no_crash():
    """No footer — malformed block must not raise, returns unchanged."""
    text = "--- ALIASES ---\nhp=/Player|HP|health\n[Scene]"
    result = strip_alias_block(text)
    assert isinstance(result, str)
    # footer not found → text returned as-is (no crash)
    assert "--- ALIASES ---" in result


# ── Wave 5.2: compound path NOT resolved (whole-value only) ──────────────────

def test_resolve_compound_path_not_resolved():
    """$name/child is NOT supported — whole-value only."""
    cache = {"player": "/GridPlayer"}
    result = resolve_aliases_in_args({"path": "$player/child"}, cache)
    assert result["path"] == "$player/child"


# ── Wave 6.2: C#↔Python cross-boundary format tests ──────────────────────────

# Exact format that C# HierarchySerializer emits (confirmed from GetAliasesTests.cs)
CSHARP_ALIAS_BLOCK = (
    "--- ALIASES ---\n"
    "player=/GridPlayer\n"
    "c1=/Collectible_1\n"
    "light=/Directional Light\n"
    "---\n"
    "[Scene]\n"
    "├── GridPlayer\n"
    "├── Collectible_1\n"
    "└── Directional Light\n"
)


def test_cross_boundary_csharp_format_parsed():
    """Exact C# HierarchySerializer output parses correctly in Python."""
    result = parse_aliases_from_hierarchy(CSHARP_ALIAS_BLOCK)
    assert result == {
        "player": "/GridPlayer",
        "c1": "/Collectible_1",
        "light": "/Directional Light",
    }


def test_cross_boundary_sigil_strip():
    """C# may emit '$player=' — Python must strip the '$'."""
    text = "--- ALIASES ---\n$player=/GridPlayer\n---\n[Scene]"
    result = parse_aliases_from_hierarchy(text)
    assert "player" in result
    assert "$player" not in result
    assert result["player"] == "/GridPlayer"


def test_cross_boundary_resolve_full_pipeline():
    """End-to-end: parse C# block → populate cache → resolve in args."""
    cache = parse_aliases_from_hierarchy(CSHARP_ALIAS_BLOCK)
    args = resolve_aliases_in_args({"path": "$player", "component": "GridPlayer"}, cache)
    assert args["path"] == "/GridPlayer"


def test_cross_boundary_space_in_path_preserved():
    """'/Directional Light' has a space — must survive parse → resolve."""
    cache = parse_aliases_from_hierarchy(CSHARP_ALIAS_BLOCK)
    args = resolve_aliases_in_args({"path": "$light"}, cache)
    assert args["path"] == "/Directional Light"


# ── Typed alias: ValConst cross-boundary ──────────────────────────────────────

def test_resolve_valconst_path_key_full_value():
    """ValConst alias: no pipes → 'path' key gets the full const value."""
    cache = {"speed": "5.5"}
    result = resolve_aliases_in_args({"path": "$speed"}, cache)
    assert result["path"] == "5.5"


def test_resolve_valconst_query_key_full_value():
    """ValConst alias: 'query' key also gets the full const value (no segments)."""
    cache = {"difficulty": "hard"}
    result = resolve_aliases_in_args({"query": "$difficulty"}, cache)
    assert result["query"] == "hard"


def test_cross_boundary_valconst_format_parsed():
    """C# BuildAliasSection emits 'speed=5.5' for ValConst — must parse and resolve."""
    text = "--- ALIASES ---\nspeed=5.5\nhp=/Player|HP|health\n---\n[Scene]"
    cache = parse_aliases_from_hierarchy(text)
    assert cache["speed"] == "5.5"
    assert cache["hp"] == "/Player|HP|health"
    args = resolve_aliases_in_args({"path": "$speed"}, cache)
    assert args["path"] == "5.5"
