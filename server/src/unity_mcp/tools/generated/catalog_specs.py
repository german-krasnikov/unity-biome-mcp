# Generated from dsl/tools.biome
# source-sha256=6a2dab2cbf6fdd17da6f1e423c9b365486638d3b7cf59738474a9a17c71396d4
# Edit the source, then run biome generate.

SPEC_KWARGS: dict[str, dict] = {
    'export_package': {
        'category': '_INTERNAL',
        'effect': 'RW',
        'timeout_s': 120.0,
    },
    'get_aliases': {
        'category': '_INTERNAL',
        'effect': 'RO',
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
    'set_active': {
        'category': 'SCENE',
        'effect': 'RW_IDEM',
        'tier1': True,
    },
    'set_sibling_index': {
        'category': 'SCENE',
        'effect': 'RW',
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
