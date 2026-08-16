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
    # 'do' demoted from CORE to SYSTEM direct_only (Wave 2)
    for name in ("get_hierarchy", "batch", "set_property"):
        spec = _SPECS[name]
        assert spec.core is True
        assert spec.category == "CORE"


def test_do_demoted_to_system_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["do"]
    assert spec.core is False
    assert spec.category == "SYSTEM"
    assert spec.direct_only is True


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
    assert _SPECS["run_tests"].timeout_s == 30.0
    assert _SPECS["run_tests_wait"].timeout_s == 1200.0
    assert _SPECS["get_test_run"].timeout_s == 10.0
    assert _SPECS["ping"].timeout_s == 5.0
    assert _SPECS["get_console"].timeout_s == 10.0


def test_core_count_is_13():
    """P-12440 Phase 1: CORE shrunk from 15 to 13 (compile_preflight+mcp_status in, 4 scene/verify out)."""
    from unity_mcp.tools.tool_specs import _SPECS
    assert sum(1 for s in _SPECS.values() if s.core) == 13


def test_tier1_count_in_bounds():
    """Tier1 (non-core) count: P-12440 Phase 1 target is 20; guard [20, 25]."""
    from unity_mcp.tools.tool_specs import _SPECS
    count = sum(1 for s in _SPECS.values() if s.tier1 and not s.core)
    assert 20 <= count <= 25, (
        f"Tier1 non-core count is {count} — outside [20, 25] bounds. "
        "Update this range intentionally if the tier1 set changed."
    )


# P-301 / G12: run_playtest_suite must be tier1 so filter_by_tier keeps it visible
def test_run_playtest_suite_is_tier1():
    """G12: run_playtest_suite must be tier1=True so it survives filter_by_tier.
    Without tier1, it lives in _ALL_KNOWN but not TIER1 — filter_by_tier drops it,
    making it invisible to clients even though mcp.tool() registered it."""
    from unity_mcp.tools.tool_specs import _SPECS
    from unity_mcp.tools.gating import TIER1
    spec = _SPECS["run_playtest_suite"]
    assert spec.tier1 is True, "run_playtest_suite must be tier1=True to appear in gateway"
    assert "run_playtest_suite" in TIER1


def test_side_effecting_playtest_and_baseline_tools_are_writes():
    from unity_mcp.tools.tool_specs import _SPECS

    for name in (
        "test_step", "run_playtest", "run_playtest_suite", "screenshot_baseline",
    ):
        assert _SPECS[name].mutability == "write", name
