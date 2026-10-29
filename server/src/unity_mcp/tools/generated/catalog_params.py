# Generated from dsl/tools.biome
# source-sha256=6a2dab2cbf6fdd17da6f1e423c9b365486638d3b7cf59738474a9a17c71396d4
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
