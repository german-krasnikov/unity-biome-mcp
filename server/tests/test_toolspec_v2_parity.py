"""Phase 1b parity tests. Runs without Unity ($0). pytest -m 'not live'."""
import pytest

# Snapshot the OLD hardcoded sets here so tests are self-contained.
# These are copied from middleware_types.py BEFORE the phase-1b changes.
_OLD_WRITE_CMDS = {
    "set_property", "set_property_delta", "create_object", "delete_object",
    "manage_component", "wire_event", "set_active", "set_material",
    "set_runtime_property", "set_rect", "move_to", "batch", "animation",
    "timeline", "animator", "particle", "shader", "material", "prefab",
    "scriptable_object", "asset", "scene", "create_ui", "execute_code",
    "menu", "project_settings", "set_parent", "unwire_event",
    "transfer_object", "rename_object", "set_sibling_index",
}
_OLD_READ_CMDS = {
    "get_hierarchy", "get_component", "inspect", "get_object_detail",
    "get_components_list", "find_objects", "search_scene",
    "query_state", "get_spatial_context", "scan_scene",
    "get_console", "get_compile_errors", "validate_references",
    "screenshot", "screenshot_compare",
    "get_selection", "get_capabilities",
    "alias_status", "get_aliases", "list_connections", "get_enabled_tools",
    "budget_status", "permission_prompt",
    "get_test_results", "get_test_progress", "get_test_count",
    "get_frame_stats", "get_memory", "get_metrics", "get_perf",
    "get_watches", "debug", "debug_animator", "debug_physics", "profile",
    "object_diff", "scene_diff", "scene_health", "material_audit",
    "analyze_lod_culling", "render_analyze", "fingerprint",
    "validate_layout", "check_colliders", "spatial_query",
    "get_schema", "get_changes",
    "compile_preflight", "await_compile", "auto_fix", "diagnose",
    "list_skills", "list_templates", "load_session",
    "ask", "ask_user",
}
_OLD_RUNTIME_ONLY = {
    "invoke_method", "set_runtime_property",
    "wait_until", "move_to", "query_state", "test_step",
    "run_playtest",
    "get_perf", "get_frame_stats", "debug_animator", "debug_physics",
    "watch_add",
    "profile",
}


def test_old_write_cmds_subset_of_derived():
    """Every old WRITE_CMDS member must be in derived WRITE_CMDS. Expansion OK."""
    from unity_mcp.middleware_types import WRITE_CMDS
    missing = _OLD_WRITE_CMDS - WRITE_CMDS
    assert not missing, f"Lost from WRITE_CMDS: {missing}"


def test_old_read_cmds_subset_of_derived():
    """Every old READ_CMDS member must be in derived READ_CMDS."""
    from unity_mcp.middleware_types import READ_CMDS
    missing = _OLD_READ_CMDS - READ_CMDS
    assert not missing, f"Lost from READ_CMDS: {missing}"


def test_old_runtime_only_subset_of_derived():
    """Every old _RUNTIME_ONLY_CMDS member must be in derived set."""
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
    missing = _OLD_RUNTIME_ONLY - _RUNTIME_ONLY_CMDS
    assert not missing, f"Lost from _RUNTIME_ONLY_CMDS: {missing}"


def test_no_overlap_read_write():
    """A tool cannot be both read and write."""
    from unity_mcp.middleware_types import READ_CMDS, WRITE_CMDS
    overlap = READ_CMDS & WRITE_CMDS
    assert not overlap, f"READ ∩ WRITE = {overlap}"


def test_spec_mutability_matches_read_cmds():
    """Every tool in derived READ_CMDS must have mutability='read' in _SPECS."""
    from unity_mcp.tools.tool_specs import _SPECS
    from unity_mcp.middleware_types import READ_CMDS
    for cmd in READ_CMDS:
        if cmd in _SPECS:
            assert _SPECS[cmd].mutability == 'read', \
                f"{cmd} in READ_CMDS but _SPECS[{cmd}].mutability != 'read'"


def test_spec_runtime_only_matches_runtime_cmds():
    """Every tool in derived _RUNTIME_ONLY_CMDS that is in _SPECS must have runtime_only=True."""
    from unity_mcp.tools.tool_specs import _SPECS
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
    for cmd in _RUNTIME_ONLY_CMDS:
        if cmd in _SPECS:
            assert _SPECS[cmd].runtime_only, \
                f"{cmd} in _RUNTIME_ONLY_CMDS but _SPECS[{cmd}].runtime_only=False"


def test_runtime_only_12_known_tools():
    """All 12 known runtime-only MCP tools are present."""
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
    expected = {
        "invoke_method", "set_runtime_property", "wait_until", "move_to",
        "query_state", "test_step", "run_playtest",
        "get_perf", "get_frame_stats", "debug_animator", "debug_physics", "profile",
    }
    missing = expected - _RUNTIME_ONLY_CMDS
    assert not missing, f"runtime_only tools missing: {missing}"


def test_watch_add_runtime_only_residual():
    """watch_add (non-MCP C# sub-cmd) must remain in runtime-only set."""
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
    assert "watch_add" in _RUNTIME_ONLY_CMDS


def test_plugin_api_update_still_works():
    """plugin_api.register_read_cmds mutates the derived set."""
    from unity_mcp.middleware_types import READ_CMDS
    READ_CMDS.discard("__test_plugin_tool__")
    original_len = len(READ_CMDS)
    from unity_mcp.plugin_api import register_read_cmds
    register_read_cmds("__test_plugin_tool__")
    assert "__test_plugin_tool__" in READ_CMDS
    READ_CMDS.discard("__test_plugin_tool__")
    assert len(READ_CMDS) == original_len


def test_write_cmds_bounded_size():
    """WRITE_CMDS should not grow silently. Update this count when intentionally adding writes."""
    from unity_mcp.middleware_types import WRITE_CMDS
    # If this fails, a tool was added without mutability='read' annotation.
    # Either annotate it as read, or increase this number intentionally.
    assert len(WRITE_CMDS) <= 62, f"WRITE_CMDS grew to {len(WRITE_CMDS)} — annotate new tools with mutability='read' if they don't mutate"
