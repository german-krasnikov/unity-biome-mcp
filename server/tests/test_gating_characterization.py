"""M8 characterization test — golden snapshot of gating.py's derived collections.

Captured from the LIVE pre-refactor gating.py (5-layer hand-typed chain:
_CORE_TOOLS -> _THEMED_CATEGORIES -> _CATEGORY_ALIAS -> CATEGORIES -> TIER1 -> _ALL_KNOWN)
before M8 flattens it to a single ToolSpec-seeded source of truth (tools/tool_specs.py).

Purpose: exact-equality safety net. The existing test_gating.py only asserts membership
("X in TIER1") which would NOT catch a stray extra/missing tool introduced by a buggy
_SPECS-generation script. This test catches that: any drift in the generated collections
vs. this frozen snapshot fails loudly.

This test must stay GREEN both BEFORE and AFTER the M8 refactor — it characterizes
externally-observable behavior, not internal representation.

Exact-equality vs. subset: _CORE_TOOLS/TIER1/TIMEOUT_CATEGORIES are
NEVER mutated by register_tools() (plugin self-registration) — real third-party
plugins loaded via entry_points (e.g. an environment with a private plugin package
installed) can only add to _ALL_KNOWN/CATEGORIES/_THEMED_CATEGORIES at runtime. So
those three collections get exact-equality checks; the plugin-mutable ones get
subset/lower-bound checks so this test isn't flaky across environments with extra
installed plugins.
"""

_CORE_TOOLS_SNAPSHOT = frozenset({
    "ask", "ask_user", "batch", "create_object", "delete_object", "discover_tools",
    "do", "doctor", "editor", "get_compile_errors", "get_component", "get_console",
    "get_enabled_tools", "get_hierarchy", "inspect", "list_connections",
    "manage_component", "permission_prompt", "reconnect_unity", "resolve_tool_schema",
    "scene", "search_scene", "set_parent", "set_property",
})

_TIER1_SNAPSHOT = frozenset({
    "alias_status",
    "apply_scene_change", "ask", "ask_user", "await_compile", "batch", "compile_preflight",
    "configure_objects", "console_mark", "create_object", "delete_object", "discover_tools", "do",
    "doctor", "editor", "get_compile_errors", "get_component",
    "get_console", "get_console_since", "get_enabled_tools", "get_hierarchy", "get_test_progress",
    "get_test_results", "inspect", "invoke_method", "list_connections",
    "manage_component", "mcp_status", "move_to",
    "permission_prompt", "query_state", "reconnect_unity", "resolve_scene_refs", "resolve_tool_schema",
    "export_playtest_aliases_to_defs",
    "lint_playtest", "lint_playtest_suite", "lint_scene_refs",
    "run_playtest", "run_playtest_file", "run_playtest_suite",
    "run_tests", "run_tests_wait", "scene", "screenshot",
    "sync_playtest_aliases_from_defs",
    "search_scene", "set_parent",
    "set_property", "set_runtime_property", "setup_objects",
    "scene_change_plan",
    "sync_unity", "test_step", "validate_playtest_aliases", "verify_after_change", "wait_until",
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
    "get_hierarchy", "get_memory", "get_metrics", "get_object_detail", "get_perf",
    "get_schema", "get_selection", "get_spatial_context", "get_test_count",
    "get_test_progress", "get_test_results", "get_watches", "inspect", "invoke_method",
    "list_connections", "list_skills", "list_templates",
    "load_session",
    "manage_component", "material", "material_audit", "menu", "move_to",
    "navmesh_query", "object_diff", "particle", "permission_prompt", "ping_object",
    "prefab", "profile", "project_settings", "query_state", "recompile",
    "reconnect_unity", "references", "region_clear", "render_analyze",
    "resolve_tool_schema", "run_playtest", "run_playtest_file", "run_playtest_suite", "run_tests",
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
    "ADVANCED_CODE": {"auto_fix", "await_compile", "checkpoint", "compile_preflight",
                       "diagnose", "execute_code", "get_schema", "menu", "recompile",
                       "smart_build", "sync_unity", "undo_last", "validate_references",
                       "verify_after_change"},
    "ANIMATION": {"animation", "animator", "particle", "timeline"},
    "ASSETS": {"asset", "prefab", "project_settings", "scriptable_object"},
    "COMPONENTS": {"auto_wire", "unwire_event", "wire_event"},
    "CONNECTION": set(),
    "DEBUG": {"debug", "get_metrics", "get_watches", "snapshot", "watch"},
    "META": {"animator_intent", "autofit_collider", "budget_status", "check_colliders",
              "configure_objects", "get_capabilities", "navmesh_query", "region_clear",
              "scan_scene", "scene_environment", "scene_health", "set_llm_config",
              "set_properties", "setup_objects", "spatial_query"},
    "PLUGINS": set(),
    "PROFILING": {"get_frame_stats", "get_memory", "profile"},
    "RENDERING": {"analyze_lod_culling", "render_analyze"},
    "RUNTIME": {"debug_animator", "debug_physics", "get_perf", "invoke_method",
                "move_to", "query_state", "set_runtime_property", "wait_until"},
    "SCENE_EDIT": {"find_objects", "get_components_list", "get_object_detail",
                   "get_selection", "object_diff", "ping_object", "set_active",
                   "set_material", "set_property_delta", "transfer_object"},
    "SCREENSHOTS": {"screenshot", "screenshot_baseline", "screenshot_compare"},
    "SESSION_SKILLS": {"apply_template", "fingerprint", "get_changes",
                        "list_skills", "list_templates", "load_session",
                        "save_session", "save_skill",
                        "save_template", "scene_diff", "use_skill"},
    "SHADERS_MATERIAL": {"material", "material_audit", "references", "shader"},
    "UI": {"create_ui", "get_spatial_context", "set_rect", "ui_intent", "validate_layout"},
    "UNIT_TESTS": {"export_playtest_aliases_to_defs", "get_test_count", "get_test_progress",
                   "get_test_results", "lint_playtest", "lint_playtest_suite",
                   "run_playtest", "run_playtest_file",
                   "run_playtest_suite", "run_tests",
                   "sync_playtest_aliases_from_defs", "test_step",
                   "validate_playtest_aliases"},
    "VFX": {"vfx_intent"},
}

_CATEGORY_SIZES_SNAPSHOT = {
    "advanced": 28, "animation": 4, "asset": 8, "connection": 0, "debug": 5,
    "object": 13, "perf": 5, "plugins": 0, "profiling": 3, "rendering": 2,
    "runtime": 14, "session": 14, "ui": 6,
}

_TIMEOUT_CATEGORIES_SNAPSHOT = {
    "apply_scene_change": 120.0,
    "ask_user": 300.0, "batch": 120.0, "compile_preflight": 60.0,
    "execute_code": 60.0, "export_package": 120.0, "fingerprint": 10.0,
    "get_console": 10.0, "get_hierarchy": 15.0,
    "get_test_count": 10.0, "get_version": 5.0, "import_package": 120.0,
    "lint_playtest": 60.0, "lint_playtest_suite": 120.0,
    "list_playtest_files": 10.0,
    "ping": 5.0, "run_playtest": 300.0, "run_playtest_file": 300.0,
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
    an existing themed key (e.g. 'CONNECTION') at runtime in plugin-loaded environments."""
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
