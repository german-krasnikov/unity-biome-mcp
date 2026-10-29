# Generated from dsl/tools.biome
# source-sha256=d16b52065a6f8c7eedc0066d897ed8385bdbbc514bbacf9c620c1c355e021465
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
