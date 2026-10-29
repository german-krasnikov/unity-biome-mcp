# Generated from dsl/tools.biome
# source-sha256=09792bd514604115b328cdcefd5679dcc575340549f0847b997e65895a264476
# Edit the source, then run biome generate.

from .. import _catalog_runtime as rt
from .._common import bind

Any = rt.ANY
ostr = str | None
oint = int | None
ofloat = float | None
obool = bool | None

_send = None
_args = None
tools = rt.ToolSet()


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.install(mcp)


tools.expect('analyze_lod_culling', 'RO', "LOD group coverage + occlusion culling analysis.", (
    ('focus', ostr, None),
))
tools.expect('ask', 'RO', "Answer a read-only question about the Unity scene (AI-routed, not interactive — use `ask_user` to show a UI card and wait for user input).", (
    ('question', str),
))
tools.expect('asset', 'RW', "Asset database. Creates, moves, or deletes assets. No confirmation required. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package|read_text|write_text|reimport. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial|AnimatorController|ScriptableObject (class= required for SO). move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. validate_move path_only=True: syntax check only, skips AssetDatabase folder existence check (preflight). get_dependencies: forward deps. find_dependents: reverse deps (who references this asset). export_package: path+output[+include_deps=false to skip deps]. import_package: path (filesystem). read_text: path. write_text: path+content. reimport: path.", (
    ('action', str),
    ('path', ostr, None),
    ('type', ostr, None),
    ('name', ostr, None),
    ('folder', ostr, None),
    ('source', ostr, None),
    ('dest', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('recursive', bool, False),
    ('labels', ostr, None),
    ('output', ostr, None),
    ('include_deps', bool, True),
    ('content', ostr, None),
    ('class_name', ostr, None),
    ('path_only', bool, False),
))
tools.expect('auto_wire', 'RW', "Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required.", (
    ('path', str),
    ('dry_run', bool, False),
))
tools.expect('autofit_collider', 'RW', "Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule.", (
    ('path', str),
    ('type', str, "box"),
))
tools.expect('await_compile', 'RO', "Block until Unity finishes compiling + reloading, then return compile errors.", (
    ('timeout', float, 60.0),
    ('expected_generation', oint, None),
))
tools.expect('bake', 'RW', "Bake operations.", (
    ('target', str),
    ('action', ostr, None),
))
tools.expect('build', 'RW', "Build player. action: build.", (
    ('action', str),
    ('target', ostr, None),
    ('scenes', ostr, None),
    ('path', ostr, None),
    ('dev', bool, False),
))
tools.expect('check_colliders', 'RO', "Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given.", (
    ('path', ostr, None),
))
tools.expect('compile_preflight', 'RO', "Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile.", (
    ('file_path', str),
    ('new_content', str),
))
tools.expect('create_object', 'RW', "Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene).", (
    ('name', str),
    ('parent', ostr, None),
    ('components', ostr, None),
    ('primitive', ostr, None),
    ('prefab_path', ostr, None),
    ('scene', ostr, None),
))
tools.expect('debug_animator', 'RO', "[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile).", (
    ('path', str),
))
tools.expect('debug_physics', 'RO', "[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile).", (
    ('path', str),
    ('radius', float, 5.0),
))
tools.expect('delete_object', 'DEL', "Delete GameObject by instance ID or scene path. Deletes scene objects. No confirmation required. Provide one. force=True to delete non-empty containers.", (
    ('id', oint, None),
    ('path', ostr, None),
    ('force', bool, False),
))
tools.expect('diagnose', 'RO', "Read Unity compile/reload fact-signals atomically; returns typed verdict. For scene symptom analysis, use `debug`. For runtime component state, use `debug_animator` or `debug_physics`.", (
    ('prev_mvid', str, ""),
    ('expected_compile', bool, True),
))
tools.expect('export_playtest_aliases_to_defs', 'RW', "Export PlaytestConfig.asset aliases to a readable .defs text file.", (
    ('asset', str, "Assets/Configs/PlaytestConfig.asset"),
    ('defs', str, "Assets/PlaytestDefs/farm_core.defs"),
))
tools.expect('find_objects', 'RO', "Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace).", (
    ('name', ostr, None),
    ('tag', ostr, None),
    ('layer', ostr, None),
    ('component', ostr, None),
))
tools.expect('fingerprint', 'RO', "Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens.", (
    ('path', ostr, None),
    ('depth', int, 3),
))
tools.expect('get_changes', 'RW', "Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,", (
    ('clear', bool, True),
))
tools.expect('get_component', 'RO', "Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N.", (
    ('path', str),
    ('type', str, ""),
    ('fields', ostr, None),
    ('full', bool, False),
    ('compress', bool, False),
    ('component', ostr, None),
))
tools.expect('get_components_list', 'RO', "List all components on object by instance ID.", (
    ('id', int),
))
tools.expect('get_enabled_tools', 'RO', "List enabled tool names, comma-separated.", (
))
tools.expect('get_hierarchy', 'RO', "Scene hierarchy as text tree. For finding specific object by name/type use search_scene. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call. full=True: bypass distillation. scene: filter to a single scene by name (multi-scene only).", (
    ('depth', int, 2),
    ('root', ostr, None),
    ('filter', ostr, None),
    ('components', bool, False),
    ('compress', bool, False),
    ('summary', bool, False),
    ('incremental', bool, False),
    ('full', bool, False),
    ('scene', ostr, None),
))
tools.expect('get_metrics', 'RW', "Returns telemetry snapshot. Clears counters when reset=True. No confirmation required. format: text|json.", (
    ('format', str, "text"),
    ('reset', bool, False),
))
tools.expect('get_object_detail', 'RO', "Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation.", (
    ('id', int),
    ('full', bool, False),
))
tools.expect('get_spatial_context', 'RO', "Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only.", (
    ('path', str),
    ('radius', float, 5.0),
))
tools.expect('get_unity_events', 'RO', "List all UnityEvent persistent listeners in the active scene.", (
    ('path', ostr, None),
))
tools.expect('inspect', 'RO', "Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all).", (
    ('paths', ostr, None),
    ('components', ostr, None),
    ('fields', ostr, None),
    ('full', bool, False),
    ('compress', bool, False),
    ('find_type', ostr, None),
))
tools.expect('invoke_method', 'RW', "[Play Mode] Call public method on a component via reflection.", (
    ('path', str),
    ('component', str),
    ('method', str),
    ('args', str, ""),
))
tools.expect('lint_playtest_suite', 'RO', "Read-only preflight check across multiple .playtest files.", (
    ('pattern', ostr, None),
    ('suite_path', ostr, None),
))
tools.expect('lint_scene_refs', 'RO', "Read-only linter for scene references in DSL scripts or batch commands.", (
    ('path', ostr, None),
    ('snippet', ostr, None),
))
tools.expect('list_connections', 'RO', "List Unity connection status.", (
))
tools.expect('list_events', 'RO', "Read persistent listeners on a UnityEvent field. Use after wire_event to verify.", (
    ('path', str),
    ('component', str),
    ('event', str),
))
tools.expect('load_session', 'RO', "Load previous session context beside the current hierarchy.", (
))
tools.expect('manage_component', 'RW', "Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button').", (
    ('path', str),
    ('type', str),
    ('action', str),
))
tools.expect('material', 'RW', "Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path. get_errors: path (shader asset). list_shaders: filter (optional name filter). set_fields: path+value (newline-separated prop=val). set target: shared|instance|asset (default shared).", (
    ('action', str),
    ('path', ostr, None),
    ('object_path', ostr, None),
    ('shader', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('source', ostr, None),
    ('targets', ostr, None),
    ('slot', oint, None),
    ('filter', ostr, None),
    ('target', ostr, None),
))
tools.expect('material_audit', 'RO', "Material/texture scene-wide audit.", (
    ('action', str, "summary"),
    ('platform', ostr, None),
))
tools.expect('move_to', 'RW', "[Play Mode] Move character to position and wait for arrival.", (
    ('path', str),
    ('position', str),
    ('timeout', float, 15.0),
))
tools.expect('navmesh_query', 'RW', "NavMesh queries and management. Bakes or clears NavMesh data for bake/clear actions. No confirmation required. action: sample|path|raycast|bake|status|clear|get_settings|set_settings.", (
    ('action', str),
    ('center', ostr, None),
    ('from_pos', ostr, None),
    ('to', ostr, None),
    ('max_distance', float, 5.0),
    ('area_mask', int, -1),
    ('agentRadius', ofloat, None),
    ('agentHeight', ofloat, None),
    ('agentClimb', ofloat, None),
    ('agentSlope', ofloat, None),
))
tools.expect('object_diff', 'RO', "Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'.", (
    ('path_a', str),
    ('path_b', str),
))
tools.expect('package', 'RW', "Package manager. Adds or removes packages. No confirmation required. action: list|search|add|remove.", (
    ('action', str),
    ('name', ostr, None),
    ('version', ostr, None),
    ('query', ostr, None),
))
tools.expect('prefab', 'RW', "Prefab. Creates or modifies prefab assets. No confirmation required. action: save|create_variant|apply|revert|get_overrides|unpack|edit|instantiate.", (
    ('action', str),
    ('path', ostr, None),
    ('asset_path', ostr, None),
    ('base_path', ostr, None),
    ('variant_path', ostr, None),
    ('component', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('add_component', ostr, None),
    ('remove_component', ostr, None),
    ('recursive', bool, False),
    ('mode', ostr, None),
    ('scope', ostr, None),
    ('format', ostr, None),
))
tools.expect('project_settings', 'RW_IDEM', "Project settings. Modifies project settings when action=set. No confirmation required. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input.", (
    ('action', str),
    ('target', str),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('index', oint, None),
    ('build_target', ostr, None),
))
tools.expect('query_state', 'RO', "[Play Mode] Snapshot multiple game values in one call.", (
    ('queries', str),
))
tools.expect('region_clear', 'RW', "Delete (or preview) all objects whose XZ pivot is inside the polygon region. Deletes scene objects when dry_run=False. No confirmation required.", (
    ('vertices', str),
    ('dry_run', bool, True),
    ('filter', ostr, None),
    ('cap', int, 50),
))
tools.expect('rename_object', 'RW_IDEM', "Rename a GameObject. Returns new scene path after rename.", (
    ('path', str),
    ('name', str),
))


@tools.provide
async def rename_object(
    path: str,
    name: str,
) -> str:
    ("Rename a GameObject. Returns new scene path after rename."
     "\n    path: current scene path, &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy). name: new name (non-empty)."
     "\n    Note: all subsequent MCP calls must use the new path."
    )
    return await _send('rename_object', _args(
        path=rt.w_str(path),
        name=rt.w_str(name),
    ))


tools.expect('render_analyze', 'RO', "Rendering analysis.", (
    ('action', str),
    ('path', ostr, None),
    ('detail', str, "brief"),
    ('baseline_id', ostr, None),
    ('max_events', oint, None),
))
tools.expect('resolve_scene_refs', 'RO', "Read-only scene reference resolver.", (
    ('refs', str),
    ('fields', ostr, None),
))
tools.expect('run_playtest', 'RW', "[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit tests, use `run_tests`).", (
    ('script', ostr, None),
    ('timeout', float, 120.0),
    ('abort_on_fail', bool, False),
    ('defs', ostr, None),
    ('path', ostr, None),
    ('snapshot_on_failure', bool, False),
    ('fresh', bool, False),
    ('before_hook', ostr, None),
    ('after_hook', ostr, None),
    ('format', str, "text"),
))
tools.expect('run_playtest_suite', 'RW', "Run multiple .playtest files sequentially and return a compact matrix.", (
    ('pattern', ostr, None),
    ('suite_path', ostr, None),
    ('timeout_per_test', float, 120.0),
    ('stop_on_fail', bool, False),
    ('stop_after', bool, True),
    ('auto_play', bool, False),
    ('restart_between', bool, False),
    ('suite_timeout', float, 300.0),
    ('tag', ostr, None),
))
tools.expect('runtime_snapshot', 'RO', "Snapshot all runtime objects of a given component type. Returns per-object field dump.", (
    ('type', str),
    ('name', ostr, None),
    ('component', ostr, None),
    ('compress', bool, False),
))
tools.expect('save_session', 'RW', "Save current scene state to .claude/session-context.json for cold-start recovery.", (
))
tools.expect('scan_scene', 'RO', "Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats.", (
))
tools.expect('scene', 'DEL', "Scene management. action: new|open|save|discard|open_additive|close|set_active|list|save_copy.", (
    ('action', str),
    ('path', ostr, None),
    ('scene', ostr, None),
    ('include_unsaved', bool, True),
))
tools.expect('scene_diff', 'RO', "Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines.", (
))
tools.expect('scene_environment', 'RW', "Read/write scene environment: ambient light, fog, skybox, reflections.", (
    ('action', str, "get"),
    ('prop', ostr, None),
    ('value', ostr, None),
))
tools.expect('scene_health', 'RO', "Scene hierarchy/health audit.", (
    ('focus', str, "all"),
))
tools.expect('screenshot', 'RW', "Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens), raw=True forces path.", (
    ('width', int, 640),
    ('height', int, 480),
    ('camera', ostr, None),
    ('path', ostr, None),
    ('output_path', ostr, None),
    ('describe', ostr, None),
    ('raw', bool, False),
    ('zoom', ofloat, None),
    ('angles', ostr, None),
    ('supersample', oint, None),
    ('offset', ostr, None),
    ('fixed_size', ofloat, None),
    ('highlight', ostr, None),
    ('show_colliders', obool, None),
    ('angle', ostr, None),
    ('annotation_id', ostr, None),
))
tools.expect('screenshot_baseline', 'RW', "Save screenshot as baseline for visual regression. name: file-safe identifier.", (
    ('name', str, "default"),
    ('width', int, 640),
    ('height', int, 480),
    ('camera', ostr, None),
))
tools.expect('screenshot_compare', 'RW', "Compare current screenshot with saved baseline.", (
    ('name', str, "default"),
    ('width', int, 640),
    ('height', int, 480),
    ('camera', ostr, None),
    ('mode', str, "auto"),
    ('question', ostr, None),
))
tools.expect('scriptable_object', 'RW', "ScriptableObject. action: create|get|set|list_types|find. create: type+path[+fields]. get/set: path. set/create fields: \\n-separated prop=value pairs. get fields: comma-sep filter. find: type. list_types: filter.", (
    ('action', str),
    ('path', ostr, None),
    ('type', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('fields', ostr, None),
    ('filter', ostr, None),
))
tools.expect('search_scene', 'RO', "Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.", (
    ('query', str),
    ('root', ostr, None),
    ('limit', int, 50),
    ('scene', ostr, None),
))


@tools.provide
async def search_scene(
    query: str,
    root: ostr = None,
    limit: int = 50,
    scene: ostr = None,
) -> str:
    ("Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces."
     "\n    root: scope search to subtree (path or None for whole scene)."
     "\n    limit: max results (default 50; 0=unlimited)."
     "\n    scene: filter to a single scene by name (multi-scene only)."
    )
    return await _send('search_scene', _args(
        query=rt.w_str(query),
        root=rt.w_ostr(root),
        limit=rt.w_int(limit, 50),
        scene=rt.w_ostr(scene),
    ))


tools.expect('serialized_field_rename_audit', 'RO', "Audit [SerializeField] rename safety.", (
    ('type', str),
    ('old_field', str),
    ('new_field', str),
    ('include', str, "prefabs,scenes,scriptable_objects"),
))
tools.expect('set_active', 'RW_IDEM', "Set GameObject active/inactive.", (
    ('path', str),
    ('active', bool),
))


@tools.provide
async def set_active(
    path: str,
    active: bool,
) -> str:
    ("Set GameObject active/inactive."
    )
    return await _send('set_active', _args(
        path=rt.w_str(path),
        active=rt.w_bool(active),
    ))


tools.expect('set_material', 'RW_IDEM', "Set scene object material color (for full asset management use `material`). color: hex (#FF0000). shader: URP/Standard auto.", (
    ('path', str),
    ('color', str),
    ('shader', ostr, None),
))
tools.expect('set_parent', 'RW_IDEM', "Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent.", (
    ('path', str),
    ('parent', ostr, None),
    ('world_position_stays', bool, True),
))
tools.expect('set_property', 'RW_IDEM', "Set component property (Edit Mode, SerializedObject — for Play Mode use `invoke_method` or `execute_code`).", (
    ('path', ostr, None),
    ('component', str, ""),
    ('prop', str, ""),
    ('value', Any, None),
    ('dry_run', bool, False),
    ('find_type', ostr, None),
    ('ref_component_type', ostr, None),
))
tools.expect('set_property_delta', 'RW', "Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new.", (
    ('path', str),
    ('component', str),
    ('prop', str),
    ('delta', str),
))
tools.expect('set_sibling_index', 'RW', "Set sibling index of a GameObject within its parent. index=0 moves to first child.", (
    ('path', str),
    ('index', int),
))


@tools.provide
async def set_sibling_index(
    path: str,
    index: int,
) -> str:
    ("Set sibling index of a GameObject within its parent. index=0 moves to first child."
    )
    return await _send('set_sibling_index', _args(
        path=rt.w_str(path),
        index=rt.w_int(index),
    ))


tools.expect('snapshot', 'RO', "Capture or compare object state.", (
    ('path', str),
    ('label', str, "default"),
    ('compare', str, ""),
))
tools.expect('spatial_query', 'RO', "Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|raycast|spatial_map|objects_in_polygon.", (
    ('action', str),
    ('path', ostr, None),
    ('target', ostr, None),
    ('distance', ofloat, None),
    ('radius', ofloat, None),
    ('component', ostr, None),
    ('cell_size', ofloat, None),
    ('layer_mask', ostr, None),
    ('center', ostr, None),
    ('vertices', ostr, None),
    ('region_id', ostr, None),
    ('cap', oint, None),
))
tools.expect('sync_playtest_aliases_from_defs', 'RW', "Overwrite PlaytestConfig.asset aliases from a .defs text file.", (
    ('defs', str, "Assets/PlaytestDefs/farm_core.defs"),
    ('asset', str, "Assets/Configs/PlaytestConfig.asset"),
))
tools.expect('test_step', 'RW', "[Play Mode] Move character, snapshot state before/after, check console.", (
    ('path', str),
    ('position', str),
    ('checks_before', str, ""),
    ('checks_after', str, ""),
    ('wait_after', float, 0.5),
    ('timeout', float, 15.0),
))
tools.expect('transfer_object', 'RW', "Move or copy a GameObject to another loaded scene. action: move|copy.", (
    ('path', str),
    ('action', str),
    ('target_scene', ostr, None),
    ('parent', ostr, None),
    ('world_position_stays', bool, True),
))
tools.expect('unwire_event', 'DEL', "Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation required.", (
    ('path', str),
    ('component', str),
    ('event', str),
    ('index', oint, None),
))
tools.expect('use_skill', 'RW', "Execute a previously saved skill. params: comma-separated key=value for substitution.", (
    ('name', str),
    ('params', ostr, None),
))
tools.expect('validate_playtest_aliases', 'RO', "Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed.", (
    ('defs', str, "Assets/PlaytestDefs/farm_core.defs"),
    ('asset', str, "Assets/Configs/PlaytestConfig.asset"),
))
tools.expect('validate_triggers', 'RO', "Check 3D trigger/collider overlaps. Warns if triggers closer than min_distance meters.", (
    ('root', str, "/"),
    ('min_distance', float, 3.0),
))
tools.expect('verify_after_change', 'RW', "Single verification gate after code/scene changes.", (
    ('changed_files', str, ""),
    ('test_filter', str, ""),
    ('run_tests_mode', str, ""),
    ('playtests', str, ""),
    ('mark_id', str, ""),
    ('timeout', float, 300.0),
    ('auto_play', bool, False),
    ('restart_between', bool, False),
    ('suite_timeout', float, 300.0),
))
tools.expect('wait_until', 'RW_IDEM', "[Play Mode] Poll field until it matches value (or timeout).", (
    ('path', str),
    ('component', str),
    ('field', str),
    ('value', str),
    ('timeout', float, 5.0),
    ('negate', bool, False),
    ('abort_on_fail', bool, False),
))
tools.expect('wire_event', 'RW', "Wire UnityEvent persistent listener. Mutates scene. No confirmation required.", (
    ('path', str),
    ('component', str),
    ('event', str),
    ('target', str),
    ('method', str),
    ('arg_type', str, "void"),
    ('arg_value', ostr, None),
    ('target_component_type', ostr, None),
    ('parameter_types', ostr, None),
))
