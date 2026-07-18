"""M8 characterization test — golden snapshot of gating.py's derived collections.

Phase 2 update: CORE shrunk from 24 to 15 (9 tools demoted to SYSTEM tier1).
Phase 1a update: CORE shrunk from 15 to 11 (delete_object/set_parent/scene/search_scene → SCENE tier1).
Phase 1b update: runtime tools demoted; set_active/validate_references/execute_code/undo_last promoted.
Phase 1c update: run_playtest_file removed, then re-added in v0.84 as deprecated MCP alias.
18 themed categories replaced with 8 task-oriented ones.
"""

_CORE_TOOLS_SNAPSHOT = frozenset({
    # Wave 2: 'do' demoted from CORE to SYSTEM direct_only (10 tools)
    "batch", "create_object", "editor",
    "get_compile_errors", "get_component", "get_console", "get_hierarchy",
    "inspect", "manage_component", "set_property",
    # sprint1-2: 5 tools promoted tier1→core (#04 execute_code, #15 scene/verify tools)
    "execute_code",
    "apply_scene_change", "resolve_scene_refs", "scene_change_plan", "verify_after_change",
})

_TIER1_SNAPSHOT = frozenset({
    # CORE 10 (Wave 2: 'do' demoted to SYSTEM direct_only)
    "batch", "create_object", "editor",
    "get_compile_errors", "get_component", "get_console", "get_hierarchy",
    "inspect", "manage_component", "set_property",
    # tier1=True non-core
    # Phase 1a: delete_object/set_parent/scene/search_scene promoted from CORE to SCENE tier1
    "alias_status", "apply_scene_change", "ask", "ask_user", "await_compile",
    "compile_preflight", "configure_objects", "console_mark",
    "delete_object", "discover_tools",
    # Phase 1b: execute_code/set_active/validate_references/undo_last promoted
    "execute_code",
    "get_console_since", "get_test_results",
    "lint_playtest", "lint_scene_refs",
    "mcp_status", "permission_prompt", "reconnect_unity", "release_smoke",
    "resolve_scene_refs", "resolve_tool_schema",
    "run_playtest", "run_tests", "run_tests_wait",
    "scene", "scene_change_plan", "screenshot",
    "search_scene", "set_active", "set_parent",
    "setup_objects", "sync_unity",
    "undo_last", "validate_references", "verify_after_change",
})

_ALL_KNOWN_SNAPSHOT = frozenset({
    "analyze_lod_culling", "animation", "animator", "animator_intent",
    "apply_template", "ask", "ask_user", "asset", "auto_fix", "auto_wire",
    "autofit_collider", "await_compile", "batch", "budget_status",
    "check_colliders", "checkpoint", "compile_preflight", "configure_objects",
    "create_object", "create_ui", "debug", "debug_animator", "debug_physics",
    "delete_object", "diagnose", "discover_tools", "do", "doctor", "editor",
    "execute_code", "find_objects", "fingerprint",
    "get_capabilities", "get_changes", "get_compile_errors", "get_component",
    "get_components_list", "get_console", "get_enabled_tools", "get_frame_stats",
    "get_hierarchy", "get_memory", "get_metrics", "get_object_detail",
    "get_schema", "get_selection", "get_spatial_context", "get_test_count",
    "get_test_progress", "get_test_results", "get_watches", "inspect", "invoke_method",
    "list_connections", "list_skills", "list_templates",
    "load_session",
    "manage_component", "material", "material_audit", "menu", "move_to",
    "navmesh_query", "object_diff", "particle", "permission_prompt", "ping_object",
    "prefab", "profile", "project_settings", "query_state", "recompile",
    "reconnect_unity", "references", "region_clear", "render_analyze",
    # Phase 1c: run_playtest_file removed
    "resolve_tool_schema", "run_playtest", "run_playtest_suite", "run_tests",
    "save_session",
    "export_playtest_aliases_to_defs",
    "lint_playtest", "lint_playtest_suite", "lint_scene_refs",
    "save_skill", "save_template", "scan_scene", "scene", "scene_diff",
    "scene_environment", "scene_health", "screenshot", "screenshot_baseline",
    "screenshot_compare", "scriptable_object", "search_scene", "set_active",
    "set_llm_config", "set_material", "set_parent", "set_properties",
    "set_property", "set_property_delta", "set_rect", "set_runtime_property",
    "setup_objects", "shader", "smart_build", "snapshot", "spatial_query",
    "sync_playtest_aliases_from_defs",
    "sync_unity", "test_step", "timeline", "transfer_object", "ui_intent",
    "undo_last", "unwire_event", "use_skill", "validate_layout",
    "validate_playtest_aliases", "validate_references", "verify_after_change", "vfx_intent",
    "wait_until", "watch", "wire_event",
})

