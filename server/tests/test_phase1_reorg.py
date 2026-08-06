"""P-12440 Phase 1 tool surface reorganization tests."""
import pytest
from unity_mcp.tools.gating import _CORE_TOOLS, TIER1, is_visible, is_deferred, reset, enable_category

_EXPECTED_CORE = frozenset({
    "batch", "compile_preflight", "create_object", "editor",
    "execute_code", "get_compile_errors", "get_component", "get_console", "get_hierarchy",
    "inspect", "manage_component", "mcp_status", "set_property",
})

_EXPECTED_TIER1_NONCORE = frozenset({
    "apply_scene_change", "await_compile", "delete_object", "discover_tools",
    "lint_scene_refs", "permission_prompt", "reconnect_unity", "resolve_tool_schema",
    "run_playtest", "run_playtest_suite", "run_tests", "run_tests_wait", "scene", "scene_change_plan",
    "screenshot", "search_scene", "set_active", "set_parent", "sync_unity",
    "validate_references", "verify_after_change",
})

_DEMOTED_TOOLS = frozenset({
    "alias_status", "ask", "ask_user", "configure_objects", "console_mark",
    "get_console_since", "get_test_results", "get_test_run", "lint_playtest",
    "release_smoke", "resolve_test_request", "setup_objects", "undo_last",
})


def test_core_exact_13():
    assert _CORE_TOOLS == _EXPECTED_CORE


def test_tier1_noncore_exact_21():
    assert TIER1 - _CORE_TOOLS == _EXPECTED_TIER1_NONCORE


def test_visible_surface_34():
    assert len(TIER1) == 34


def test_promoted_compile_preflight_is_core():
    assert "compile_preflight" in _CORE_TOOLS
    assert not is_deferred("compile_preflight")


def test_promoted_mcp_status_is_core():
    assert "mcp_status" in _CORE_TOOLS
    assert not is_deferred("mcp_status")


def test_demoted_resolve_scene_refs_not_core_not_tier1():
    assert "resolve_scene_refs" not in _CORE_TOOLS
    assert "resolve_scene_refs" not in TIER1


def test_demoted_apply_scene_change_tier1_not_core():
    assert "apply_scene_change" in TIER1
    assert "apply_scene_change" not in _CORE_TOOLS


def test_bulk_demotions_not_visible_after_reset():
    reset()
    for tool in _DEMOTED_TOOLS:
        assert not is_visible(tool), f"{tool} should not be visible after reset()"


def test_demoted_tools_visible_after_discover():
    reset()
    for cat in ("SYSTEM", "SCENE", "RUNTIME", "TESTS"):
        enable_category(cat)
    for tool in _DEMOTED_TOOLS:
        assert is_visible(tool), f"{tool} should be visible after enable_category"
