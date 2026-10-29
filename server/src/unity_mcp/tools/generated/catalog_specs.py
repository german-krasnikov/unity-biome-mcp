# Generated from dsl/tools.biome
# source-sha256=896692d2e17494813cb32187d5a874fe55d1875072e069d26b91037ba888c7b9
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
    'export_package': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'timeout_s': 120.0,
    },
    'get_aliases': {
        'category': '_INTERNAL',
        'effect': 'RO',
    },
    'get_metrics': {
        'category': 'RUNTIME',
        'effect': 'RW',
        'direct_only': True,
    },
    'get_playtest_run': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'direct_only': True,
        'unity_transport': True,
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
    'list_connections': {
        'category': 'SYSTEM',
        'effect': 'RO',
        'direct_only': True,
    },
    'list_playtest_files': {
        'category': '_INTERNAL',
        'effect': 'RO',
        'timeout_s': 10.0,
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
    'use_skill': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
}