_THEMED_CATEGORIES_SNAPSHOT = {
    "SCENE": {
        # Phase 1a additions: demoted from CORE
        "delete_object", "scene", "search_scene", "set_parent",
        "apply_scene_change", "autofit_collider", "check_colliders", "configure_objects",
        "find_objects", "get_components_list", "get_object_detail", "get_selection",
        "get_spatial_context", "navmesh_query", "object_diff", "ping_object",
        "region_clear", "rename_object", "scene_change_plan", "scene_diff",
        "scene_environment", "set_active", "set_material", "set_properties",
        "set_property_delta", "set_sibling_index", "setup_objects", "spatial_query",
        "transfer_object",
    },
    "COMPONENTS": {"auto_wire", "references", "unwire_event", "wire_event"},
    "ASSETS": {"asset", "material", "material_audit", "prefab", "project_settings",
               "scriptable_object", "shader"},
    "MEDIA": {
        "analyze_lod_culling", "animation", "animator", "create_ui", "particle",
        "render_analyze", "screenshot", "screenshot_baseline", "screenshot_compare",
        "set_rect", "timeline", "ui_intent", "validate_layout", "vfx_intent",
    },
    "VERIFY": {
        "await_compile", "compile_preflight", "diagnose", "lint_scene_refs",
        "resolve_scene_refs", "scan_scene", "scene_health", "validate_references",
        "verify_after_change",
    },
    "RUNTIME": {
        "console_mark", "debug", "debug_animator", "debug_physics", "get_console_since",
        "get_frame_stats", "get_memory", "get_metrics", "get_watches",
        "invoke_method", "move_to", "profile", "query_state", "set_runtime_property",
        "snapshot", "wait_until", "watch",
    },
    "TESTS": {
        # Phase 1c: run_playtest_file removed
        "export_playtest_aliases_to_defs", "get_test_count", "get_test_progress",
        "get_test_results", "lint_playtest", "lint_playtest_suite",
        "run_playtest", "run_playtest_suite", "run_tests",
        "run_tests_wait", "sync_playtest_aliases_from_defs", "test_step",
        "validate_playtest_aliases",
    },
    "SYSTEM": {
        "alias_status", "animator_intent", "apply_template", "ask", "ask_user",
        "auto_fix", "budget_status", "checkpoint", "discover_tools", "doctor",
        "execute_code", "fingerprint", "get_capabilities", "get_changes",
        "get_enabled_tools", "get_schema", "list_connections", "list_skills",
        "list_templates", "load_session", "mcp_status", "menu", "permission_prompt",
        "recompile", "reconnect_unity", "resolve_tool_schema", "save_session",
        "save_skill", "save_template", "set_llm_config", "smart_build",
        "sync_unity", "undo_last", "use_skill",
    },
}

_CATEGORY_SIZES_SNAPSHOT = {
    # Phase 1a: object grows +4 (delete_object/set_parent/scene/search_scene added to SCENE)
    # Phase 1c: runtime shrinks -1 (run_playtest_file removed from TESTS)
    "advanced": 34, "animation": 14, "asset": 7, "connection": 34, "debug": 17,
    "object": 29, "perf": 17, "plugins": 34, "profiling": 17, "rendering": 14,
    "runtime": 30, "session": 34, "ui": 14,
}

_TIMEOUT_CATEGORIES_SNAPSHOT = {
    "apply_scene_change": 120.0,
    "ask_user": 300.0, "batch": 120.0, "compile_preflight": 60.0,
    "execute_code": 60.0, "export_package": 120.0, "fingerprint": 10.0,
    "get_console": 10.0, "get_hierarchy": 15.0,
    "get_test_count": 10.0, "get_version": 5.0, "import_package": 120.0,
    "lint_playtest": 60.0, "lint_playtest_suite": 120.0,
    "list_playtest_files": 10.0,
    "ping": 5.0, "run_playtest": 300.0,
    "run_playtest_suite": 3600.0, "run_tests": 300.0, "run_tests_wait": 300.0,
    "resolve_scene_refs": 15.0, "search_scene": 15.0, "verify_after_change": 600.0,
}


def test_core_tools_exact_snapshot():
    from unity_mcp.tools.gating import _CORE_TOOLS
    assert set(_CORE_TOOLS) == set(_CORE_TOOLS_SNAPSHOT)


def test_tier1_exact_snapshot():
    from unity_mcp.tools.gating import TIER1
    assert set(TIER1) == set(_TIER1_SNAPSHOT)


def test_all_known_contains_snapshot():
    """Subset check, not exact — register_tools() (plugin self-registration) can
    legitimately grow _ALL_KNOWN at runtime in environments with extra plugins."""
    from unity_mcp.tools.gating import _ALL_KNOWN
    missing = _ALL_KNOWN_SNAPSHOT - set(_ALL_KNOWN)
    assert not missing, f"tools dropped from _ALL_KNOWN by refactor: {sorted(missing)}"


def test_themed_categories_contains_snapshot():
    """Subset check per key, not exact — register_tools() can add plugin tools into
    an existing themed key at runtime in plugin-loaded environments."""
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    for key, expected_tools in _THEMED_CATEGORIES_SNAPSHOT.items():
        actual_tools = set(_THEMED_CATEGORIES.get(key, []))
        missing = expected_tools - actual_tools
        assert not missing, f"_THEMED_CATEGORIES[{key!r}] dropped tools by refactor: {sorted(missing)}"


def test_categories_sizes_at_least_snapshot():
    """Lower-bound check, not exact — CATEGORIES can only grow via register_tools()
    in environments with extra installed plugins."""
    from unity_mcp.tools.gating import CATEGORIES
    for key, min_size in _CATEGORY_SIZES_SNAPSHOT.items():
        actual_size = len(CATEGORIES.get(key, set()))
        assert actual_size >= min_size, f"CATEGORIES[{key!r}] shrank: {actual_size} < {min_size}"


def test_timeout_categories_exact_snapshot():
    from unity_mcp.timeout_categories import TIMEOUT_CATEGORIES
    assert TIMEOUT_CATEGORIES == _TIMEOUT_CATEGORIES_SNAPSHOT


def test_default_timeout_unchanged():
    from unity_mcp.timeout_categories import DEFAULT_TIMEOUT
    assert DEFAULT_TIMEOUT == 30.0
