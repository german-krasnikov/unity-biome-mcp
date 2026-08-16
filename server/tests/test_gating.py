"""Tests for capability gating (Part A)."""
import pytest
from unittest.mock import MagicMock, AsyncMock


# --- helpers ---

def _make_tool(name: str):
    t = MagicMock()
    t.name = name
    return t


# --- gating module tests ---

def test_tier1_visible():
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("get_hierarchy"), _make_tool("batch")]
    assert filter_by_tier(tools) == tools


def test_tier2_hidden():
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("animation"), _make_tool("shader")]
    result = filter_by_tier(tools)
    assert result == []


def test_enable_category_returns_tool_names():
    from unity_mcp.tools.gating import enable_category, reset
    reset()
    names = enable_category("animation")
    assert "animation" in names
    assert "timeline" in names


def test_enable_category_makes_tools_visible():
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("animation")
    tools = [_make_tool("animation"), _make_tool("shader")]
    result = filter_by_tier(tools)
    names = [t.name for t in result]
    assert "animation" in names
    assert "shader" not in names


def test_unknown_category_error():
    from unity_mcp.tools.gating import enable_category, reset
    reset()
    with pytest.raises(ValueError, match="Unknown category"):
        enable_category("nonexistent")


def test_filter_preserves_unknown_tools():
    """Plugin tools not in any tier list pass through (opt-in)."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("custom_plugin_a"), _make_tool("custom_plugin_b")]
    result = filter_by_tier(tools)
    assert len(result) == 2


def test_reset_clears_session():
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("animation")
    reset()
    tools = [_make_tool("animation")]
    assert filter_by_tier(tools) == []


def test_is_visible_after_enable():
    from unity_mcp.tools.gating import enable_category, is_visible, reset
    reset()
    assert not is_visible("animation")
    enable_category("animation")
    assert is_visible("animation")


def test_is_visible_tier1():
    from unity_mcp.tools.gating import is_visible, reset
    reset()
    assert is_visible("get_hierarchy")
    assert is_visible("batch")


def test_get_categories():
    from unity_mcp.tools.gating import get_categories
    cats = get_categories()
    assert "animation" in cats
    assert "runtime" in cats
    assert isinstance(cats["animation"], (set, frozenset))


async def test_discover_tools_invalid_category_lists_valid():
    """G31: invalid category error must list what categories ARE valid."""
    from unity_mcp.tools.gating import discover_tools, reset
    import pytest
    reset()
    with pytest.raises(ValueError) as exc_info:
        await discover_tools(category="nonexistent_xyz")
    msg = str(exc_info.value)
    assert "Valid" in msg, f"error should list valid categories, got: {msg}"
    assert "SCENE" in msg or "RUNTIME" in msg, f"expected known categories in: {msg}"


async def test_discover_tools_lists_categories():
    from unity_mcp.tools.gating import discover_tools, reset
    reset()
    # Default: only canonical 8 keys
    result = await discover_tools(enable=False)
    assert "RUNTIME" in result
    assert "SCENE" in result
    # legacy aliases not shown by default
    assert "animation:" not in result
    assert "runtime:" not in result


async def test_discover_tools_include_legacy():
    from unity_mcp.tools.gating import discover_tools, reset
    reset()
    result = await discover_tools(enable=False, include_legacy=True)
    assert "animation" in result
    assert "runtime" in result


async def test_discover_tools_enables():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()
    await discover_tools(category="animation")
    assert is_visible("animation")


async def test_discover_tools_browse_only():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()
    result = await discover_tools(category="animation", enable=False)
    assert not is_visible("animation")
    assert "animation" in result


async def test_discover_tools_category_structured_preserves_enable_behavior():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()

    result = await discover_tools(category="SYSTEM", structured=True)

    assert result.startswith("Category 'SYSTEM':\n")
    assert "  build" in result
    assert "surfaces=direct" in next(
        line for line in result.splitlines() if line.strip().startswith("build")
    )
    assert "mutability=write" in next(
        line for line in result.splitlines() if line.strip().startswith("build")
    )
    assert is_visible("build")


async def test_discover_tools_category_structured_browse_only_does_not_enable():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()

    result = await discover_tools(
        category="SYSTEM", structured=True, enable=False
    )

    assert "surfaces=direct" in result
    assert not is_visible("build")


# --- TDD: runtime tools in TIER1 ---

def test_runtime_tools_in_tier1():
    """Phase 1b: runtime-only tools demoted from TIER1; only run_playtest stays."""
    from unity_mcp.tools.gating import TIER1
    assert "run_playtest" in TIER1
    for name in ("invoke_method", "query_state", "wait_until", "move_to",
                 "set_runtime_property", "test_step"):
        assert name not in TIER1, f"{name} should be demoted from TIER1"


def test_batch_allows_invoke_method():
    """invoke_method is sync — BatchHelper delegates to CommandRegistry.IsBatchable()."""
    from pathlib import Path
    path = str(Path(__file__).parents[2] / "unity-plugin" / "Editor" / "BatchHelper.cs")
    src = open(path, encoding="utf-8").read()
    assert "IsBatchable" in src, "BatchHelper must use CommandRegistry.IsBatchable()"
    assert 'cmd == "invoke_method"' not in src, "invoke_method must not be hardcoded in blocklist"


# --- TDD Phase 2: register_tools() self-registration ---

def test_register_tools_adds_to_category():
    """register_tools() adds tools to a named category."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat", {"tool_x", "tool_y"})
    try:
        assert "tool_x" in gating.CATEGORIES["test_cat"]
        assert "tool_y" in gating.CATEGORIES["test_cat"]
    finally:
        del gating.CATEGORIES["test_cat"]
        gating._ALL_KNOWN.discard("tool_x")
        gating._ALL_KNOWN.discard("tool_y")


