# Generated from dsl/tools.biome
# source-sha256=8605691b5a77c2ae753306620d1421adfe84064108a19b336760e682d35d2f23
# Edit the source, then run biome generate.

PARAM_DESCRIPTIONS: dict[str, dict[str, str]] = {
    'auto_wire': {
    },
    'await_compile': {
    },
    'compile_preflight': {
    },
    'create_object': {
        'name': "Name for the new GameObject",
        'parent': "Scene path of the parent (omit = scene root)",
        'components': "Comma-separated component types to add on creation (e.g. 'Rigidbody,BoxCollider')",
        'primitive': "Primitive mesh type: Cube|Sphere|Cylinder|Capsule|Plane|Quad",
        'prefab_path': "Asset path to instantiate from prefab (e.g. Assets/Prefabs/Enemy.prefab)",
        'scene': "Target scene name when multiple scenes are loaded (omit = active scene)",
    },
    'debug_animator': {
    },
    'debug_physics': {
    },
    'delete_object': {
        'id': "Instance ID of the GameObject to delete (from get_hierarchy)",
        'path': "Scene path of the GameObject to delete",
        'force': "True to delete non-empty container objects without error",
    },
    'find_objects': {
    },
    'get_component': {
        'path': "Scene path to the GameObject (e.g. /Player or /World/Enemy)",
        'type': "Component type name (e.g. 'Transform', 'Rigidbody', 'MeshRenderer')",
        'fields': "Comma-separated fields to return (e.g. 'mass,localPosition') — skips all others, saves tokens",
        'full': "Return raw uncompressed response (bypass distillation)",
        'compress': "Strip fields at their default value before TCP transfer — reduces response size",
    },
    'get_components_list': {
    },
    'get_metrics': {
    },
    'get_object_detail': {
    },
    'get_unity_events': {
    },
    'inspect': {
        'paths': "Comma-separated scene paths (e.g. '/Player,/Enemy,/Camera')",
        'components': "Comma-separated component types to read (omit = all)",
        'fields': "Comma-separated field names to project across all objects — reduces tokens",
        'full': "Return raw uncompressed response (bypass distillation)",
        'compress': "Strip fields at their default value before TCP transfer — reduces response size",
        'find_type': "Component type — auto-populates paths from all scene objects with this component",
    },
    'list_connections': {
    },
    'list_events': {
    },
    'manage_component': {
        'path': "Scene path to the GameObject",
        'type': "Component type (short: 'Button' or full namespace: 'UnityEngine.UI.Button')",
        'action': "'add' or 'remove' — not 'enable'/'disable' (use set_property m_Enabled for that)",
    },
    'object_diff': {
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
    'set_material': {
    },
    'set_parent': {
        'path': "Scene path of the GameObject to reparent",
        'parent': "New parent scene path (omit or null = move to scene root)",
        'world_position_stays': "True (default) = preserve world transform; False = keep local transform relative to new parent",
    },
    'set_property': {
        'path': "Scene path to the GameObject (e.g. /Player/Body)",
        'component': "Component type (empty string = Transform)",
        'prop': "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')",
        'value': "New value. ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy), or 'null'",
        'dry_run': "Show what would change without applying (safe preview)",
        'find_type': "Component type — bulk-sets prop on ALL scene objects with this component (no path needed)",
    },
    'set_property_delta': {
    },
    'set_sibling_index': {
    },
    'snapshot': {
    },
    'transfer_object': {
    },
    'unwire_event': {
    },
    'use_skill': {
        'name': "Saved learned-skill identifier from list_skills",
    },
    'wire_event': {
    },
}
