# Generated from dsl/tools.biome
# source-sha256=656282e8dd231f9a6dcec3d3df084647c65a101ac4035b113dc85e22d74f4e9a
# Edit the source, then run biome generate.

PARAM_DESCRIPTIONS: dict[str, dict[str, str]] = {
    'rename_object': {
    },
    'search_scene': {
        'query': "Syntax: 'name text', 't:Component', 'tag=Tag', 'layer=N', 'active=bool' — combine with spaces",
        'root': "Scene path to scope the search (omit = whole scene)",
        'limit': "Max results (default 50; 0 = unlimited)",
        'scene': "Filter to a single scene by name (multi-scene only)",
    },
    'set_active': {
        'path': "Scene path to the GameObject",
        'active': "True to activate, False to deactivate",
    },
    'set_sibling_index': {
    },
    'use_skill': {
        'name': "Saved learned-skill identifier from list_skills",
    },
}
