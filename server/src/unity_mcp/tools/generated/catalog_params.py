# Generated from dsl/tools.biome
# source-sha256=896692d2e17494813cb32187d5a874fe55d1875072e069d26b91037ba888c7b9
# Edit the source, then run biome generate.

PARAM_DESCRIPTIONS: dict[str, dict[str, str]] = {
    'auto_wire': {
    },
    'await_compile': {
    },
    'compile_preflight': {
    },
    'debug_animator': {
    },
    'debug_physics': {
    },
    'get_metrics': {
    },
    'list_connections': {
    },
    'rename_object': {
    },
    'search_scene': {
        'query': "Syntax: 'name text', 't:Component', 'tag=Tag', 'layer=N', 'active=bool' — combine with spaces",
        'root': "Scene path to scope the search (omit = whole scene)",
        'limit': "Max results (default 50; 0 = unlimited)",
        'scene': "Filter to a single scene by name (multi-scene only)",
    },
    'serialized_field_rename_audit': {
    },
    'set_active': {
        'path': "Scene path to the GameObject",
        'active': "True to activate, False to deactivate",
    },
    'set_sibling_index': {
    },
    'snapshot': {
    },
    'use_skill': {
        'name': "Saved learned-skill identifier from list_skills",
    },
}