def test_register_tools_no_tier1_promotion():
    """Plugins do not control their own visibility — the platform does.
    register_tools() must NOT accept a tier1 param at all."""
    import inspect
    from unity_mcp.tools.gating import register_tools
    sig = inspect.signature(register_tools)
    assert "tier1" not in sig.parameters, "tier1 param must be removed from plugin API"


def test_plugin_tools_default_tier2():
    """Plugin-registered tools must NOT appear in TIER1 — category-only, discoverable."""
    from unity_mcp.tools import gating
    gating.register_tools("test_plugin", {"plugin_tool_x", "plugin_tool_y"})
    try:
        assert "plugin_tool_x" not in gating.TIER1
        assert "plugin_tool_y" not in gating.TIER1
    finally:
        del gating.CATEGORIES["test_plugin"]
        gating._ALL_KNOWN.discard("plugin_tool_x")
        gating._ALL_KNOWN.discard("plugin_tool_y")


def test_register_tools_idempotent():
    """Calling register_tools twice does not duplicate entries."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat3", {"tool_z"})
    try:
        size_before = len(gating.CATEGORIES["test_cat3"])
        gating.register_tools("test_cat3", {"tool_z"})
        assert len(gating.CATEGORIES["test_cat3"]) == size_before  # set.update is idempotent
    finally:
        del gating.CATEGORIES["test_cat3"]
        gating._ALL_KNOWN.discard("tool_z")


def test_register_tools_plugins_category_updates_themed_categories():
    """plugins alias resolves to SYSTEM via _CATEGORY_ALIAS (Phase 2: PLUGINS folded into SYSTEM)."""
    from unity_mcp.tools import gating
    gating.register_tools("plugins", {"my_plugin_tool"})
    try:
        assert "my_plugin_tool" in gating._THEMED_CATEGORIES["SYSTEM"]
        assert "my_plugin_tool" in gating.get_catalog()["categories"]["SYSTEM"]
    finally:
        gating._THEMED_CATEGORIES["SYSTEM"].remove("my_plugin_tool")
        gating.CATEGORIES = gating._rebuild_categories()
        gating._ALL_KNOWN.discard("my_plugin_tool")


def test_register_tools_unknown_category_does_not_touch_themed_categories():
    """register_tools() for a category with no _THEMED_CATEGORIES counterpart (e.g. a
    plugin-defined custom category) must not create a spurious themed entry."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat_no_theme", {"tool_w"})
    try:
        assert "TEST_CAT_NO_THEME" not in gating._THEMED_CATEGORIES
    finally:
        del gating.CATEGORIES["test_cat_no_theme"]
        gating._ALL_KNOWN.discard("tool_w")


# --- Integration tests: plugin self-registration composability ---

# --- C6: register_tools() dual-write → CATEGORIES becomes a derived view ---

