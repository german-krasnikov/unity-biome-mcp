# Generated from dsl/tools.biome
# source-sha256=8605691b5a77c2ae753306620d1421adfe84064108a19b336760e682d35d2f23
# Edit the source, then run biome generate.

SPEC_KWARGS: dict[str, dict] = {
    'auto_wire': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
    'await_compile': {
        'category': 'VERIFY',
        'effect': 'RO',
        'direct_only': True,
        'tier1': True,
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
    'export_package': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'timeout_s': 120.0,
    },
    'find_objects': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'get_aliases': {
        'category': '_INTERNAL',
        'effect': 'RO',
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
    'manage_component': {
        'category': 'CORE',
        'effect': 'RW',
        'core': True,
    },
    'object_diff': {
        'category': 'SCENE',
        'effect': 'RO',
    },
    'ping': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'timeout_s': 5.0,
    },
    'rename_object': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
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
    'set_sibling_index': {
        'category': 'SCENE',
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
    'start_playtest': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'direct_only': True,
        'unity_transport': True,
    },
    'transfer_object': {
        'category': 'SCENE',
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
    'wire_event': {
        'category': 'COMPONENTS',
        'effect': 'RW',
    },
}
