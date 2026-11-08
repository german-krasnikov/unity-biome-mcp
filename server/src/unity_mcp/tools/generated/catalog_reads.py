# Generated from dsl/tools.biome
# source-sha256=57969cbaed0d0c5e9a2b61a501b70c29c90e633e3c4cccfd85d2201549e717a7
# Edit the source, then run biome generate.

ACTION_READS: dict[str, frozenset[str]] = {
    'animation': frozenset({
        'get',
        'get_clip_path',
        'get_events',
    }),
    'animator': frozenset({
        'get',
        'get_blend_tree',
    }),
    'asset': frozenset({
        'find',
        'find_dependents',
        'get_dependencies',
        'get_info',
        'validate_move',
    }),
    'bake': frozenset({
        'settings',
        'status',
    }),
    'editor': frozenset({
        'project_path',
        'state',
    }),
    'material': frozenset({
        'get',
        'get_errors',
        'list_properties',
        'list_shaders',
        'list_slots',
    }),
    'menu': frozenset({
        'list',
    }),
    'navmesh_query': frozenset({
        'get_settings',
        'path',
        'raycast',
        'sample',
        'status',
    }),
    'package': frozenset({
        'list',
        'search',
    }),
    'particle': frozenset({
        'get',
    }),
    'prefab': frozenset({
        'get_overrides',
    }),
    'profile': frozenset({
        'analyze',
        'compare',
        'list_sessions',
        'status',
    }),
    'project_settings': frozenset({
        'get',
    }),
    'scene': frozenset({
        'list',
    }),
    'scene_environment': frozenset({
        'get',
    }),
    'scriptable_object': frozenset({
        'find',
        'get',
        'list_types',
    }),
    'shader': frozenset({
        'get',
        'graph_get',
    }),
    'timeline': frozenset({
        'get',
        'get_bindings',
    }),
    'uitk_file': frozenset({
        'read',
    }),
}
