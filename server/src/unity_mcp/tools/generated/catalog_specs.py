# Generated from dsl/tools.biome
# source-sha256=656282e8dd231f9a6dcec3d3df084647c65a101ac4035b113dc85e22d74f4e9a
# Edit the source, then run biome generate.

SPEC_KWARGS: dict[str, dict] = {
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
    'use_skill': {
        'category': 'SYSTEM',
        'effect': 'RW',
        'direct_only': True,
    },
}
