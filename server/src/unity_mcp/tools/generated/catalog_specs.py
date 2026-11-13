# Generated from dsl/tools.biome
# source-sha256=16b40461ecc9b06e898b054cc35553ca287bcec1ac777667d0abcfce574b0966
# Edit the source, then run biome generate.

SPEC_KWARGS: dict[str, dict] = {
    'alias_status': {
        'category': 'SYSTEM',
        'effect': 'RO',
    },
    'analyze_lod_culling': {
        'category': 'MEDIA',
        'effect': 'RO',
    },
    'animation': {
        'category': 'MEDIA',
        'effect': 'RW',
    },
    'animator': {
        'category': 'MEDIA',
        'effect': 'RW',
    },
    'animator_intent': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'apply_scene_change': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 120.0,
    },
    'apply_template': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'ask': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'ask_user': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'unity_transport': True,
        'timeout_s': 300.0,
    },
    'asset': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'attach_uitk': {
        'category': 'UITOOLKIT',
        'effect': 'RW',
    },
    'auto_fix': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'auto_wire': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
    'autofit_collider': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'await_compile': {
        'category': 'VERIFY',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
    },
    'bake': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'batch': {
        'category': 'CORE',
        'effect': 'RW',
        'core': True,
        'timeout_s': 120.0,
    },
    'brief_build': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'budget_status': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'build': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
        'timeout_s': 300.0,
    },
    'cancel_test_run': {
        'category': 'TESTS',
        'effect': 'RW_IDEM',
        'timeout_s': 10.0,
    },
    'check_colliders': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'checkpoint': {
        'category': 'SYSTEM',
        'effect': 'RW',
    },
    'checkpoint_create': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'checkpoint_restore': {
        'category': 'SYSTEM',
        'effect': 'DEL',
        'direct_only': True,
    },
    'compile_preflight': {
        'category': 'VERIFY',
        'effect': 'RO',
        'core': True,
        'timeout_s': 15.0,
    },
    'compile_status': {
        'category': '_INTERNAL',
        'effect': 'RO',
    },
    'configure_objects': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
    },
    'console_mark': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
    },
    'create_object': {
        'category': 'CORE',
        'effect': 'RW',
        'core': True,
    },
    'create_ui': {
        'category': 'UGUI',
        'effect': 'RW',
    },
    'debug': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'direct_only': True,
    },
    'debug_animator': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'runtime_only': True,
    },
    'debug_physics': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'runtime_only': True,
    },
    'delete_object': {
        'category': 'SCENE',
        'effect': 'DEL',
        'tier1': True,
    },
    'diagnose': {
        'category': 'VERIFY',
        'effect': 'RO',
    },
    'discover_tools': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
        'session': True,
    },
    'do': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'doctor': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'editor': {
        'category': 'CORE',
        'effect': 'RW',
        'core': True,
    },
    'execute_code': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'core': True,
        'timeout_s': 60.0,
    },
    'export_package': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'timeout_s': 120.0,
    },
    'export_playtest_aliases_to_defs': {
        'category': 'TESTS',
        'effect': 'RW',
    },
    'find_objects': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'fingerprint': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'get_aliases': {
        'category': '_INTERNAL',
        'effect': 'RO',
    },
    'get_capabilities': {
        'category': 'SYSTEM',
        'effect': 'RO',
    },
    'get_changes': {
        'category': 'SYSTEM',
        'effect': 'RW',
    },
    'get_changeset': {
        'category': 'VERIFY',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 5.0,
    },
    'get_compile_errors': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
    },
    'get_component': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
    },
    'get_components_list': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_console': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
        'timeout_s': 10.0,
        'max_chars': 20000,
    },
    'get_console_since': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
    },
    'get_enabled_tools': {
        'category': 'SYSTEM',
        'effect': 'RO',
    },
    'get_frame_stats': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'runtime_only': True,
    },
    'get_hierarchy': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
        'timeout_s': 15.0,
        'max_chars': 50000,
    },
    'get_memory': {
        'category': 'RUNTIME',
        'effect': 'RO',
    },
    'get_metrics': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'direct_only': True,
    },
    'get_object_detail': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_playtest_run': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'direct_only': True,
        'unity_transport': True,
    },
    'get_schema': {
        'category': 'SYSTEM',
        'effect': 'RO',
    },
    'get_selection': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_spatial_context': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_status': {
        'category': '_INTERNAL',
        'effect': 'RO',
    },
    'get_test_count': {
        'category': 'TESTS',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'get_test_progress': {
        'category': 'TESTS',
        'effect': 'RO',
    },
    'get_test_results': {
        'category': 'TESTS',
        'effect': 'RO',
    },
    'get_test_run': {
        'category': 'TESTS',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'get_unity_events': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_version': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'timeout_s': 5.0,
    },
    'get_watches': {
        'category': 'RUNTIME',
        'effect': 'RO',
    },
    'import_package': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'timeout_s': 120.0,
    },
    'inspect': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
    },
    'inspect_uitk': {
        'category': 'UITOOLKIT',
        'effect': 'RO',
        'timeout_s': 15.0,
    },
    'invoke_method': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'runtime_only': True,
    },
    'lint_playtest': {
        'category': 'TESTS',
        'effect': 'RO',
        'timeout_s': 60.0,
    },
    'lint_playtest_suite': {
        'category': 'TESTS',
        'effect': 'RO',
        'direct_only': True,
        'timeout_s': 120.0,
    },
    'lint_scene_refs': {
        'category': 'VERIFY',
        'effect': 'RO',
        'tier1': True,
        'timeout_s': 30.0,
    },
    'lint_ugui': {
        'category': 'UGUI',
        'effect': 'RO',
        'timeout_s': 15.0,
    },
    'lint_uitk': {
        'category': 'UITOOLKIT',
        'effect': 'RO',
        'timeout_s': 15.0,
    },
    'list_connections': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'list_events': {
        'category': 'COMPONENTS',
        'effect': 'RO',
    },
    'list_playtest_files': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'list_skills': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'list_templates': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'list_test_runs': {
        'category': 'TESTS',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'load_session': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'manage_component': {
        'category': 'CORE',
        'effect': 'RW',
        'core': True,
    },
    'material': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'material_audit': {
        'category': 'ASSETS',
        'effect': 'RO',
    },
    'mcp_status': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'core': True,
    },
    'menu': {
        'category': 'SYSTEM',
        'effect': 'RW',
    },
    'move_to': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
        'runtime_only': True,
    },
    'navmesh_query': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
    },
    'object_diff': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'package': {
        'category': 'ASSETS',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
        'timeout_s': 60.0,
    },
    'particle': {
        'category': 'MEDIA',
        'effect': 'RW',
    },
    'permission_prompt': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
    },
    'ping': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'timeout_s': 5.0,
    },
    'ping_object': {
        'category': 'SCENE',
        'effect': 'RO',
        'session': True,
    },
    'prefab': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'profile': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'runtime_only': True,
    },
    'project_settings': {
        'category': 'ASSETS',
        'effect': 'RW_IDEM',
    },
    'query_state': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'runtime_only': True,
        'timeout_s': 10.0,
    },
    'recompile': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'timeout_s': 60.0,
    },
    'reconnect_unity': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
        'session': True,
    },
    'references': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
    'region_clear': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'release_smoke': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'rename_object': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
    },
    'render_analyze': {
        'category': 'MEDIA',
        'effect': 'RO',
    },
    'resolve_scene_refs': {
        'category': 'VERIFY',
        'effect': 'RO',
        'timeout_s': 15.0,
    },
    'resolve_test_request': {
        'category': 'TESTS',
        'effect': 'RO',
        'timeout_s': 10.0,
    },
    'resolve_tool_schema': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
    },
    'run_playtest': {
        'category': 'TESTS',
        'effect': 'RW',
        'tier1': True,
        'direct_only': True,
        'unity_transport': True,
        'timeout_s': 300.0,
    },
    'run_playtest_suite': {
        'category': 'TESTS',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 3600.0,
    },
    'run_tests': {
        'category': 'TESTS',
        'effect': 'RW_IDEM',
        'tier1': True,
        'direct_only': True,
        'unity_transport': True,
    },
    'run_tests_wait': {
        'category': 'TESTS',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 1200.0,
    },
    'runtime_snapshot': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'max_chars': 50000,
    },
    'save_session': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'save_skill': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'save_template': {
        'category': 'SYSTEM',
        'effect': 'RW_IDEM',
        'direct_only': True,
    },
    'scan_scene': {
        'category': 'VERIFY',
        'effect': 'RO',
    },
    'scene': {
        'category': 'SCENE',
        'effect': 'DEL',
        'tier1': True,
    },
    'scene_change_plan': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
    },
    'scene_diff': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'scene_environment': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'scene_health': {
        'category': 'VERIFY',
        'effect': 'RO',
    },
    'screenshot': {
        'category': 'MEDIA',
        'effect': 'RW',
        'tier1': True,
        'direct_only': True,
        'unity_transport': True,
    },
    'screenshot_baseline': {
        'category': 'MEDIA',
        'effect': 'RW',
        'direct_only': True,
    },
    'screenshot_compare': {
        'category': 'MEDIA',
        'effect': 'RW',
        'direct_only': True,
    },
    'scriptable_object': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'search_scene': {
        'category': 'SCENE',
        'effect': 'RO',
        'tier1': True,
        'timeout_s': 15.0,
        'max_chars': 30000,
    },
    'serialized_field_rename_audit': {
        'category': 'VERIFY',
        'effect': 'RO',
    },
    'set_active': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
        'tier1': True,
    },
    'set_llm_config': {
        'category': 'SYSTEM',
        'effect': 'RW_IDEM',
        'direct_only': True,
    },
    'set_material': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
    },
    'set_parent': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
        'tier1': True,
    },
    'set_properties': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
    },
    'set_property': {
        'category': 'CORE',
        'effect': 'RW_IDEM',
        'core': True,
    },
    'set_property_delta': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'set_rect': {
        'category': 'UGUI',
        'effect': 'RW_IDEM',
    },
    'set_sibling_index': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'setup_objects': {
        'category': 'SCENE',
        'effect': 'RW',
        'direct_only': True,
    },
    'shader': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'smart_build': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'snapshot': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'direct_only': True,
    },
    'source_patch_write': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
    },
    'spatial_query': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'start_playtest': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
    },
    'sync_playtest_aliases_from_defs': {
        'category': 'TESTS',
        'effect': 'RW',
    },
    'sync_unity': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
    },
    'test_step': {
        'category': 'TESTS',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
        'runtime_only': True,
    },
    'timeline': {
        'category': 'MEDIA',
        'effect': 'RW',
    },
    'transfer_object': {
        'category': 'SCENE',
        'effect': 'RW',
    },
    'ui_intent': {
        'category': 'UGUI',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
    },
    'uitk_element': {
        'category': 'UITOOLKIT',
        'effect': 'RW',
        'timeout_s': 15.0,
    },
    'uitk_file': {
        'category': 'UITOOLKIT',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
    },
    'uitk_intent': {
        'category': 'UITOOLKIT',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 60.0,
    },
    'undo_last': {
        'category': 'SYSTEM',
        'effect': 'RW',
    },
    'unwire_event': {
        'category': 'COMPONENTS',
        'effect': 'DEL',
    },
    'use_skill': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
    'validate_playtest_aliases': {
        'category': 'TESTS',
        'effect': 'RO',
    },
    'validate_references': {
        'category': 'VERIFY',
        'effect': 'RO',
        'tier1': True,
    },
    'validate_triggers': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'verify_after_change': {
        'category': 'VERIFY',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
        'timeout_s': 600.0,
    },
    'vfx_intent': {
        'category': 'MEDIA',
        'effect': 'RW',
        'direct_only': True,
        'tier1': True,
    },
    'wait_until': {
        'category': 'RUNTIME',
        'effect': 'RW_IDEM',
        'direct_only': True,
        'unity_transport': True,
        'runtime_only': True,
    },
    'watch': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'direct_only': True,
    },
    'wire_event': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
}
