# Generated from dsl/tools.biome
# source-sha256=67237de7e69770f0d609124c47a552bf0c2865bf154aedf8a7ab7de4060cfadf
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
