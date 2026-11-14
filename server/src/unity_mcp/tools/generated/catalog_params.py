# Generated from dsl/tools.biome
# source-sha256=22d5b56170705618537a6cda2e383e185768772939d760f99df4e8e641e19e58
# Edit the source, then run biome generate.

PARAM_DESCRIPTIONS: dict[str, dict[str, str]] = {
    'alias_status': {
    },
    'analyze_lod_culling': {
    },
    'animation': {
    },
    'animator': {
    },
    'animator_intent': {
    },
    'apply_scene_change': {
    },
    'apply_template': {
        'name': "Saved scene-template identifier from .claude/templates (without .cs)",
    },
    'ask': {
    },
    'ask_user': {
    },
    'asset': {
        'path': "Action-specific project asset/package path (usually Assets/...; import_package accepts a package file path)",
        'type': "Unity asset type filter or create kind (for example Material or ScriptableObject)",
        'name': "Asset-name search filter used by find (not a GameObject name)",
        'value': "Import-setting/property value for the selected asset action",
    },
    'attach_uitk': {
    },
    'auto_fix': {
    },
    'auto_wire': {
    },
    'autofit_collider': {
        'type': "Collider shape to fit: box|sphere|capsule",
    },
    'await_compile': {
    },
    'bake': {
    },
    'batch': {
        'commands': "One command per line (e.g. 'get_component path=/Player type=Transform')",
        'on_error': "Error behavior: continue (default) | stop — stop aborts remaining commands",
        'timeout': "Total timeout in seconds (default 75)",
        'atomic': "On failure, revert prior Undo-recorded Unity mutations; external/file/asset/package/process effects may remain",
        'validate_aliases': "Dry-run alias validation before executing any mutations",
    },
    'brief_build': {
    },
    'budget_status': {
    },
    'build': {
        'path': "Player build output file or directory (default Builds/<target>)",
    },
    'cancel_test_run': {
    },
    'check_colliders': {
    },
    'checkpoint': {
    },
    'checkpoint_create': {
    },
    'checkpoint_restore': {
    },
    'compile_preflight': {
    },
    'configure_objects': {
    },
    'console_mark': {
    },
    'create_object': {
        'name': "Name for the new GameObject",
        'parent': "Scene path of the parent (omit = scene root)",
        'components': "Comma-separated component types to add on creation (e.g. 'Rigidbody,BoxCollider')",
        'primitive': "Primitive mesh type: Cube|Sphere|Cylinder|Capsule|Plane|Quad",
        'prefab_path': "Asset path to instantiate from prefab (e.g. Assets/Prefabs/Enemy.prefab)",
        'scene': "Target scene name when multiple scenes are loaded (omit = active scene)",
    },
    'create_ui': {
        'type': "uGUI element type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView",
    },
    'debug': {
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
    'diagnose': {
    },
    'discover_tools': {
    },
    'do': {
    },
    'doctor': {
    },
    'editor': {
    },
    'execute_code': {
        'code': "C# code to execute in the Unity Editor context",
        'undo_label': "Label for the Undo group entry (default 'execute_code')",
    },
    'export_playtest_aliases_to_defs': {
    },
    'find_objects': {
    },
    'fingerprint': {
    },
    'get_capabilities': {
    },
    'get_changes': {
    },
    'get_changeset': {
    },
    'get_compile_errors': {
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
    'get_console': {
        'count': "Max log entries to return (default 10)",
        'level': "Filter by level: log|warning|error|exception|assert (omit = all)",
        'first': "Skip the first N entries (pagination offset)",
    },
    'get_console_since': {
    },
    'get_enabled_tools': {
    },
    'get_frame_stats': {
    },
    'get_hierarchy': {
        'depth': "Traversal depth (default 2; increase for deeper trees)",
        'root': "Scene path to scope the hierarchy (omit = whole scene)",
        'filter': "Substring filter on GameObject name",
        'components': "Show component types next to each object (increases token cost)",
        'compress': "Group repeated slot/point/mesh siblings to save tokens on dense scenes",
        'summary': "Return compact root-only counts (~60-100 tokens) instead of the full tree",
        'incremental': "Return NO_CHANGE if scene is unchanged since last call (saves tokens)",
        'full': "Bypass distillation — return raw response",
        'scene': "Filter to a single scene by name (multi-scene only)",
    },
    'get_memory': {
    },
    'get_metrics': {
    },
    'get_object_detail': {
    },
    'get_schema': {
    },
    'get_selection': {
    },
    'get_spatial_context': {
    },
    'get_test_count': {
    },
    'get_test_progress': {
    },
    'get_test_results': {
    },
    'get_test_run': {
        'compact': "Return a shorter summary instead of the full per-test breakdown",
    },
    'get_unity_events': {
    },
    'get_watches': {
    },
    'inspect': {
        'paths': "Comma-separated scene paths (e.g. '/Player,/Enemy,/Camera')",
        'components': "Comma-separated component types to read (omit = all)",
        'fields': "Comma-separated field names to project across all objects — reduces tokens",
        'full': "Return raw uncompressed response (bypass distillation)",
        'compress': "Strip fields at their default value before TCP transfer — reduces response size",
        'find_type': "Component type — auto-populates paths from all scene objects with this component",
    },
    'inspect_uitk': {
    },
    'invoke_method': {
    },
    'lint_playtest': {
        'path': "Project-relative path to .playtest file",
        'script': "Inline DSL (mutually exclusive with path)",
    },
    'lint_playtest_suite': {
    },
    'lint_scene_refs': {
        'path': "Project-relative path to a .playtest DSL file",
    },
    'lint_ugui': {
    },
    'lint_uitk': {
        'path': "Assets/ path to the UXML or USS file to validate",
    },
    'list_connections': {
    },
    'list_events': {
    },
    'list_skills': {
    },
    'list_templates': {
    },
    'list_test_runs': {
    },
    'load_session': {
    },
    'manage_component': {
        'path': "Scene path to the GameObject",
        'type': "Component type (short: 'Button' or full namespace: 'UnityEngine.UI.Button')",
        'action': "'add' or 'remove' — not 'enable'/'disable' (use set_property m_Enabled for that)",
    },
    'material': {
    },
    'material_audit': {
    },
    'mcp_status': {
    },
    'menu': {
        'path': "Unity Editor menu-item path to execute, or submenu prefix to list",
    },
    'move_to': {
    },
    'navmesh_query': {
    },
    'object_diff': {
    },
    'package': {
        'name': "Unity Package Manager package identifier for add/remove (for example com.unity.inputsystem)",
    },
    'particle': {
    },
    'permission_prompt': {
    },
    'ping_object': {
    },
    'prefab': {
    },
    'profile': {
    },
    'project_settings': {
    },
    'query_state': {
    },
    'recompile': {
    },
    'reconnect_unity': {
    },
    'references': {
    },
    'region_clear': {
    },
    'release_smoke': {
    },
    'rename_object': {
    },
    'render_analyze': {
    },
    'resolve_scene_refs': {
    },
    'resolve_test_request': {
    },
    'resolve_tool_schema': {
    },
    'run_playtest': {
        'script': "Inline DSL script (mutually exclusive with path)",
        'timeout': "Max seconds to wait for the playtest to finish (default 120)",
        'abort_on_fail': "Stop after the first failed step or automatic console failure; remaining steps, including teardown, are skipped",
        'defs': "Inline VAL definitions prepended to script (alias block)",
        'path': "Path to .playtest DSL file on disk (mutually exclusive with script)",
        'snapshot_on_failure': "Auto-capture screenshot on first ASSERT failure",
        'fresh': "Stop and restart Play Mode before running the script",
        'before_hook': "DSL commands to run before entering Play Mode",
        'after_hook': "DSL commands to run after Play Mode exits",
    },
    'run_playtest_suite': {
    },
    'run_tests': {
        'mode': "Test runner mode: EditMode or PlayMode",
        'filter': "NUnit filter expression (e.g. 'MyNamespace.MyTest')",
        'request_id': "Caller-supplied idempotency ID — reuse to retry a failed dispatch without double-running",
        'assemblies': "Comma-separated assembly names to restrict the run to",
        'categories': "Comma-separated NUnit category names to restrict the run to",
        'group': "Named test group to restrict the run to",
        'tests': "Comma-separated fully-qualified test names to restrict the run to",
    },
    'run_tests_wait': {
        'mode': "Test runner mode: EditMode or PlayMode",
        'filter': "NUnit filter expression (e.g. 'MyNamespace.MyTest')",
        'timeout': "Max seconds to wait for completion (default 900)",
        'poll_interval': "Seconds between status polls (default 5)",
        'request_id': "Caller-supplied idempotency ID",
    },
    'runtime_snapshot': {
    },
    'save_session': {
    },
    'save_skill': {
        'name': "File-safe identifier for the learned skill",
        'code': "C# code or batch commands to save as a reusable skill",
    },
    'save_template': {
        'name': "File-safe identifier for the reusable scene template",
        'code': "C# code or batch commands to save as a scene template",
    },
    'scan_scene': {
    },
    'scene': {
        'action': "Operation: new|open|save|discard|open_additive|close|set_active|list|save_copy",
        'path': "Asset path — required for open/save/open_additive/close/set_active",
        'scene': "Scene name for save/discard when multiple scenes are loaded",
    },
    'scene_change_plan': {
        'dry_run': "False (default) = create plan_id. True = probe pre-flights only, no plan created.",
    },
    'scene_diff': {
    },
    'scene_environment': {
    },
    'scene_health': {
    },
    'screenshot': {
        'width': "Image width in pixels (default 640)",
        'height': "Image height in pixels (default 480)",
        'camera': "View type: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game",
        'path': "For single_view/multi_view: target GameObject scene path; for standard captures: legacy project-contained .png destination (prefer output_path)",
        'output_path': "Unambiguous project-contained .png destination for every capture mode",
        'describe': "Haiku prompt for AI text description instead of returning file path (15-100x fewer tokens)",
        'raw': "Force returning file path even when describe= is set",
        'zoom': "Zoom factor — higher = closer",
        'angles': "Per-view Euler angles: 'ex,ey,ez|...' (underscore = skip this view)",
        'supersample': "Anti-alias quality 1-4 (higher = sharper, slower)",
        'offset': "Framing offset vector",
        'fixed_size': "Fixed framing size override",
        'highlight': "Comma-separated paths to highlight with bounding box (e.g. /Player:#FF0000)",
        'show_colliders': "Overlay collider wireframes on the screenshot",
        'angle': "Euler angle for single_view: front|left|top|iso|ex,ey,ez",
        'annotation_id': "Frame + highlight annotation by ID (auto-sets camera=annotation_frame)",
    },
    'screenshot_baseline': {
        'name': "File-safe baseline identifier (not a GameObject name; no '/', '\\', or '..')",
    },
    'screenshot_compare': {
        'name': "File-safe saved-baseline identifier (not a GameObject name; no '/', '\\', or '..')",
    },
    'scriptable_object': {
        'path': "Project asset path to the ScriptableObject .asset file",
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
    'set_llm_config': {
    },
    'set_material': {
    },
    'set_parent': {
        'path': "Scene path of the GameObject to reparent",
        'parent': "New parent scene path (omit or null = move to scene root)",
        'world_position_stays': "True (default) = preserve world transform; False = keep local transform relative to new parent",
    },
    'set_properties': {
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
    'set_rect': {
    },
    'set_sibling_index': {
    },
    'setup_objects': {
    },
    'shader': {
        'path': "Project asset path to a .shader or .shadergraph file",
        'shader_name': "Shader declaration name used when creating shader source",
        'name': "Action-specific Shader Graph property/node name (not a GameObject name)",
        'type': "Action-specific Shader Graph property value type (not a component type)",
    },
    'smart_build': {
    },
    'snapshot': {
    },
    'spatial_query': {
    },
    'sync_playtest_aliases_from_defs': {
    },
    'sync_unity': {
    },
    'test_step': {
    },
    'timeline': {
        'name': "Action-specific Timeline track, clip, or marker name",
        'value': "Action-specific Timeline value; set_track_offset accepts auto|transform|scene",
    },
    'transfer_object': {
    },
    'ui_intent': {
    },
    'uitk_element': {
        'name': "VisualElement name used after ref and before selector in addressing priority",
    },
    'uitk_file': {
        'path': "Assets/ path to a .uxml or .uss project file; Library/ and Packages/ are rejected",
    },
    'uitk_intent': {
        'name': "Base asset filename without extension (for <name>.uxml and <name>.uss)",
        'path': "Project asset output folder for the generated UXML/USS pair (default Assets/UI)",
    },
    'undo_last': {
    },
    'unwire_event': {
    },
    'use_skill': {
        'name': "Saved learned-skill identifier from list_skills",
    },
    'validate_playtest_aliases': {
    },
    'validate_references': {
    },
    'validate_triggers': {
    },
    'verify_after_change': {
    },
    'vfx_intent': {
    },
    'wait_until': {
        'value': "Expected field value to compare against (this tool does not set the field)",
        'abort_on_fail': "Stop Play Mode if the comparison times out (default False)",
    },
    'watch': {
    },
    'wire_event': {
    },
}
