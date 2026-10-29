# Generated from dsl/tools.biome
# source-sha256=caf194d14490c5dc8328e227d4d813c58457ad5cd87cd6f0ea9e0b0402cbb823
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
