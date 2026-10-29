# Generated from dsl/tools.biome
# source-sha256=d16b52065a6f8c7eedc0066d897ed8385bdbbc514bbacf9c620c1c355e021465
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