def test_register_tools_updates_categories_and_themed_categories_consistently():
    """register_tools('debug', ...) keeps CATEGORIES and _THEMED_CATEGORIES in agreement.
    Phase 2: 'debug' alias resolves to RUNTIME via _CATEGORY_ALIAS."""
    from unity_mcp.tools import gating
    gating.register_tools("debug", {"my_plugin_tool"})
    try:
        assert "my_plugin_tool" in gating.CATEGORIES["debug"]
        assert "my_plugin_tool" in gating._THEMED_CATEGORIES["RUNTIME"]
    finally:
        gating._THEMED_CATEGORIES["RUNTIME"].remove("my_plugin_tool")
        gating.CATEGORIES = gating._rebuild_categories()
        gating._ALL_KNOWN.discard("my_plugin_tool")


def test_register_tools_themed_rebuild_preserves_other_plugins_custom_categories():
    """Real-world scenario: plugin A registers into a custom category (documented
    public API, docs/plugins/quickstart.md), then plugin B registers into a themed
    category ('debug'). The CATEGORIES rebuild triggered by plugin B's themed
    registration must NOT wipe plugin A's unrelated custom category."""
    from unity_mcp.tools import gating
    gating.register_tools("my_custom_plugin", {"plugin_a_tool"})
    try:
        gating.register_tools("debug", {"plugin_b_tool"})
        try:
            assert "plugin_a_tool" in gating.CATEGORIES["my_custom_plugin"], (
                "themed-category rebuild wiped an unrelated custom plugin category"
            )
        finally:
            gating._THEMED_CATEGORIES["RUNTIME"].remove("plugin_b_tool")
            gating.CATEGORIES = gating._rebuild_categories()
            gating._ALL_KNOWN.discard("plugin_b_tool")
    finally:
        gating.CATEGORIES.pop("my_custom_plugin", None)
        gating._ALL_KNOWN.discard("plugin_a_tool")


def test_register_tools_unknown_category_falls_back_to_direct_categories_write():
    """register_tools() for a category with no _THEMED_CATEGORIES counterpart must
    still land in CATEGORIES (fallback branch) without creating a spurious themed entry."""
    from unity_mcp.tools import gating
    gating.register_tools("totally_new_category", {"x"})
    try:
        assert "x" in gating.CATEGORIES["totally_new_category"]
        assert "TOTALLY_NEW_CATEGORY" not in gating._THEMED_CATEGORIES
    finally:
        del gating.CATEGORIES["totally_new_category"]
        gating._ALL_KNOWN.discard("x")


def test_register_tools_makes_unknown_tool_gated():
    """unknown tool passes filter; after register_tools without tier1 it becomes known+gated."""
    from unity_mcp.tools import gating
    tool = _make_tool("new_shiny_tool")
    # Before registration: unknown → passes through
    assert gating.filter_by_tier([tool]) == [tool]
    gating.register_tools("test_new_cat", {"new_shiny_tool"})
    try:
        # Now known, not in tier1, not session-enabled → filtered out
        assert gating.filter_by_tier([tool]) == []
    finally:
        gating._ALL_KNOWN.discard("new_shiny_tool")
        del gating.CATEGORIES["test_new_cat"]


# --- TDD: audit fixes ---

def test_set_parent_in_tier1():
    """set_parent is a core mutation (like delete_object) and must be in TIER1."""
    from unity_mcp.tools.gating import TIER1
    assert "set_parent" in TIER1


def test_unwire_event_in_object_category():
    """unwire_event pairs with wire_event and must be in the object category."""
    from unity_mcp.tools.gating import CATEGORIES
    assert "unwire_event" in CATEGORIES["object"]


def test_set_parent_visible_without_enable():
    """set_parent must be visible by default (TIER1), no category unlock needed."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("set_parent")]
    assert filter_by_tier(tools) == tools


def test_unwire_event_visible_after_object_enable():
    """unwire_event becomes visible after enable_category('object')."""
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("object")
    tools = [_make_tool("unwire_event")]
    assert filter_by_tier(tools) == tools


def test_unwire_event_hidden_without_enable():
    """unwire_event is gated (not in TIER1), hidden by default."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("unwire_event")]
    assert filter_by_tier(tools) == []


# --- TDD F4: is_deferred ---

