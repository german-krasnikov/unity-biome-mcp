# Generated from dsl/tools.biome
# source-sha256=16b40461ecc9b06e898b054cc35553ca287bcec1ac777667d0abcfce574b0966
# Edit the source, then run biome generate.

ARG_EXAMPLES: dict[str, dict[str, str]] = {
    'alias_status': {
    },
    'analyze_lod_culling': {
    },
    'animation': {
        'action': "get",
        'path': "/Main Camera",
    },
    'animator': {
        'path': "/Main Camera",
    },
    'ask_user': {
    },
    'asset': {
        'action': "list",
    },
    'attach_uitk': {
        'path': "/__seam_nonexistent",
    },
    'auto_wire': {
        'path': "/__seam_nonexistent",
    },
    'autofit_collider': {
        'path': "/__seam_nonexistent",
    },
    'bake': {
    },
    'batch': {
        'commands': "get_status",
    },
    'build': {
    },
    'cancel_test_run': {
        'run_id': "__seam_nonexistent",
    },
    'check_colliders': {
        'path': "/__seam_nonexistent",
    },
    'checkpoint': {
    },
    'compile_preflight': {
    },
    'create_object': {
        'name': "__seam_surface_probe",
    },
    'create_ui': {
        'type': "Button",
        'parent': "/__seam_nonexistent",
    },
    'debug_animator': {
    },
    'debug_physics': {
    },
    'delete_object': {
        'path': "/__seam_nonexistent",
    },
    'diagnose': {
    },
    'editor': {
        'action': "status",
    },
    'execute_code': {
    },
    'export_playtest_aliases_to_defs': {
    },
    'find_objects': {
        'name': "__seam_surface_nonexistent",
    },
    'fingerprint': {
    },
    'get_capabilities': {
    },
    'get_changes': {
    },
    'get_compile_errors': {
    },
    'get_component': {
        'path': "/__seam_nonexistent",
        'type': "Transform",
    },
    'get_components_list': {
        'id': "999999999",
    },
    'get_console': {
    },
    'get_enabled_tools': {
    },
    'get_frame_stats': {
    },
    'get_hierarchy': {
        'depth': "1",
    },
    'get_memory': {
    },
    'get_object_detail': {
        'id': "999999999",
    },
    'get_schema': {
        'type': "Transform",
    },
    'get_selection': {
    },
    'get_spatial_context': {
        'path': "/__seam_nonexistent",
    },
    'get_test_count': {
    },
    'get_test_progress': {
        'run_id': "__seam_nonexistent",
    },
    'get_test_results': {
        'run_id': "__seam_nonexistent",
    },
    'get_test_run': {
        'run_id': "__seam_nonexistent",
    },
    'get_unity_events': {
        'path': "/__seam_nonexistent",
    },
    'get_watches': {
    },
    'inspect': {
        'paths': "/Main Camera",
    },
    'inspect_uitk': {
        'path': "/__seam_nonexistent",
    },
    'invoke_method': {
    },
    'lint_playtest': {
        'script': "ASSERT_CONSOLE_CLEAN",
    },
    'lint_scene_refs': {
    },
    'lint_ugui': {
    },
    'lint_uitk': {
    },
    'list_events': {
        'path': "/__seam_nonexistent",
    },
    'list_test_runs': {
    },
    'manage_component': {
        'path': "/__seam_nonexistent",
        'type': "Rigidbody",
        'action': "add",
    },
    'material': {
        'action': "get",
        'path': "/__seam_nonexistent",
    },
    'material_audit': {
    },
    'menu': {
        'action': "list",
    },
    'move_to': {
    },
    'object_diff': {
        'path_a': "/__seam_nonexistent_a",
        'path_b': "/__seam_nonexistent_b",
    },
    'package': {
    },
    'particle': {
        'path': "/__seam_nonexistent",
    },
    'ping_object': {
        'path': "/__seam_nonexistent",
    },
    'prefab': {
        'action': "list",
    },
    'profile': {
    },
    'project_settings': {
        'action': "get",
        'target': "tags",
    },
    'query_state': {
    },
    'recompile': {
    },
    'references': {
        'action': "get",
        'path': "/__seam_nonexistent",
    },
    'region_clear': {
        'vertices': "0,0 1,0 1,1 0,1",
    },
    'rename_object': {
        'path': "/__seam_nonexistent",
        'name': "__seam_renamed",
    },
    'render_analyze': {
    },
    'resolve_scene_refs': {
    },
    'resolve_test_request': {
        'request_id': "__seam_nonexistent",
    },
    'run_playtest': {
    },
    'run_tests': {
    },
    'runtime_snapshot': {
    },
    'scan_scene': {
    },
    'scene': {
        'action': "get",
    },
    'scene_diff': {
    },
    'scene_environment': {
        'action': "get",
    },
    'scene_health': {
    },
    'screenshot': {
    },
    'scriptable_object': {
        'action': "list",
    },
    'search_scene': {
        'query': "__seam_nonexistent",
    },
    'serialized_field_rename_audit': {
        'type': "MyNamespace.PlayerStats",
        'old_field': "oldFieldName",
        'new_field': "newFieldName",
    },
    'set_active': {
        'path': "/__seam_nonexistent",
        'active': "false",
    },
    'set_material': {
        'path': "/__seam_nonexistent",
        'color': "#FFFFFF",
    },
    'set_parent': {
        'path': "/__seam_nonexistent",
        'parent': "/",
    },
    'set_property': {
        'path': "/__seam_nonexistent",
        'component': "Transform",
        'prop': "m_LocalPosition",
        'value': "0,0,0",
    },
    'set_property_delta': {
        'path': "/__seam_nonexistent",
        'component': "Transform",
        'prop': "m_LocalPosition.x",
        'delta': "1",
    },
    'set_rect': {
        'path': "/__seam_nonexistent",
    },
    'set_sibling_index': {
        'path': "/__seam_nonexistent",
        'index': "0",
    },
    'shader': {
        'path': "/__seam_nonexistent",
    },
    'spatial_query': {
        'path': "/__seam_nonexistent",
    },
    'sync_playtest_aliases_from_defs': {
    },
    'test_step': {
    },
    'timeline': {
        'path': "/__seam_nonexistent",
    },
    'transfer_object': {
        'path': "/__seam_nonexistent",
        'action': "move",
    },
    'uitk_element': {
        'action': "get",
        'path': "/__seam_nonexistent",
    },
    'uitk_file': {
    },
    'undo_last': {
    },
    'unwire_event': {
        'path': "/__seam_nonexistent",
        'event': "onClick",
    },
    'validate_playtest_aliases': {
    },
    'validate_references': {
        'path': "/__seam_nonexistent",
    },
    'validate_triggers': {
    },
    'wait_until': {
    },
    'wire_event': {
        'path': "/__seam_nonexistent",
        'event': "onClick",
        'target': "/__seam_tgt",
        'method': "SetActive",
    },
}
