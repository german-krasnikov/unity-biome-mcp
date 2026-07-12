"""Tests for tools/tool_specs.py — M8: single ToolSpec source of truth."""
import pytest


def test_tool_spec_is_frozen_dataclass_with_defaults():
    from unity_mcp.tools.tool_specs import ToolSpec
    spec = ToolSpec(category="SCENE_EDIT")
    assert spec.category == "SCENE_EDIT"
    assert spec.core is False
    assert spec.tier1 is False
    assert spec.timeout_s == 30.0
    with pytest.raises(Exception):  # frozen dataclass rejects mutation
        spec.category = "OTHER"


def test_specs_is_nonempty_dict_of_toolspec():
    from unity_mcp.tools.tool_specs import _SPECS, ToolSpec
    assert len(_SPECS) > 100
    assert all(isinstance(v, ToolSpec) for v in _SPECS.values())


def test_internal_commands_present_and_excluded_from_core_tier1():
    """ping/get_version/export_package/import_package are protocol commands, not MCP
    tools — category='_INTERNAL', never core/tier1 (would leak into gating collections)."""
    from unity_mcp.tools.tool_specs import _SPECS
    for name in ("ping", "get_version", "export_package", "import_package"):
        spec = _SPECS[name]
        assert spec.category == "_INTERNAL"
        assert spec.core is False
        assert spec.tier1 is False


def test_core_tools_have_core_true_and_core_category():
    from unity_mcp.tools.tool_specs import _SPECS
    # 11 CORE tools after Phase 1a (scene/search_scene/delete_object/set_parent moved to SCENE tier1)
    for name in ("get_hierarchy", "batch", "set_property", "do"):
        spec = _SPECS[name]
        assert spec.core is True
        assert spec.category == "CORE"


def test_tier1_residual_tool_has_tier1_true_and_themed_category():
    """A tool promoted to always-visible but not core keeps its real themed category
    (still reachable via discover_tools()) plus tier1=True."""
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["screenshot"]
    assert spec.tier1 is True
    assert spec.core is False
    assert spec.category == "MEDIA"  # Phase 2: SCREENSHOTS → MEDIA


def test_themed_tool_has_no_core_or_tier1():
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["animation"]
    assert spec.category == "MEDIA"  # Phase 2: ANIMATION → MEDIA
    assert spec.core is False
    assert spec.tier1 is False
    assert spec.timeout_s == 30.0


def test_custom_timeout_preserved():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS["run_tests"].timeout_s == 300.0
    assert _SPECS["ping"].timeout_s == 5.0
    assert _SPECS["get_console"].timeout_s == 10.0


def test_core_count_is_11():
    """Phase 1a: CORE shrunk from 15 to 11 (delete_object/set_parent/scene/search_scene demoted)."""
    from unity_mcp.tools.tool_specs import _SPECS
    assert sum(1 for s in _SPECS.values() if s.core) == 11


def test_tier1_count_in_bounds():
    """Tier1 (non-core) count: prevents silent creep past 55; floor 30 guards against mass removal."""
    from unity_mcp.tools.tool_specs import _SPECS
    count = sum(1 for s in _SPECS.values() if s.tier1 and not s.core)
    assert 30 <= count <= 55, (
        f"Tier1 non-core count is {count} — outside [30, 55] bounds. "
        "Update this range intentionally if the tier1 set changed."
    )