def test_is_deferred_returns_true_for_non_core_known_tool():
    """A tool in CATEGORIES but not in _CORE_TOOLS is deferred."""
    from unity_mcp.tools.gating import is_deferred
    # 'animation' is in CATEGORIES["animation"] but not in _CORE_TOOLS
    assert is_deferred("animation") is True


def test_is_deferred_returns_false_for_core_tool():
    """A CORE tool is not deferred."""
    from unity_mcp.tools.gating import is_deferred
    assert is_deferred("get_hierarchy") is False
    assert is_deferred("batch") is False


def test_is_deferred_returns_false_for_unknown_plugin_tool():
    """Unknown tools (not in _ALL_KNOWN) pass through — not deferred."""
    from unity_mcp.tools.gating import is_deferred
    assert is_deferred("my_totally_unknown_plugin_tool_xyz") is False


# --- P1-2: connection tools survive filter_by_tier ---

def test_reconnect_unity_in_tier1_not_core():
    """reconnect_unity demoted from CORE to SYSTEM tier1 (Phase 2)."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "reconnect_unity" not in _CORE_TOOLS
    assert "reconnect_unity" in TIER1


def test_list_connections_not_core_not_tier1():
    """list_connections demoted from CORE (Phase 2) then from TIER1 (Phase 1b)."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "list_connections" not in _CORE_TOOLS
    assert "list_connections" not in TIER1


def test_reconnect_unity_survives_filter_when_disabled_cache_cold():
    """reconnect_unity is tier1 — visible even with a cold session-enable cache."""
    from unity_mcp.tools import gating
    gating.reset()
    tool = _make_tool("reconnect_unity")
    result = gating.filter_by_tier([tool])
    assert result == [tool], "reconnect_unity must survive filter_by_tier with cold session"


def test_list_connections_hidden_without_enable():
    """list_connections demoted to Tier2 (Phase 1b) — hidden by default, requires category enable."""
    from unity_mcp.tools import gating
    gating.reset()
    tool = _make_tool("list_connections")
    result = gating.filter_by_tier([tool])
    assert result == [], "list_connections must be gated after Phase 1b demotion"


# --- TDD audit PY3.test.1/2 + PY2.arch.2: themed tools hidden by default ---

def test_themed_tools_hidden_by_default():
    """get_test_results, object_diff, set_llm_config, transfer_object are in _THEMED_CATEGORIES
    but must be in _ALL_KNOWN so filter_by_tier gates them (not passes as unknown plugins)."""
    from unity_mcp.tools import gating
    gating.reset()
    for name in ["object_diff", "set_llm_config", "transfer_object"]:
        tool = _make_tool(name)
        result = gating.filter_by_tier([tool])
        assert result == [], f"{name} must be gated (hidden) by default, not pass as unknown plugin"


def test_orphaned_tools_are_in_ALL_KNOWN():
    """Themed tools must be in _ALL_KNOWN so is_deferred() and filter_by_tier work correctly."""
    from unity_mcp.tools.gating import _ALL_KNOWN
    for name in ["get_test_results", "object_diff", "set_llm_config", "transfer_object"]:
        assert name in _ALL_KNOWN, f"{name} must be in _ALL_KNOWN"


# --- TDD audit PY3.test.3: resolve_tool_schema ---

def test_resolve_tool_schema_in_ALL_KNOWN():
    """resolve_tool_schema is core; must also be in _ALL_KNOWN so filter_by_tier
    exercises the is_visible/_CORE_TOOLS path, not unknown-plugin passthrough."""
    from unity_mcp.tools.gating import _ALL_KNOWN
    assert "resolve_tool_schema" in _ALL_KNOWN


def test_resolve_tool_schema_survives_filter():
    """resolve_tool_schema passes filter_by_tier via TIER1/_CORE_TOOLS, not unknown passthrough."""
    from unity_mcp.tools import gating
    gating.reset()
    tool = _make_tool("resolve_tool_schema")
    assert gating.filter_by_tier([tool]) == [tool]


# --- TDD audit X5.cross.2: disabled_set overrides session enable ---

def test_filter_tools_disabled_set_overrides_session_enable():
    """enable_category('animation') + disabled={'animation'} → animation tool is hidden."""
    from unity_mcp.tools import gating
    from unity_mcp.server_filtering import filter_tools
    gating.reset()
    gating.enable_category("animation")
    tool = _make_tool("animation")
    result = filter_tools([tool], {"animation"})
    assert result == [], "disabled set must suppress session-enabled tools"


