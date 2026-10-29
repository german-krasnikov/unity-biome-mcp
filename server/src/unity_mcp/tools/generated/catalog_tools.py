# Generated from dsl/tools.biome
# source-sha256=8605691b5a77c2ae753306620d1421adfe84064108a19b336760e682d35d2f23
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


tools.expect('auto_wire', 'RW', "Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required.", (
    ('path', str),
    ('dry_run', bool, False),
))
tools.expect('await_compile', 'RO', "Block until Unity finishes compiling + reloading, then return compile errors.", (
    ('timeout', float, 60.0),
    ('expected_generation', oint, None),
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
tools.expect('find_objects', 'RO', "Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace).", (
    ('name', ostr, None),
    ('tag', ostr, None),
    ('layer', ostr, None),
    ('component', ostr, None),
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
tools.expect('get_metrics', 'RW', "Returns telemetry snapshot. Clears counters when reset=True. No confirmation required. format: text|json.", (
    ('format', str, "text"),
    ('reset', bool, False),
))
tools.expect('get_object_detail', 'RO', "Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation.", (
    ('id', int),
    ('full', bool, False),
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
tools.expect('list_connections', 'RO', "List Unity connection status.", (
))
tools.expect('list_events', 'RO', "Read persistent listeners on a UnityEvent field. Use after wire_event to verify.", (
    ('path', str),
    ('component', str),
    ('event', str),
))
tools.expect('manage_component', 'RW', "Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button').", (
    ('path', str),
    ('type', str),
    ('action', str),
))
tools.expect('object_diff', 'RO', "Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'.", (
    ('path_a', str),
    ('path_b', str),
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
