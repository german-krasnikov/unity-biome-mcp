# Generated from dsl/tools.biome
# source-sha256=c708da88fd9bc9c1ae9eaa6f10411ee31a1b5710c6ab12af7a900ff3c814ac03
# Edit the source, then run biome generate.

SPEC_KWARGS: dict[str, dict] = {
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
    'check_colliders': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'compile_preflight': {
        'category': 'VERIFY',
        'effect': 'RO',
        'core': True,
        'timeout_s': 60.0,
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
    'do': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
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
    'get_component': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
    },
    'get_components_list': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_enabled_tools': {
        'category': 'SYSTEM',
        'effect': 'RO',
    },
    'get_hierarchy': {
        'category': 'CORE',
        'effect': 'RO',
        'core': True,
        'timeout_s': 15.0,
        'max_chars': 50000,
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
    'get_spatial_context': {
        'category': 'SCENE',
        'effect': 'RO',
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
    'prefab': {
        'category': 'ASSETS',
        'effect': 'RW',
    },
    'project_settings': {
        'category': 'ASSETS',
        'effect': 'RW_IDEM',
    },
    'query_state': {
        'category': 'RUNTIME',
        'effect': 'RO',
        'runtime_only': True,
    },
    'region_clear': {
        'category': 'SCENE',
        'effect': 'RW',
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
    'scan_scene': {
        'category': 'VERIFY',
        'effect': 'RO',
    },
    'scene': {
        'category': 'SCENE',
        'effect': 'DEL',
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
    'set_material': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
    },
    'set_parent': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
        'tier1': True,
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
    'shader': {
        'category': 'ASSETS',
        'effect': 'RW',
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
    'wire_event': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
}