# --- TDD FIX-33: single-source taxonomy ---

def test_categories_derived_from_themed():
    """Every tool in built-in CATEGORIES aliases must exist in _THEMED_CATEGORIES or _CORE_TOOLS.
    Skips dynamically-registered plugin categories (not in _CATEGORY_ALIAS)."""
    from unity_mcp.tools.gating import CATEGORIES, _THEMED_CATEGORIES, _CORE_TOOLS, _CATEGORY_ALIAS
    themed_all = {t for tools in _THEMED_CATEGORIES.values() for t in tools}
    for cat in _CATEGORY_ALIAS:  # only built-in aliases, skip plugin categories
        for tool in CATEGORIES.get(cat, set()):
            assert tool in themed_all or tool in _CORE_TOOLS, (
                f"CATEGORIES['{cat}'] has '{tool}' not in _THEMED_CATEGORIES or _CORE_TOOLS"
            )


def test_no_orphan_themed_tools():
    """Every non-CORE tool in _THEMED_CATEGORIES must appear in at least one CATEGORIES alias."""
    from unity_mcp.tools.gating import CATEGORIES, _THEMED_CATEGORIES, _CORE_TOOLS, TIER1
    cats_all = {t for tools in CATEGORIES.values() for t in tools}
    themed_all = {t for tools in _THEMED_CATEGORIES.values() for t in tools}
    # Tools that are in TIER1 don't need to be in CATEGORIES (they're always visible)
    # But every non-TIER1 themed tool should be reachable via some category alias
    for tool in themed_all:
        if tool not in TIER1 and tool not in _CORE_TOOLS:
            assert tool in cats_all, (
                f"'{tool}' is in _THEMED_CATEGORIES but unreachable via any CATEGORIES alias"
            )


def test_old_category_aliases_work():
    """All 8 legacy category names must still work with discover_tools/enable_category."""
    from unity_mcp.tools.gating import CATEGORIES
    expected_aliases = {"object", "animation", "asset", "advanced", "ui", "runtime", "connection", "session"}
    assert expected_aliases.issubset(set(CATEGORIES.keys())), (
        f"Missing aliases: {expected_aliases - set(CATEGORIES.keys())}"
    )


def test_category_alias_mapping_is_exhaustive():
    """Every non-empty themed group must be reachable via CATEGORIES (alias or direct key).
    Phase 2: the 8 new themed keys are included in CATEGORIES directly by _rebuild_categories(),
    so they need not appear in _CATEGORY_ALIAS values."""
    from unity_mcp.tools.gating import _CATEGORY_ALIAS, _THEMED_CATEGORIES, CATEGORIES
    mapped_via_alias = set()
    for groups in _CATEGORY_ALIAS.values():
        mapped_via_alias.update(groups)
    non_empty_themed = {k for k, v in _THEMED_CATEGORIES.items() if v}
    # Reachable = covered by an alias OR exposed directly as a CATEGORIES key
    unreachable = non_empty_themed - mapped_via_alias - set(CATEGORIES.keys())
    assert not unreachable, (
        f"Themed groups not reachable via any CATEGORIES key: {unreachable}"
    )


# --- DRY audit issues-23-29 Cat.2: TIER1 derived from _CORE_TOOLS, not re-typed ---

def test_tier1_is_superset_of_core_tools():
    """TIER1 must contain every _CORE_TOOLS entry — derived via union, not a hand-typed
    fresh literal that can silently drift from _CORE_TOOLS on a rename."""
    from unity_mcp.tools.gating import TIER1, _CORE_TOOLS
    missing = _CORE_TOOLS - TIER1
    assert not missing, f"_CORE_TOOLS entries missing from TIER1: {sorted(missing)}"


