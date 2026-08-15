"""G2 Session 2: UGUI + UITOOLKIT category gating tests."""


def test_no_ui_tool_is_tier1():
    """All 6 UI tools (UGUI + UITOOLKIT) are absent from TIER1."""
    from unity_mcp.tools.gating import TIER1
    ui_tools = {"create_ui", "set_rect", "ui_intent", "lint_ugui", "inspect_uitk", "lint_uitk"}
    in_tier1 = ui_tools & TIER1
    assert not in_tier1, f"UI tools must not be TIER1: {sorted(in_tier1)}"


def test_discover_ugui_excludes_uitk():
    """'ugui' alias does not include UITOOLKIT tools."""
    from unity_mcp.tools.gating import CATEGORIES
    ugui_tools = CATEGORIES.get("ugui", set())
    uitk_tools = {"inspect_uitk", "lint_uitk"}
    overlap = ugui_tools & uitk_tools
    assert not overlap, f"UGUI must not contain UITOOLKIT tools: {overlap}"


def test_discover_uitoolkit_excludes_ugui():
    """'uitoolkit' alias does not include UGUI tools."""
    from unity_mcp.tools.gating import CATEGORIES
    uitk_tools = CATEGORIES.get("uitoolkit", set())
    ugui_only = {"create_ui", "set_rect", "ui_intent", "lint_ugui"}
    overlap = uitk_tools & ugui_only
    assert not overlap, f"UITOOLKIT must not contain UGUI tools: {overlap}"


def test_discover_ui_returns_both():
    """'ui' alias contains both UGUI and UITOOLKIT tools."""
    from unity_mcp.tools.gating import CATEGORIES
    ui_tools = CATEGORIES.get("ui", set())
    expected_ugui = {"create_ui", "set_rect", "ui_intent", "lint_ugui"}
    expected_uitk = {"inspect_uitk", "lint_uitk"}
    missing_ugui = expected_ugui - ui_tools
    missing_uitk = expected_uitk - ui_tools
    assert not missing_ugui, f"'ui' alias missing UGUI tools: {missing_ugui}"
    assert not missing_uitk, f"'ui' alias missing UITOOLKIT tools: {missing_uitk}"


def test_discover_single_category_token_budget():
    """Single category has <= 50 tools (token budget guard)."""
    from unity_mcp.tools.gating import CATEGORIES
    for cat in ("ugui", "uitoolkit"):
        tools = CATEGORIES.get(cat, set())
        assert len(tools) <= 50, f"Category {cat} has {len(tools)} tools — token budget risk"
