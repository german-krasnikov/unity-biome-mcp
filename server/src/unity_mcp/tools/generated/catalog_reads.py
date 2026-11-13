# Generated from dsl/tools.biome
# source-sha256=385b6968907572e67385e5e7dc2e105b35a7e35bfd730786d74a3fea06784905
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
        'get_info',
        'validate_move',
        'get_dependencies',
        'find_dependents',
    }),
    'bake': frozenset({
        'settings',
        'status',
    }),
    'build': frozenset({
    }),
    'editor': frozenset({
        'state',
        'project_path',
    }),
    'manage_component': frozenset({
    }),
    'material': frozenset({
        'get',
        'list_properties',
        'list_slots',
        'get_errors',
        'list_shaders',
    }),
    'material_audit': frozenset({
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
        'status',
        'analyze',
        'compare',
        'list_sessions',
    }),
    'project_settings': frozenset({
        'get',
    }),
    'references': frozenset({
    }),
    'render_analyze': frozenset({
    }),
    'scene': frozenset({
        'list',
    }),
    'scene_environment': frozenset({
        'get',
    }),
    'scriptable_object': frozenset({
        'get',
        'list_types',
        'find',
    }),
    'shader': frozenset({
        'get',
        'graph_get',
    }),
    'spatial_query': frozenset({
    }),
    'timeline': frozenset({
        'get',
        'get_bindings',
    }),
    'transfer_object': frozenset({
    }),
    'uitk_element': frozenset({
    }),
    'uitk_file': frozenset({
        'read',
    }),
}