def test_tier1_residual_names_still_present():
    """Regression: tier1-only names (not in _CORE_TOOLS) must survive refactors.
    Phase 1a: delete_object/set_parent/scene/search_scene demoted from CORE, now tier1-only.
    Phase 1b: runtime tools (invoke_method etc.) demoted from TIER1 entirely;
    set_active/validate_references/undo_last promoted to tier1.
    Phase sprint1-2: execute_code promoted from tier1 → core (#04)."""
    from unity_mcp.tools.gating import TIER1, _CORE_TOOLS
    residual_expected = {
        "screenshot", "run_tests", "await_compile", "sync_unity", "run_playtest",
        # Phase 1a demotions from CORE → now tier1-only
        "delete_object", "set_parent", "scene", "search_scene",
        # Phase 1b promotions
        "set_active", "validate_references",
        # P-12440 Phase 1: demoted from CORE to tier1
        "apply_scene_change", "scene_change_plan", "verify_after_change",
    }
    missing = residual_expected - TIER1
    assert not missing, f"TIER1-only names dropped by refactor: {sorted(missing)}"
    # Sanity: none of the tier1-only names accidentally ended up back in _CORE_TOOLS
    assert not (residual_expected & _CORE_TOOLS)


# --- TDD Fix 1: TIER1 pollution — vfx_intent/animator_intent/ui_intent must be Tier2+ ---

def test_tier1_excludes_intent_tools():
    """vfx_intent, animator_intent, ui_intent must NOT be in TIER1 — they are themed
    (VFX/UI/META) tools, not always-on core."""
    from unity_mcp.tools.gating import TIER1
    for name in ("vfx_intent", "animator_intent", "ui_intent"):
        assert name not in TIER1, f"{name} should be Tier2+, not TIER1"


def test_vfx_intent_hidden_by_default():
    from unity_mcp.tools import gating
    gating.reset()
    assert gating.filter_by_tier([_make_tool("vfx_intent")]) == []


def test_animator_intent_hidden_by_default():
    from unity_mcp.tools import gating
    gating.reset()
    assert gating.filter_by_tier([_make_tool("animator_intent")]) == []


def test_ui_intent_hidden_by_default():
    from unity_mcp.tools import gating
    gating.reset()
    assert gating.filter_by_tier([_make_tool("ui_intent")]) == []


async def test_lint_ugui_visible_after_discover_ui_category():
    # "ui" now maps to UGUI + UITOOLKIT; vfx_intent is MEDIA (not in "ui")
    from unity_mcp.tools import gating
    gating.reset()
    await gating.discover_tools(category="ui")
    try:
        assert gating.is_visible("lint_ugui")
        assert gating.is_visible("inspect_uitk")
        assert not gating.is_visible("vfx_intent")
    finally:
        gating.reset()


async def test_animator_intent_visible_after_discover_advanced_category():
    from unity_mcp.tools import gating
    gating.reset()
    await gating.discover_tools(category="advanced")
    try:
        assert gating.is_visible("animator_intent")
    finally:
        gating.reset()


async def test_ui_intent_visible_after_discover_ui_category():
    from unity_mcp.tools import gating
    gating.reset()
    await gating.discover_tools(category="ui")
    try:
        assert gating.is_visible("ui_intent")
    finally:
        gating.reset()


# --- TDD Fix 2: budget_status orphan ---

def test_budget_status_in_all_known():
    """budget_status must be in _ALL_KNOWN (not an orphan)."""
    from unity_mcp.tools.gating import _ALL_KNOWN
    assert "budget_status" in _ALL_KNOWN


def test_budget_status_hidden_by_default():
    from unity_mcp.tools import gating
    gating.reset()
    assert gating.filter_by_tier([_make_tool("budget_status")]) == []


async def test_budget_status_visible_after_discover_advanced():
    from unity_mcp.tools import gating
    gating.reset()
    await gating.discover_tools(category="advanced")
    try:
        assert gating.is_visible("budget_status")
    finally:
        gating.reset()


# --- B4b: set_properties demoted out of TIER1 (superseded by configure_objects) ---

def test_set_properties_not_in_tier1():
    """set_properties duplicates configure_objects' capability — demote to Tier2
    to cut always-on token cost. Discoverable via META category."""
    from unity_mcp.tools import gating
    assert "set_properties" not in gating.TIER1


# --- m1: drift-invariant — every registered tool must be known to gating ---

def test_all_registered_tools_are_known_to_gating():
    """Drift invariant: every tool FastMCP knows about must be classified
    somewhere in gating.py (TIER1, a category, or _CORE_TOOLS). A tool that
    slips through is invisible to discover_tools()/enable_category() and can
    never be surfaced to a filtered client."""
    from unity_mcp.tools import gating
    from unity_mcp.server import mcp
    registered = {t.name for t in mcp._tool_manager.list_tools()}
    missing = registered - gating._ALL_KNOWN
    assert not missing, f"Tools registered but unknown to gating: {sorted(missing)}"


# --- Phase 2: new taxonomy tests ---

def test_no_orphan():
    from unity_mcp.tools.gating import _THEMED_CATEGORIES, _CORE_TOOLS
    from unity_mcp.tools.tool_specs import _SPECS
    themed = set()
    for tools in _THEMED_CATEGORIES.values():
        themed.update(tools)
    internal = {n for n, s in _SPECS.items() if s.category == "_INTERNAL"}
    deprecated = {n for n, s in _SPECS.items() if s.category == "DEPRECATED"}
    assert themed | _CORE_TOOLS | internal | deprecated == set(_SPECS.keys())


def test_each_tool_exactly_one_themed_category():
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    seen = set()
    for tools in _THEMED_CATEGORIES.values():
        for t in tools:
            assert t not in seen, f"duplicate in _THEMED_CATEGORIES: {t}"
            seen.add(t)


def test_discover_old_aliases_still_work():
    from unity_mcp.tools.gating import CATEGORIES
    for alias in ["object", "animation", "asset", "advanced", "ui",
                  "runtime", "session", "debug", "profiling"]:
        assert alias in CATEGORIES


def test_discover_new_categories_work():
    from unity_mcp.tools.gating import CATEGORIES
    for cat in ["SCENE", "COMPONENTS", "ASSETS", "MEDIA",
                "VERIFY", "RUNTIME", "TESTS", "SYSTEM"]:
        assert cat in CATEGORIES, f"New category {cat!r} must be directly in CATEGORIES"


def test_catalog_has_10_themed_categories():
    # Session 2: UGUI + UITOOLKIT added alongside original 8
    from unity_mcp.tools.gating import get_catalog
    cats = get_catalog()["categories"]
    themed = {k for k in cats if k != "CORE"}
    assert themed == {
        "SCENE", "COMPONENTS", "ASSETS", "MEDIA", "VERIFY", "RUNTIME", "TESTS", "SYSTEM",
        "UGUI", "UITOOLKIT",
    }


def test_demoted_tools_are_tier1_not_core():
    """Phase 2: these tools moved from CORE to SYSTEM tier1. doctor/list_connections/get_enabled_tools
    further demoted to Tier2 in Phase 1b. ask/ask_user fully demoted in P-12440 Phase 1."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    demoted = {"discover_tools", "permission_prompt", "reconnect_unity", "resolve_tool_schema"}
    assert not any(t in _CORE_TOOLS for t in demoted)
    assert all(t in TIER1 for t in demoted)


# --- #04: execute_code promoted to core ---

def test_execute_code_in_core():
    """#04: execute_code must be in _CORE_TOOLS so Codex sees it by default."""
    from unity_mcp.tools.gating import _CORE_TOOLS
    assert "execute_code" in _CORE_TOOLS


# --- #15: verify/scene tools promoted to core ---

def test_verify_after_change_not_in_core():
    """P-12440: verify_after_change demoted from CORE to TIER1-only."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "verify_after_change" not in _CORE_TOOLS
    assert "verify_after_change" in TIER1


def test_apply_scene_change_not_in_core():
    """P-12440: apply_scene_change demoted from CORE to TIER1-only."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "apply_scene_change" not in _CORE_TOOLS
    assert "apply_scene_change" in TIER1


def test_scene_change_plan_not_in_core():
    """P-12440: scene_change_plan demoted from CORE to TIER1-only."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "scene_change_plan" not in _CORE_TOOLS
    assert "scene_change_plan" in TIER1


def test_resolve_scene_refs_not_in_core_not_in_tier1():
    """P-12440: resolve_scene_refs fully demoted — not CORE, not TIER1."""
    from unity_mcp.tools.gating import _CORE_TOOLS, TIER1
    assert "resolve_scene_refs" not in _CORE_TOOLS
    assert "resolve_scene_refs" not in TIER1


# --- P-319: exact TIER1 count assertion ---

def test_tier1_tool_count():
    """P-319 regression: TIER1 count must match CHANGELOG documentation.
    If this fails, update CHANGELOG.md and this assertion together."""
    from unity_mcp.tools.gating import TIER1
    assert len(TIER1) == 37, (
        f"TIER1 count changed: {len(TIER1)}. Update CHANGELOG.md and this assertion."
    )
