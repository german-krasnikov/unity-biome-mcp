# Generated from dsl/tools.biome
# source-sha256=651201e784144372792873459f22dff06e4a66eaac23e1145bf10fd93893162b
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


tools.expect('alias_status', 'RO', "Check alias table health: loaded/empty/stale, sources, and total alias count.", (
))


@tools.provide
async def alias_status(
) -> str:
    ("Check alias table health: loaded/empty/stale, sources, and total alias count."
    )
    return await _send('alias_status', _args(
    ))


tools.expect('analyze_lod_culling', 'RO', "LOD group coverage + occlusion culling analysis.", (
    ('focus', ostr, None),
))


@tools.provide
async def analyze_lod_culling(
    focus: ostr = None,
) -> str:
    ("LOD group coverage + occlusion culling analysis."
     "\n    focus: lod|culling|occlusion|null=all."
    )
    return await _send('analyze_lod_culling', _args(
        focus=rt.w_ostr(focus),
    ))


tools.expect('animation', 'RW', "Animate GameObject properties via AnimationClip. Use when you need to read or author keyframe animation on a specific object (not an Animator state machine — use `animator` for that, not this).", (
    ('action', str),
    ('path', str),
    ('clip', ostr, None),
    ('clip_name', ostr, None),
    ('property', ostr, None),
    ('keys', ostr, None),
    ('time', ofloat, None),
    ('component_type', ostr, None),
    ('binding_path', ostr, None),
    ('tangent', ostr, None),
    ('function_name', ostr, None),
    ('int_param', oint, None),
    ('float_param', ofloat, None),
    ('string_param', ostr, None),
))


@tools.provide
async def animation(
    action: str,
    path: str,
    clip: ostr = None,
    clip_name: ostr = None,
    property: ostr = None,
    keys: ostr = None,
    time: ofloat = None,
    component_type: ostr = None,
    binding_path: ostr = None,
    tangent: ostr = None,
    function_name: ostr = None,
    int_param: oint = None,
    float_param: ofloat = None,
    string_param: ostr = None,
) -> str:
    ("Animate GameObject properties via AnimationClip. Use when you need to read or author keyframe animation on a specific object (not an Animator state machine — use `animator` for that, not this)."
     "\n    action: get (list clips/keys) | create (new AnimationClip on object) | edit (add/replace keyframes) | preview (scrub to time) | add_event | remove_event | get_events | set_wrap (keys='loop'|'once'|'pingpong'|'clamp') | set_framerate (keys='30') | get_clip_path (returns asset path)."
     "\n    clip=clip name, keys='t:0 v:(0,0,0); t:1 v:(0,2,0)', property=e.g. localPosition.x."
     "\n    component_type: Unity component to animate (default: Transform). Examples: Light, Camera, Rigidbody."
     "\n    binding_path: sub-object path for EditorCurveBinding (e.g. 'Head/Jaw'). Default '' = root."
     "\n    tangent: tangent mode for keyframes: auto (default) | smooth | linear | constant."
     "\n    function_name: method name for add_event. int_param/float_param/string_param: event parameters."
    )
    return await _send('animation', _args(
        action=rt.w_str(action),
        path=rt.w_str(path),
        clip=rt.w_ostr(clip),
        clip_name=rt.w_ostr(clip_name),
        property=rt.w_ostr(property),
        keys=rt.w_ostr(keys),
        time=rt.w_ofloat(time, raw=True),
        component_type=rt.w_ostr(component_type),
        binding_path=rt.w_ostr(binding_path),
        tangent=rt.w_ostr(tangent),
        function_name=rt.w_ostr(function_name),
        int_param=rt.w_oint(int_param, raw=True),
        float_param=rt.w_ofloat(float_param, raw=True),
        string_param=rt.w_ostr(string_param),
    ))


tools.expect('animator', 'RW', "Animator Controller — state machine. Modifies animator assets. No confirmation required. (use `animation` for keyframe clips, `timeline` for cinematics). action: get|add_param|add_state|add_transition|set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|set_avatar|rename_state|rename_param.", (
    ('action', str),
    ('path', str),
    ('state', ostr, None),
    ('states', ostr, None),
    ('params', ostr, None),
    ('source', ostr, None),
    ('target', ostr, None),
    ('conditions', ostr, None),
    ('duration', ofloat, None),
    ('exit_time', ofloat, None),
    ('has_exit_time', obool, None),
    ('type', ostr, None),
    ('name', ostr, None),
    ('blend_type', ostr, None),
    ('param', ostr, None),
    ('param_y', ostr, None),
    ('children', ostr, None),
    ('edit_action', ostr, None),
    ('layer', Any, None),
    ('weight', ofloat, None),
    ('blending', ostr, None),
    ('value', ostr, None),
    ('avatar_path', ostr, None),
))
tools.expect('animator_intent', 'RW', "Convert NL intent to Unity Animator Controller setup via DSL.", (
    ('target', str),
    ('intent', str),
    ('dry_run', bool, False),
))
tools.expect('apply_scene_change', 'RW', "Execute scene mutations with atomic apply, post-verify, and optional save.", (
    ('plan_id', str),
    ('commands', str),
    ('verify', bool, True),
    ('save', bool, True),
))
tools.expect('apply_template', 'RW', "Apply a scene template (.cs file from .claude/templates/).", (
    ('name', str),
    ('params', ostr, None),
))
tools.expect('ask', 'RO', "Answer a read-only question about the Unity scene (AI-routed, not interactive — use `ask_user` to show a UI card and wait for user input).", (
    ('question', str),
))
tools.expect('ask_user', 'RO', "Show a question card in Unity chat; wait for user answer (interactive UI — use `ask` for read-only AI scene questions instead).", (
    ('questions', str),
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
tools.expect('attach_uitk', 'RW', "Attach UIDocument (or PanelRenderer on Unity 6.4+) to a GameObject (use for UI Toolkit runtime panels).", (
    ('path', str),
    ('uxml', ostr, None),
    ('panel_settings', ostr, None),
    ('sort_order', oint, None),
))


@tools.provide
async def attach_uitk(
    path: str,
    uxml: ostr = None,
    panel_settings: ostr = None,
    sort_order: oint = None,
) -> str:
    ("Attach UIDocument (or PanelRenderer on Unity 6.4+) to a GameObject (use for UI Toolkit runtime panels)."
     "\n    Side effect: mutates the scene by adding one Undo-recorded UIDocument after validating"
     "\n    every supplied asset; it does not create UXML or PanelSettings assets."
     "\n    path: scene path to the target GameObject."
     "\n    uxml: Assets/ path to .uxml VisualTreeAsset (optional; component added without VTA if omitted)."
     "\n    panel_settings: optional Assets/ path to a PanelSettings asset; omitted leaves the field unset."
     "\n    sort_order: UIDocument.sortingOrder (default 0; ignored on Unity 6.4+ PanelRenderer)."
     "\n    err: if UIDocument or PanelRenderer already present — remove it first or use inspect_uitk/uitk_element."
     "\n    "
    )
    return await _send('attach_uitk', _args(
        path=rt.w_str(path),
        uxml=rt.w_ostr(uxml),
        panel_settings=rt.w_ostr(panel_settings),
        sort_order=rt.w_oint(sort_order, raw=True),
    ))


tools.expect('auto_fix', 'RO', "Analyze recent Unity errors and ask MCP client sampling for a fix suggestion.", (
    ('ctx', Any),
))
tools.expect('auto_wire', 'RW', "Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required.", (
    ('path', str),
    ('dry_run', bool, False),
))


@tools.provide
async def auto_wire(
    path: str,
    dry_run: bool = False,
) -> str:
    ("Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required."
     "\n    dry_run=true previews without applying. Returns wired/ambiguous/no-match summary."
    )
    return await _send('auto_wire', _args(
        path=rt.w_str(path),
        dry_run=rt.w_bool(dry_run, False),
    ))


tools.expect('autofit_collider', 'RW', "Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule.", (
    ('path', str),
    ('type', str, "box"),
))


@tools.provide
async def autofit_collider(
    path: str,
    type: str = "box",
) -> str:
    ("Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule."
    )
    return await _send('autofit_collider', _args(
        path=rt.w_str(path),
        type=rt.w_str(type),
    ))


tools.expect('await_compile', 'RO', "Block until Unity finishes compiling + reloading, then return compile errors.", (
    ('timeout', float, 60.0),
    ('expected_generation', oint, None),
))
tools.expect('bake', 'RW', "Bake operations.", (
    ('target', str),
    ('action', ostr, None),
))


@tools.provide
async def bake(
    target: str,
    action: ostr = None,
) -> str:
    ("Bake operations."
     "\n    target: lighting|occlusion."
     "\n    action (lighting): start(default)|status|cancel|clear|settings."
     "\n    action (occlusion): start(default)|status|clear."
     "\n    Poll status after start — lighting bake is async."
    )
    return await _send('bake', _args(
        target=rt.w_str(target),
        action=rt.w_ostr(action),
    ))


tools.expect('batch', 'RW', "Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop (default continue). timeout: seconds (default 75; inner soft-timeout capped at 60s). atomic: reverts prior Undo-recorded mutations on failure; external/file/asset/package/process effects may remain. PREFER over individual tool calls.", (
    ('commands', str),
    ('on_error', str, "continue"),
    ('timeout', float, 75.0),
    ('atomic', bool, False),
    ('validate_aliases', bool, False),
))
tools.expect('brief_build', 'RO', "Use to get a snapshot of project state before starting work.", (
    ('kinds', str, "console,compile_errors,hierarchy"),
    ('budget', int, 2000),
))
tools.expect('budget_status', 'RO', "Returns Haiku cost: session/cap/day/skipped features. Text format.", (
))
tools.expect('build', 'RW', "Build player. action: build.", (
    ('action', str),
    ('target', ostr, None),
    ('scenes', ostr, None),
    ('path', ostr, None),
    ('dev', bool, False),
))


@tools.provide
async def build(
    action: str,
    target: ostr = None,
    scenes: ostr = None,
    path: ostr = None,
    dev: bool = False,
) -> str:
    ("Build player. action: build."
     "\n    target: StandaloneWindows64|StandaloneOSX|Android|iOS|WebGL (default: active)."
     "\n    scenes: comma-sep asset paths (default: Build Settings list)."
     "\n    path: output path (default: Builds/<target>)."
     "\n    dev: development build flag."
    )
    return await _send('build', _args(
        action=rt.w_str(action),
        target=rt.w_ostr(target),
        scenes=rt.w_ostr(scenes),
        path=rt.w_ostr(path),
        dev=rt.w_bool(dev, False),
    ))


tools.expect('cancel_test_run', 'RW_IDEM', "Request cancellation of one exact test run; cancellation is asynchronous.", (
    ('run_id', str),
))


@tools.provide
async def cancel_test_run(
    run_id: str,
) -> str:
    ("Request cancellation of one exact test run; cancellation is asynchronous."
    )
    return await _send('cancel_test_run', _args(
        run_id=rt.w_str(run_id),
    ))


tools.expect('check_colliders', 'RO', "Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given.", (
    ('path', ostr, None),
))


@tools.provide
async def check_colliders(
    path: ostr = None,
) -> str:
    ("Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given."
    )
    return await _send('check_colliders', _args(
        path=rt.w_ostr(path),
    ))


tools.expect('checkpoint', 'RW', "Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity.", (
    ('label', str, "checkpoint"),
))


@tools.provide
async def checkpoint(
    label: str = "checkpoint",
) -> str:
    ("Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity."
    )
    return await _send('checkpoint', _args(
        label=rt.w_str(label),
    ))


tools.expect('checkpoint_create', 'RW', "Create a durable checkpoint before an agent turn.", (
    ('paths', str, ""),
))
tools.expect('checkpoint_restore', 'DEL', "Restore files to their pre-turn state.", (
    ('checkpoint_id', str),
    ('force', bool, False),
))
tools.expect('compile_preflight', 'RO', "Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile.", (
    ('file_path', str),
    ('new_content', str),
))
tools.expect('configure_objects', 'RW', "Configure multiple objects at once.", (
    ('config', str),
))
tools.expect('console_mark', 'RO', "Create a console watermark. Returns mark_id encoding current timestamp.", (
    ('label', str, ""),
))
tools.expect('create_object', 'RW', "Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene).", (
    ('name', str),
    ('parent', ostr, None),
    ('components', ostr, None),
    ('primitive', ostr, None),
    ('prefab_path', ostr, None),
    ('scene', ostr, None),
))


@tools.provide
async def create_object(
    name: str,
    parent: ostr = None,
    components: ostr = None,
    primitive: ostr = None,
    prefab_path: ostr = None,
    scene: ostr = None,
) -> str:
    ("Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene)."
    )
    return await _send('create_object', _args(
        name=rt.w_str(name),
        parent=rt.w_ostr(parent),
        components=rt.w_ostr(components),
        primitive=rt.w_ostr(primitive),
        prefab_path=rt.w_ostr(prefab_path),
        scene=rt.w_ostr(scene),
    ))


tools.expect('create_ui', 'RW', "Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView. Auto-creates Canvas if needed. render_mode: SSO (ScreenSpaceOverlay, default)|SSC (ScreenSpaceCamera)|WorldSpace. font_min/font_max: enable TMP autoSizing for Text type.", (
    ('type', str),
    ('name', ostr, None),
    ('parent', ostr, None),
    ('anchor', ostr, None),
    ('pos', ostr, None),
    ('size', ostr, None),
    ('pivot', ostr, None),
    ('color', ostr, None),
    ('text', ostr, None),
    ('font_size', ostr, None),
    ('render_mode', ostr, None),
    ('font_min', ostr, None),
    ('font_max', ostr, None),
))


@tools.provide
async def create_ui(
    type: str,
    name: ostr = None,
    parent: ostr = None,
    anchor: ostr = None,
    pos: ostr = None,
    size: ostr = None,
    pivot: ostr = None,
    color: ostr = None,
    text: ostr = None,
    font_size: ostr = None,
    render_mode: ostr = None,
    font_min: ostr = None,
    font_max: ostr = None,
) -> str:
    ("Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView. Auto-creates Canvas if needed. render_mode: SSO (ScreenSpaceOverlay, default)|SSC (ScreenSpaceCamera)|WorldSpace. font_min/font_max: enable TMP autoSizing for Text type."
    )
    return await _send('create_ui', _args(
        type=rt.w_str(type),
        name=rt.w_ostr(name),
        parent=rt.w_ostr(parent),
        anchor=rt.w_ostr(anchor),
        pos=rt.w_ostr(pos),
        size=rt.w_ostr(size),
        pivot=rt.w_ostr(pivot),
        color=rt.w_ostr(color),
        text=rt.w_ostr(text),
        font_size=rt.w_ostr(font_size),
        render_mode=rt.w_ostr(render_mode),
        font_min=rt.w_ostr(font_min),
        font_max=rt.w_ostr(font_max),
    ))


tools.expect('debug', 'RO', "AI-assisted scene debug: gather diagnostic context based on symptom (not compile/reload — use `diagnose` for that; not runtime state — use `debug_animator` or `debug_physics`).", (
    ('symptom', str, ""),
    ('path', str, ""),
    ('gather', str, ""),
))
tools.expect('debug_animator', 'RO', "[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile).", (
    ('path', str),
))


@tools.provide
async def debug_animator(
    path: str,
) -> str:
    ("[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile)."
     "\n    path: scene path to GameObject with Animator component."
    )
    return await _send('debug_animator', _args(
        path=rt.w_str(path),
    ))


tools.expect('debug_physics', 'RO', "[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile).", (
    ('path', str),
    ('radius', float, 5.0),
))


@tools.provide
async def debug_physics(
    path: str,
    radius: float = 5.0,
) -> str:
    ("[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile)."
     "\n    path: scene path to GameObject."
     "\n    radius: overlap sphere radius for nearby detection (default 5m)."
    )
    return await _send('debug_physics', _args(
        path=rt.w_str(path),
        radius=rt.w_float(radius, raw=True),
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
tools.expect('do', 'RW', "Convert natural language intent into Unity scene operations. Use when scene structure unknown or task is ambiguous. NOT for targeted mutations on known objects — use batch directly.", (
    ('intent', str),
    ('dry_run', bool, False),
))
tools.expect('doctor', 'RW', "Run health diagnostics. fix=True removes safe stale port/lock files.", (
    ('fix', bool, False),
))
tools.expect('editor', 'RW', "Editor state/control. action: state|play|pause|stop|select|project_path|mutation_mode.", (
    ('action', str, "state"),
    ('path', ostr, None),
    ('paths', ostr, None),
    ('enable', obool, None),
))
tools.expect('execute_code', 'RW', "Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile.", (
    ('code', str),
    ('undo_label', str, "execute_code"),
))


@tools.provide
async def execute_code(
    code: str,
    undo_label: str = "execute_code",
) -> str:
    ("Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile."
     "\n    Security uses a configurable source-pattern scan; the default AllowAll level skips it. Execution is not sandboxed."
     "\n    Bare statements are auto-wrapped in a static class — no boilerplate needed."
     "\n    Example: \"var go = new GameObject(\\\"Test\\\"); return go.name;\""
    )
    return await _send('execute_code', _args(
        code=rt.w_str(code),
        undo_label=rt.w_str(undo_label),
    ))


tools.expect('export_playtest_aliases_to_defs', 'RW', "Export PlaytestConfig.asset aliases to a readable .defs text file.", (
    ('asset', str, "Assets/Configs/PlaytestConfig.asset"),
    ('defs', str, "Assets/PlaytestDefs/farm_core.defs"),
))


@tools.provide
async def export_playtest_aliases_to_defs(
    asset: str = "Assets/Configs/PlaytestConfig.asset",
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
) -> str:
    ("Export PlaytestConfig.asset aliases to a readable .defs text file."
     "\n    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset)."
     "\n    defs: project-relative output path (default: Assets/PlaytestDefs/farm_core.defs)."
    )
    return await _send('export_playtest_aliases_to_defs', _args(
        asset=rt.w_str(asset),
        defs=rt.w_str(defs),
    ))


tools.expect('find_objects', 'RO', "Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace).", (
    ('name', ostr, None),
    ('tag', ostr, None),
    ('layer', ostr, None),
    ('component', ostr, None),
))


@tools.provide
async def find_objects(
    name: ostr = None,
    tag: ostr = None,
    layer: ostr = None,
    component: ostr = None,
) -> str:
    ("Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace)."
    )
    return await _send('find_objects', _args(
        name=rt.w_ostr(name),
        tag=rt.w_ostr(tag),
        layer=rt.w_ostr(layer),
        component=rt.w_ostr(component),
    ))


tools.expect('fingerprint', 'RO', "Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens.", (
    ('path', ostr, None),
    ('depth', int, 3),
))


@tools.provide
async def fingerprint(
    path: ostr = None,
    depth: int = 3,
) -> str:
    ("Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens."
    )
    return await _send('fingerprint', _args(
        path=rt.w_ostr(path),
        depth=rt.w_int(depth, raw=True),
    ))


tools.expect('get_capabilities', 'RO', "Unity version, platform, render pipeline, scripting backend, and optional packages available.", (
))


@tools.provide
async def get_capabilities(
) -> str:
    ("Unity version, platform, render pipeline, scripting backend, and optional packages available."
    )
    return await _send('get_capabilities', _args(
    ))


tools.expect('get_changes', 'RW', "Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,", (
    ('clear', bool, True),
))


@tools.provide
async def get_changes(
    clear: bool = True,
) -> str:
    ("Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,"
     "\n    play mode, scene open/save, selection. Returns chronological event list or NO_CHANGES."
    )
    return await _send('get_changes', _args(
        clear=rt.w_bool(clear),
    ))


tools.expect('get_changeset', 'RO', "Return the current ChangeSet: accumulated mutations this session.", (
))
tools.expect('get_compile_errors', 'RO', "Compilation errors with file:line:column. Not lost on Console.Clear(). Structured, typed.", (
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


@tools.provide
async def get_components_list(
    id: int,
) -> str:
    ("List all components on object by instance ID."
    )
    return await _send('get_components_list', _args(
        id=rt.w_int(id, raw=True),
    ))


tools.expect('get_console', 'RO', "Recent console logs. For C# compile errors use get_compile_errors instead. keyword: case-insensitive substring filter. count_only: return N matches as string. since: only logs from last N seconds.", (
    ('count', int, 10),
    ('level', ostr, None),
    ('first', int, 0),
    ('keyword', ostr, None),
    ('count_only', bool, False),
    ('since', ofloat, None),
))
tools.expect('get_console_since', 'RO', "Console entries after the watermark created by console_mark().", (
    ('mark_id', str),
    ('level', ostr, None),
    ('count', int, 500),
    ('keyword', ostr, None),
    ('count_only', bool, False),
))
tools.expect('get_enabled_tools', 'RO', "List enabled tool names, comma-separated.", (
))


@tools.provide
async def get_enabled_tools(
) -> str:
    ("List enabled tool names, comma-separated."
    )
    return await _send('get_enabled_tools', _args(
    ))


tools.expect('get_frame_stats', 'RO', "Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No session needed.", (
    ('include', str, ""),
))


@tools.provide
async def get_frame_stats(
    include: str = "",
) -> str:
    ("Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No session needed."
     "\n    include: narrow output — e.g. 'gc' for GC stats only."
    )
    return await _send('get_frame_stats', _args(
        include=rt.w_str(include, ""),
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
tools.expect('get_memory', 'RO', "Memory snapshot.", (
    ('include', str, "all"),
))


@tools.provide
async def get_memory(
    include: str = "all",
) -> str:
    ("Memory snapshot."
     "\n    include: all|textures|meshes|audio|gc — narrow the asset-type breakdown."
    )
    return await _send('get_memory', _args(
        include=rt.w_str(include, "all"),
    ))


tools.expect('get_metrics', 'RW', "Returns telemetry snapshot. Clears counters when reset=True. No confirmation required. format: text|json.", (
    ('format', str, "text"),
    ('reset', bool, False),
))
tools.expect('get_object_detail', 'RO', "Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation.", (
    ('id', int),
    ('full', bool, False),
))
tools.expect('get_schema', 'RO', "Get all serialized fields of a component type with types. Use before set_property to know exact field names.", (
    ('type', str),
))


@tools.provide
async def get_schema(
    type: str,
) -> str:
    ("Get all serialized fields of a component type with types. Use before set_property to know exact field names."
    )
    return await _send('get_schema', _args(
        type=rt.w_str(type),
    ))


tools.expect('get_selection', 'RO', "Currently selected GameObject: path and component list.", (
))


@tools.provide
async def get_selection(
) -> str:
    ("Currently selected GameObject: path and component list."
    )
    return await _send('get_selection', _args(
    ))


tools.expect('get_spatial_context', 'RO', "Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only.", (
    ('path', str),
    ('radius', float, 5.0),
))


@tools.provide
async def get_spatial_context(
    path: str,
    radius: float = 5.0,
) -> str:
    ("Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only."
    )
    return await _send('get_spatial_context', _args(
        path=rt.w_str(path),
        radius=rt.w_float(radius),
    ))


tools.expect('get_test_count', 'RO', "Number of edit-mode and play-mode tests in the project.", (
))


@tools.provide
async def get_test_count(
) -> str:
    ("Number of edit-mode and play-mode tests in the project."
    )
    return await _send('get_test_count', _args(
    ))


tools.expect('get_test_progress', 'RO', "Legacy progress facade. Pass run_id to correlate the response.", (
    ('run_id', ostr, None),
))
tools.expect('get_test_results', 'RO', "Legacy result facade. Pass run_id to prevent reading a stale latest run.", (
    ('run_id', ostr, None),
))
tools.expect('get_test_run', 'RO', "Return the durable JSON snapshot for one exact test run.", (
    ('run_id', str),
))
tools.expect('get_unity_events', 'RO', "List all UnityEvent persistent listeners in the active scene.", (
    ('path', ostr, None),
))


@tools.provide
async def get_unity_events(
    path: ostr = None,
) -> str:
    ("List all UnityEvent persistent listeners in the active scene."
     "\n    path: optional scene-path prefix filter (e.g. '/UI' to scan only the UI subtree)."
    )
    return await _send('get_unity_events', _args(
        path=rt.w_ostr(path),
    ))


tools.expect('inspect', 'RO', "Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all).", (
    ('paths', ostr, None),
    ('components', ostr, None),
    ('fields', ostr, None),
    ('full', bool, False),
    ('compress', bool, False),
    ('find_type', ostr, None),
))
tools.expect('inspect_uitk', 'RO', "Inspect the VisualElement tree of a UIDocument or PanelRenderer panel", (
    ('path', ostr, None),
    ('depth', oint, None),
    ('selector', ostr, None),
    ('filter', ostr, None),
    ('show_unity_private', obool, None),
    ('show_style', obool, None),
))
tools.expect('invoke_method', 'RW', "[Play Mode] Call public method on a component via reflection.", (
    ('path', str),
    ('component', str),
    ('method', str),
    ('args', str, ""),
))


@tools.provide
async def invoke_method(
    path: str,
    component: str,
    method: str,
    args: str = "",
) -> str:
    ("[Play Mode] Call public method on a component via reflection."
     "\n    args: comma-separated values matching method parameters."
     "\n    Example: invoke_method('/Player', 'PlayerController', 'MoveTo', '10,0,5')"
    )
    return await _send('invoke_method', _args(
        path=rt.w_str(path),
        component=rt.w_str(component),
        method=rt.w_str(method),
        args=rt.w_str(args, ""),
    ))


tools.expect('lint_playtest_suite', 'RO', "Read-only preflight check across multiple .playtest files.", (
    ('pattern', ostr, None),
    ('suite_path', ostr, None),
))
tools.expect('lint_scene_refs', 'RO', "Read-only linter for scene references in DSL scripts or batch commands.", (
    ('path', ostr, None),
    ('snippet', ostr, None),
))
tools.expect('lint_ugui', 'RO', "Diagnose uGUI problems: missing EventSystem, Canvas without GraphicRaycaster. Use when clicks miss or UI appears broken.", (
    ('root', ostr, None),
))


@tools.provide
async def lint_ugui(
    root: ostr = None,
) -> str:
    ("Diagnose uGUI problems: missing EventSystem, Canvas without GraphicRaycaster. Use when clicks miss or UI appears broken."
     "\n    Returns compact text: 'ok: 0 issues' or newline-separated warnings."
     "\n    root: scene path to root GameObject to scan (default: scan all loaded scenes)."
     "\n    "
    )
    return await _send('lint_ugui', _args(
        root=rt.w_ostr(root),
    ))


tools.expect('lint_uitk', 'RO', "Validate a UXML or USS file for structural errors and broken references", (
    ('path', ostr, None),
    ('fix', obool, None),
))
tools.expect('list_connections', 'RO', "List Unity connection status.", (
))
tools.expect('list_events', 'RO', "Read persistent listeners on a UnityEvent field. Use after wire_event to verify.", (
    ('path', str),
    ('component', str),
    ('event', str),
))


@tools.provide
async def list_events(
    path: str,
    component: str,
    event: str,
) -> str:
    ("Read persistent listeners on a UnityEvent field. Use after wire_event to verify."
     "\n    Returns listener details: target path, method name, call state, arg type/value."
     "\n    event: serialized field name — same as wire_event 'event' param (e.g. 'onClick')."
    )
    return await _send('list_events', _args(
        path=rt.w_str(path),
        component=rt.w_str(component),
        event=rt.w_str(event),
    ))


tools.expect('list_skills', 'RO', "List all saved skills with descriptions and usage counts.", (
))
tools.expect('list_templates', 'RO', "List available scene templates in .claude/templates/.", (
))
tools.expect('list_test_runs', 'RO', "List recent durable test runs as JSON, newest first.", (
    ('limit', int, 20),
))
tools.expect('load_session', 'RO', "Load previous session context beside the current hierarchy.", (
))
tools.expect('manage_component', 'RW', "Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button').", (
    ('path', str),
    ('type', str),
    ('action', str),
))


@tools.provide
async def manage_component(
    path: str,
    type: str,
    action: str,
) -> str:
    ("Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button')."
    )
    return await _send('manage_component', _args(
        path=rt.w_str(path),
        type=rt.w_str(type),
        action=rt.w_str(action),
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


@tools.provide
async def material(
    action: str,
    path: ostr = None,
    object_path: ostr = None,
    shader: ostr = None,
    prop: ostr = None,
    value: ostr = None,
    source: ostr = None,
    targets: ostr = None,
    slot: oint = None,
    filter: ostr = None,
    target: ostr = None,
) -> str:
    ("Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path. get_errors: path (shader asset). list_shaders: filter (optional name filter). set_fields: path+value (newline-separated prop=val). set target: shared|instance|asset (default shared)."
    )
    return await _send('material', _args(
        action=rt.w_str(action),
        path=rt.w_ostr(path),
        object_path=rt.w_ostr(object_path),
        shader=rt.w_ostr(shader),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        source=rt.w_ostr(source),
        targets=rt.w_ostr(targets),
        slot=rt.w_oint(slot, raw=True),
        filter=rt.w_ostr(filter),
        target=rt.w_ostr(target),
    ))


tools.expect('material_audit', 'RO', "Material/texture scene-wide audit.", (
    ('action', str, "summary"),
    ('platform', ostr, None),
))


@tools.provide
async def material_audit(
    action: str = "summary",
    platform: ostr = None,
) -> str:
    ("Material/texture scene-wide audit."
     "\n    action: summary|materials|textures|duplicates|compression|recommendations."
     "\n    platform: Android|iOS|Standalone|Default (for compression check)."
    )
    return await _send('material_audit', _args(
        action=rt.w_str(action),
        platform=rt.w_ostr(platform),
    ))


tools.expect('mcp_status', 'RO', "Compact MCP status: scene, dirty, play/compile state, port, alias count,", (
))
tools.expect('menu', 'RW', "Execute or list Unity Editor menu items. action: execute|list. execute: run menu item by path. list: show sub-items (omit path for all roots). Note: Edit/ menu items not supported by Unity API.", (
    ('action', str),
    ('path', ostr, None),
))


@tools.provide
async def menu(
    action: str,
    path: ostr = None,
) -> str:
    ("Execute or list Unity Editor menu items. action: execute|list. execute: run menu item by path. list: show sub-items (omit path for all roots). Note: Edit/ menu items not supported by Unity API."
    )
    return await _send('menu', _args(
        action=rt.w_str(action),
        path=rt.w_ostr(path),
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


@tools.provide
async def object_diff(
    path_a: str,
    path_b: str,
) -> str:
    ("Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'."
    )
    return await _send('object_diff', _args(
        path_a=rt.w_str(path_a),
        path_b=rt.w_str(path_b),
    ))


tools.expect('package', 'RW', "Package manager. Adds or removes packages. No confirmation required. action: list|search|add|remove.", (
    ('action', str),
    ('name', ostr, None),
    ('version', ostr, None),
    ('query', ostr, None),
))
tools.expect('particle', 'RW', "Particle System. action: get|create|set|apply|play|stop|pause. module=main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime. preset: fire|smoke|sparks|rain|snow|explosion|magic|dust|blood|trail.", (
    ('action', str),
    ('path', str),
    ('name', ostr, None),
    ('module', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('preset', ostr, None),
))


@tools.provide
async def particle(
    action: str,
    path: str,
    name: ostr = None,
    module: ostr = None,
    prop: ostr = None,
    value: ostr = None,
    preset: ostr = None,
) -> str:
    ("Particle System. action: get|create|set|apply|play|stop|pause. module=main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime. preset: fire|smoke|sparks|rain|snow|explosion|magic|dust|blood|trail."
    )
    return await _send('particle', _args(
        action=rt.w_str(action),
        path=rt.w_str(path),
        name=rt.w_ostr(name),
        module=rt.w_ostr(module),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        preset=rt.w_ostr(preset),
    ))


tools.expect('permission_prompt', 'RO', "Handle Claude permission prompts via MCP.", (
    ('tool_name', str),
    ('input', Any),
    ('tool_use_id', str),
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


@tools.provide
async def prefab(
    action: str,
    path: ostr = None,
    asset_path: ostr = None,
    base_path: ostr = None,
    variant_path: ostr = None,
    component: ostr = None,
    prop: ostr = None,
    value: ostr = None,
    add_component: ostr = None,
    remove_component: ostr = None,
    recursive: bool = False,
    mode: ostr = None,
    scope: ostr = None,
    format: ostr = None,
) -> str:
    ("Prefab. Creates or modifies prefab assets. No confirmation required. action: save|create_variant|apply|revert|get_overrides|unpack|edit|instantiate."
     "\n    edit: asset_path + component + prop + value (set property on prefab asset)."
     "\n    edit: asset_path + add_component or remove_component (manage components)."
     "\n    save: path (scene) + asset_path [+ mode: new|overwrite (default)]."
     "\n    revert: scope: object (default)|children."
     "\n    get_overrides: format: text (default)|structured."
     "\n    create_variant: base_path + variant_path."
     "\n    instantiate: asset_path (instantiate prefab into active scene)."
    )
    return await _send('prefab', _args(
        action=rt.w_str(action),
        path=rt.w_ostr(path),
        asset_path=rt.w_ostr(asset_path),
        base_path=rt.w_ostr(base_path),
        variant_path=rt.w_ostr(variant_path),
        component=rt.w_ostr(component),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        add_component=rt.w_ostr(add_component),
        remove_component=rt.w_ostr(remove_component),
        recursive=rt.w_bool(recursive, False),
        mode=rt.w_ostr(mode),
        scope=rt.w_ostr(scope),
        format=rt.w_ostr(format),
    ))


tools.expect('profile', 'RW', "Profile CPU/GPU/memory over time.", (
    ('action', str),
    ('duration', float, 5.0),
    ('session', str, ""),
    ('compare_with', str, ""),
    ('focus', str, ""),
    ('mode', str, "burst"),
    ('threshold_ms', float, 33.3),
))
tools.expect('project_settings', 'RW_IDEM', "Project settings. Modifies project settings when action=set. No confirmation required. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input.", (
    ('action', str),
    ('target', str),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('index', oint, None),
    ('build_target', ostr, None),
))


@tools.provide
async def project_settings(
    action: str,
    target: str,
    prop: ostr = None,
    value: ostr = None,
    index: oint = None,
    build_target: ostr = None,
) -> str:
    ("Project settings. Modifies project settings when action=set. No confirmation required. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input."
     "\n    tags set: prop=remove value=<tag> to remove; else adds."
     "\n    quality set prop=currentLevel: calls SetQualityLevel()."
     "\n    player set prop=ScriptingBackend: needs build_target (Standalone|iOS|Android|etc) + value (Mono2x|IL2CPP)."
    )
    return await _send('project_settings', _args(
        action=rt.w_str(action),
        target=rt.w_str(target),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        index=rt.w_oint(index, raw=True),
        build_target=rt.w_ostr(build_target),
    ))


tools.expect('query_state', 'RO', "[Play Mode] Snapshot multiple game values in one call.", (
    ('queries', str),
))
tools.expect('recompile', 'RW', "Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done.", (
))
tools.expect('references', 'RW', "References. action: get|find_to|remap. get: outgoing refs. find_to: reverse search. remap: remap refs.", (
    ('action', str),
    ('path', str),
    ('children', bool, False),
    ('depth', int, 1),
    ('source', ostr, None),
    ('target', ostr, None),
    ('mappings', ostr, None),
))


@tools.provide
async def references(
    action: str,
    path: str,
    children: bool = False,
    depth: int = 1,
    source: ostr = None,
    target: ostr = None,
    mappings: ostr = None,
) -> str:
    ("References. action: get|find_to|remap. get: outgoing refs. find_to: reverse search. remap: remap refs."
    )
    return await _send('references', _args(
        action=rt.w_str(action),
        path=rt.w_str(path),
        children=rt.w_bool(children, False),
        depth=rt.w_int(depth, 1, raw=True),
        source=rt.w_ostr(source),
        target=rt.w_ostr(target),
        mappings=rt.w_ostr(mappings),
    ))


tools.expect('region_clear', 'RW', "Delete (or preview) all objects whose XZ pivot is inside the polygon region. Deletes scene objects when dry_run=False. No confirmation required.", (
    ('vertices', str),
    ('dry_run', bool, True),
    ('filter', ostr, None),
    ('cap', int, 50),
))
tools.expect('release_smoke', 'RO', "Run release readiness checks: status, aliases, compile. Returns PASS/FAIL summary.", (
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


@tools.provide
async def render_analyze(
    action: str,
    path: ostr = None,
    detail: str = "brief",
    baseline_id: ostr = None,
    max_events: oint = None,
) -> str:
    ("Rendering analysis."
     "\n    action: stats|materials|shaders|lights|batching|overdraw|audit|compare"
     "\n            |frame_debug|shadow_audit|probe_audit|light_optimize"
     "\n    stats: draw calls, batches, tris, verts, set-pass from UnityStats."
     "\n    batching: SRP Batcher / static / dynamic / GPU instancing analysis."
     "\n    audit: full rendering health check (all sections, brief)."
     "\n    compare: diff against last baseline snapshot."
     "\n    frame_debug: per-draw-call data via FrameDebugger reflection (pauses rendering briefly)."
     "\n    detail: brief (default) | full.  path: optional subtree root."
    )
    return await _send('render_analyze', _args(
        action=rt.w_str(action),
        path=rt.w_ostr(path),
        detail=rt.w_str(detail),
        baseline_id=rt.w_ostr(baseline_id),
        max_events=rt.w_oint(max_events, raw=True),
    ))


tools.expect('resolve_scene_refs', 'RO', "Read-only scene reference resolver.", (
    ('refs', str),
    ('fields', ostr, None),
))
tools.expect('resolve_test_request', 'RO', "Resolve a possibly lost start ACK without dispatching another test run.", (
    ('request_id', str),
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
tools.expect('run_tests', 'RW_IDEM', "Dispatch Unity tests and return their durable identity immediately.", (
    ('mode', str, "EditMode"),
    ('filter', ostr, None),
    ('request_id', ostr, None),
))
tools.expect('run_tests_wait', 'RW', "Dispatch tests and wait for the exact run to become terminal. Dispatches test run. No confirmation required.", (
    ('mode', str, "EditMode"),
    ('filter', str, ""),
    ('timeout', float, 900.0),
    ('poll_interval', float, 5.0),
    ('request_id', ostr, None),
))
tools.expect('runtime_snapshot', 'RO', "Snapshot all runtime objects of a given component type. Returns per-object field dump.", (
    ('type', str),
    ('name', ostr, None),
    ('component', ostr, None),
    ('compress', bool, False),
))


@tools.provide
async def runtime_snapshot(
    type: str,
    name: ostr = None,
    component: ostr = None,
    compress: bool = False,
) -> str:
    ("Snapshot all runtime objects of a given component type. Returns per-object field dump."
     "\n    type: component type name (e.g. 'Rigidbody', 'EnemyController')."
     "\n    name: optional name substring filter."
     "\n    component: component type to serialize (defaults to type)."
     "\n    compress: strip default-value fields to reduce response size."
    )
    return await _send('runtime_snapshot', _args(
        type=rt.w_str(type),
        name=rt.w_ostr(name),
        component=rt.w_ostr(component),
        compress=rt.w_bool(compress, False),
    ))


tools.expect('save_session', 'RW', "Save current scene state to .claude/session-context.json for cold-start recovery.", (
))
tools.expect('save_skill', 'RW', "Save a learned skill (C# code or batch commands) for reuse across sessions.", (
    ('name', str),
    ('description', str),
    ('code', str),
))
tools.expect('save_template', 'RW_IDEM', "Save C# code as a reusable scene template in .claude/templates/.", (
    ('name', str),
    ('code', str),
))
tools.expect('scan_scene', 'RO', "Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats.", (
))


@tools.provide
async def scan_scene(
) -> str:
    ("Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats."
    )
    return await _send('scan_scene', _args(
    ))


tools.expect('scene', 'DEL', "Scene management. action: new|open|save|discard|open_additive|close|set_active|list|save_copy.", (
    ('action', str),
    ('path', ostr, None),
    ('scene', ostr, None),
    ('include_unsaved', bool, True),
))
tools.expect('scene_change_plan', 'RW', "Pre-flight + plan for safe scene edit.", (
    ('goal', str),
    ('targets', str, ""),
    ('dry_run', bool, False),
))
tools.expect('scene_diff', 'RO', "Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines.", (
))


@tools.provide
async def scene_diff(
) -> str:
    ("Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines."
    )
    return await _send('scene_diff', _args(
    ))


tools.expect('scene_environment', 'RW', "Read/write scene environment: ambient light, fog, skybox, reflections.", (
    ('action', str, "get"),
    ('prop', ostr, None),
    ('value', ostr, None),
))


@tools.provide
async def scene_environment(
    action: str = "get",
    prop: ostr = None,
    value: ostr = None,
) -> str:
    ("Read/write scene environment: ambient light, fog, skybox, reflections."
     "\n    action: get|set. set requires prop and value."
     "\n    Props: ambientMode, ambientLight, ambientIntensity, ambientSkyColor, ambientEquatorColor,"
     "\n    ambientGroundColor, fog, fogColor, fogMode, fogDensity, fogStartDistance, fogEndDistance,"
     "\n    reflectionIntensity, reflectionBounces, subtractiveShadowColor, defaultReflectionResolution."
    )
    return await _send('scene_environment', _args(
        action=rt.w_str(action),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
    ))


tools.expect('scene_health', 'RO', "Scene hierarchy/health audit.", (
    ('focus', str, "all"),
))


@tools.provide
async def scene_health(
    focus: str = "all",
) -> str:
    ("Scene hierarchy/health audit."
     "\n    focus: all | hierarchy | naming | duplicates | origins | missing | empty | disabled"
     "\n    Returns severity-tagged findings: CRITICAL/WARNING/INFO/OK per check."
    )
    return await _send('scene_health', _args(
        focus=rt.w_str(focus),
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


@tools.provide
async def scriptable_object(
    action: str,
    path: ostr = None,
    type: ostr = None,
    prop: ostr = None,
    value: ostr = None,
    fields: ostr = None,
    filter: ostr = None,
) -> str:
    ("ScriptableObject. action: create|get|set|list_types|find. create: type+path[+fields]. get/set: path. set/create fields: \\n-separated prop=value pairs. get fields: comma-sep filter. find: type. list_types: filter."
    )
    return await _send('scriptable_object', _args(
        action=rt.w_str(action),
        path=rt.w_ostr(path),
        type=rt.w_ostr(type),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        fields=rt.w_ostr(fields),
        filter=rt.w_ostr(filter),
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


@tools.provide
async def serialized_field_rename_audit(
    type: str,
    old_field: str,
    new_field: str,
    include: str = "prefabs,scenes,scriptable_objects",
) -> str:
    ("Audit [SerializeField] rename safety."
     "\n    type: fully-qualified or simple component type name (e.g. 'MyNamespace.PlayerStats')."
     "\n    old_field: field name as it exists in serialized assets."
     "\n    new_field: renamed field name in current C# source."
     "\n    include: comma-separated scan targets (prefabs,scenes,scriptable_objects)."
     "\n    Returns: has_formerly_serialized_as, stale_assets, safe_to_remove_attribute, recommended_actions."
    )
    return await _send('serialized_field_rename_audit', _args(
        type=rt.w_str(type),
        old_field=rt.w_str(old_field),
        new_field=rt.w_str(new_field),
        include=rt.w_str(include, "prefabs,scenes,scriptable_objects"),
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


@tools.provide
async def set_material(
    path: str,
    color: str,
    shader: ostr = None,
) -> str:
    ("Set scene object material color (for full asset management use `material`). color: hex (#FF0000). shader: URP/Standard auto."
    )
    return await _send('set_material', _args(
        path=rt.w_str(path),
        color=rt.w_str(color),
        shader=rt.w_ostr(shader),
    ))


tools.expect('set_parent', 'RW_IDEM', "Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent.", (
    ('path', str),
    ('parent', ostr, None),
    ('world_position_stays', bool, True),
))


@tools.provide
async def set_parent(
    path: str,
    parent: ostr = None,
    world_position_stays: bool = True,
) -> str:
    ("Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent."
    )
    return await _send('set_parent', _args(
        path=rt.w_str(path),
        parent=rt.w_ostr(parent),
        world_position_stays=rt.w_bool(world_position_stays, True),
    ))


tools.expect('set_properties', 'RW', "Set multiple properties on ONE object. For multiple objects, use configure_objects instead.", (
    ('path', str),
    ('props', str),
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


@tools.provide
async def set_property_delta(
    path: str,
    component: str,
    prop: str,
    delta: str,
) -> str:
    ("Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new."
    )
    return await _send('set_property_delta', _args(
        path=rt.w_str(path),
        component=rt.w_str(component),
        prop=rt.w_str(prop),
        delta=rt.w_str(delta),
    ))


tools.expect('set_rect', 'RW_IDEM', "Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bottom-right|etc. pos/size: (x,y). pos3: (x,y,z) sets anchoredPosition3D — use for WorldSpace canvases (wins over pos if both given).", (
    ('path', str),
    ('anchor', ostr, None),
    ('pos', ostr, None),
    ('size', ostr, None),
    ('pivot', ostr, None),
    ('offset_min', ostr, None),
    ('offset_max', ostr, None),
    ('pos3', ostr, None),
))


@tools.provide
async def set_rect(
    path: str,
    anchor: ostr = None,
    pos: ostr = None,
    size: ostr = None,
    pivot: ostr = None,
    offset_min: ostr = None,
    offset_max: ostr = None,
    pos3: ostr = None,
) -> str:
    ("Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bottom-right|etc. pos/size: (x,y). pos3: (x,y,z) sets anchoredPosition3D — use for WorldSpace canvases (wins over pos if both given)."
    )
    return await _send('set_rect', _args(
        path=rt.w_str(path),
        anchor=rt.w_ostr(anchor),
        pos=rt.w_ostr(pos),
        size=rt.w_ostr(size),
        pivot=rt.w_ostr(pivot),
        offset_min=rt.w_ostr(offset_min),
        offset_max=rt.w_ostr(offset_max),
        pos3=rt.w_ostr(pos3),
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


tools.expect('setup_objects', 'RW', "Create+configure multiple objects in one call.", (
    ('specs', str),
))
tools.expect('shader', 'RW', "Read or write shader assets (.shader / .shadergraph). Creates or modifies shader assets. No confirmation required. Use when you need to inspect shader properties, create a new shader from a preset or raw HLSL, change a shader property/keyword, or build/edit a Shader Graph node network.", (
    ('action', str),
    ('path', str),
    ('target', ostr, None),
    ('preset', ostr, None),
    ('code', ostr, None),
    ('shader_name', ostr, None),
    ('prop', ostr, None),
    ('value', ostr, None),
    ('keyword', ostr, None),
    ('enabled', ostr, None),
    ('node_type', ostr, None),
    ('node_id', ostr, None),
    ('node_action', ostr, None),
    ('output_node', ostr, None),
    ('output_slot', oint, None),
    ('input_node', ostr, None),
    ('input_slot', oint, None),
    ('edge_action', ostr, None),
    ('name', ostr, None),
    ('type', ostr, None),
    ('default_value', ostr, None),
    ('reference_name', ostr, None),
    ('new_name', ostr, None),
    ('layout', ostr, None),
    ('h_gap', ofloat, None),
    ('v_gap', ofloat, None),
))


@tools.provide
async def shader(
    action: str,
    path: str,
    target: ostr = None,
    preset: ostr = None,
    code: ostr = None,
    shader_name: ostr = None,
    prop: ostr = None,
    value: ostr = None,
    keyword: ostr = None,
    enabled: ostr = None,
    node_type: ostr = None,
    node_id: ostr = None,
    node_action: ostr = None,
    output_node: ostr = None,
    output_slot: oint = None,
    input_node: ostr = None,
    input_slot: oint = None,
    edge_action: ostr = None,
    name: ostr = None,
    type: ostr = None,
    default_value: ostr = None,
    reference_name: ostr = None,
    new_name: ostr = None,
    layout: ostr = None,
    h_gap: ofloat = None,
    v_gap: ofloat = None,
) -> str:
    ("Read or write shader assets (.shader / .shadergraph). Creates or modifies shader assets. No confirmation required. Use when you need to inspect shader properties, create a new shader from a preset or raw HLSL, change a shader property/keyword, or build/edit a Shader Graph node network."
     "\n    action: get (inspect path — shader name, properties, keywords) | create (new shader; preset=unlit|lit|transparent or code=HLSL string) | set (change prop+value or keyword+enabled on existing shader) | graph_get (read Shader Graph nodes/edges) | graph_create (new .shadergraph) | graph_node (add/remove/configure a node; node_type, node_id, node_action) | graph_edge (connect/disconnect slots; output_node/output_slot, input_node/input_slot, edge_action) | graph_get_layout (read node positions as compact text) | graph_set_layout (apply positions from layout text; layout=[id] x,y WxH lines) | graph_auto_layout (auto-arrange nodes by data-flow; h_gap, v_gap optional)."
     "\n    For material shader assignment use `material` tool instead."
    )
    return await _send('shader', _args(
        action=rt.w_str(action),
        path=rt.w_str(path),
        target=rt.w_ostr(target),
        preset=rt.w_ostr(preset),
        code=rt.w_ostr(code),
        shader_name=rt.w_ostr(shader_name),
        prop=rt.w_ostr(prop),
        value=rt.w_ostr(value),
        keyword=rt.w_ostr(keyword),
        enabled=rt.w_ostr(enabled),
        node_type=rt.w_ostr(node_type),
        node_id=rt.w_ostr(node_id),
        node_action=rt.w_ostr(node_action),
        output_node=rt.w_ostr(output_node),
        output_slot=rt.w_oint(output_slot, raw=True),
        input_node=rt.w_ostr(input_node),
        input_slot=rt.w_oint(input_slot, raw=True),
        edge_action=rt.w_ostr(edge_action),
        name=rt.w_ostr(name),
        type=rt.w_ostr(type),
        default_value=rt.w_ostr(default_value),
        reference_name=rt.w_ostr(reference_name),
        new_name=rt.w_ostr(new_name),
        layout=rt.w_ostr(layout),
        h_gap=rt.w_ofloat(h_gap, raw=True),
        v_gap=rt.w_ofloat(v_gap, raw=True),
    ))


tools.expect('smart_build', 'RW', "Build scene objects from natural language description using MCP sampling + execute_code.", (
    ('description', str),
    ('ctx', Any),
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


@tools.provide
async def sync_playtest_aliases_from_defs(
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
    asset: str = "Assets/Configs/PlaytestConfig.asset",
) -> str:
    ("Overwrite PlaytestConfig.asset aliases from a .defs text file."
     "\n    defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs)."
     "\n    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset)."
     "\n    Invalidates AliasExpander cache after sync. Not allowed in Play Mode."
    )
    return await _send('sync_playtest_aliases_from_defs', _args(
        defs=rt.w_str(defs),
        asset=rt.w_str(asset),
    ))


tools.expect('test_step', 'RW', "[Play Mode] Move character, snapshot state before/after, check console.", (
    ('path', str),
    ('position', str),
    ('checks_before', str, ""),
    ('checks_after', str, ""),
    ('wait_after', float, 0.5),
    ('timeout', float, 15.0),
))
tools.expect('timeline', 'RW', "Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinematic sequences mixing animation, audio, activation, and custom tracks — not for per-object keyframes (use `animation` for that).", (
    ('path', str),
    ('action', str),
    ('track', ostr, None),
    ('track_type', ostr, None),
    ('clip', ostr, None),
    ('binding', ostr, None),
    ('start', ofloat, None),
    ('duration', ofloat, None),
    ('blend_in', ofloat, None),
    ('blend_out', ofloat, None),
    ('asset_path', ostr, None),
    ('director_path', ostr, None),
    ('tracks', ostr, None),
    ('time', ofloat, None),
    ('name', ostr, None),
    ('clip_in', ofloat, None),
    ('index', oint, None),
    ('offset', ofloat, None),
    ('value', ostr, None),
))


@tools.provide
async def timeline(
    path: str,
    action: str,
    track: ostr = None,
    track_type: ostr = None,
    clip: ostr = None,
    binding: ostr = None,
    start: ofloat = None,
    duration: ofloat = None,
    blend_in: ofloat = None,
    blend_out: ofloat = None,
    asset_path: ostr = None,
    director_path: ostr = None,
    tracks: ostr = None,
    time: ofloat = None,
    name: ostr = None,
    clip_in: ofloat = None,
    index: oint = None,
    offset: ofloat = None,
    value: ostr = None,
) -> str:
    ("Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinematic sequences mixing animation, audio, activation, and custom tracks — not for per-object keyframes (use `animation` for that)."
     "\n    action: get | create | add_track (Animation|Audio|Activation|Signal|Control|Group) | remove_track | add_clip | remove_clip | set_binding | set_timing | mute | unmute | lock | unlock | rename_track | reorder_track | duplicate_clip | add_marker | remove_marker | set_track_offset | set_duration | add_sub_track | set_clip_in | get_bindings | preview."
     "\n    track=track name. index=target position for reorder_track. offset=time shift for duplicate_clip. value=offset mode (auto|transform|scene) for set_track_offset."
    )
    return await _send('timeline', _args(
        path=rt.w_str(path),
        action=rt.w_str(action),
        track=rt.w_ostr(track),
        track_type=rt.w_ostr(track_type),
        clip=rt.w_ostr(clip),
        binding=rt.w_ostr(binding),
        start=rt.w_ofloat(start, raw=True),
        duration=rt.w_ofloat(duration, raw=True),
        blend_in=rt.w_ofloat(blend_in, raw=True),
        blend_out=rt.w_ofloat(blend_out, raw=True),
        asset_path=rt.w_ostr(asset_path),
        director_path=rt.w_ostr(director_path),
        tracks=rt.w_ostr(tracks),
        time=rt.w_ofloat(time, raw=True),
        name=rt.w_ostr(name),
        clip_in=rt.w_ofloat(clip_in, raw=True),
        index=rt.w_oint(index, raw=True),
        offset=rt.w_ofloat(offset, raw=True),
        value=rt.w_ostr(value),
    ))


tools.expect('transfer_object', 'RW', "Move or copy a GameObject to another loaded scene. action: move|copy.", (
    ('path', str),
    ('action', str),
    ('target_scene', ostr, None),
    ('parent', ostr, None),
    ('world_position_stays', bool, True),
))


@tools.provide
async def transfer_object(
    path: str,
    action: str,
    target_scene: ostr = None,
    parent: ostr = None,
    world_position_stays: bool = True,
) -> str:
    ("Move or copy a GameObject to another loaded scene. action: move|copy."
     "\n    target_scene: destination scene name. Omit = same scene (copy = duplicate)."
     "\n    parent: target parent path in destination scene."
     "\n    world_position_stays: preserve world transform (default True)."
    )
    return await _send('transfer_object', _args(
        path=rt.w_str(path),
        action=rt.w_str(action),
        target_scene=rt.w_ostr(target_scene),
        parent=rt.w_ostr(parent),
        world_position_stays=rt.w_bool(world_position_stays, True),
    ))


tools.expect('ui_intent', 'RW', "Convert NL intent to Unity UI hierarchy. Templates bypass Haiku.", (
    ('intent', str),
    ('parent', ostr, None),
    ('template', ostr, None),
    ('dry_run', bool, False),
))
tools.expect('uitk_element', 'RW', "Mutate or query a VisualElement in a UIDocument or PanelRenderer host", (
    ('action', Any),
    ('path', ostr, None),
    ('ref', ostr, None),
    ('selector', ostr, None),
    ('name', ostr, None),
    ('value', ostr, None),
    ('property', ostr, None),
    ('class_name', ostr, None),
))
tools.expect('uitk_file', 'RW', "Read or edit a UXML or USS asset file.", (
    ('path', str),
    ('action', Any, "read"),
    ('content', ostr, None),
    ('selector', ostr, None),
    ('attr', ostr, None),
    ('value', ostr, None),
    ('cls', ostr, None),
    ('parent', ostr, None),
    ('tag', ostr, None),
    ('attrs', ostr, None),
    ('prop', ostr, None),
))
tools.expect('uitk_intent', 'RW', "Generate a UXML + USS file pair from a natural-language UI description.", (
    ('intent', str),
    ('name', str),
    ('path', str, "Assets/UI"),
    ('attach_to', ostr, None),
    ('template', ostr, None),
    ('dry_run', bool, False),
))
tools.expect('undo_last', 'RW', "Undo the last N AI turns in the Unity Undo stack. Default: 1.", (
    ('turns', int, 1),
))


@tools.provide
async def undo_last(
    turns: int = 1,
) -> str:
    ("Undo the last N AI turns in the Unity Undo stack. Default: 1."
     "\n    warn: file-system operations (asset creation/deletion via asset tool) are"
     "\n    not reversed by undo. Only scene-object and component mutations are undoable."
    )
    return await _send('undo_last', _args(
        turns=rt.w_int(turns, raw=True),
    ))


tools.expect('unwire_event', 'DEL', "Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation required.", (
    ('path', str),
    ('component', str),
    ('event', str),
    ('index', oint, None),
))


@tools.provide
async def unwire_event(
    path: str,
    component: str,
    event: str,
    index: oint = None,
) -> str:
    ("Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation required."
     "\n    index: remove specific entry (0-based). Omit to clear all."
    )
    return await _send('unwire_event', _args(
        path=rt.w_str(path),
        component=rt.w_str(component),
        event=rt.w_str(event),
        index=rt.w_oint(index, raw=True),
    ))


tools.expect('use_skill', 'RW', "Execute a previously saved skill. params: comma-separated key=value for substitution.", (
    ('name', str),
    ('params', ostr, None),
))
tools.expect('validate_playtest_aliases', 'RO', "Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed.", (
    ('defs', str, "Assets/PlaytestDefs/farm_core.defs"),
    ('asset', str, "Assets/Configs/PlaytestConfig.asset"),
))


@tools.provide
async def validate_playtest_aliases(
    defs: str = "Assets/PlaytestDefs/farm_core.defs",
    asset: str = "Assets/Configs/PlaytestConfig.asset",
) -> str:
    ("Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed."
     "\n    defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs)."
     "\n    asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset)."
     "\n    Returns 'ok: N aliases in sync' when identical, or a diff report."
    )
    return await _send('validate_playtest_aliases', _args(
        defs=rt.w_str(defs),
        asset=rt.w_str(asset),
    ))


tools.expect('validate_references', 'RO', "Validate all ObjectReference fields under path recursively.", (
    ('path', str),
    ('depth', int, 3),
    ('verbose', bool, False),
    ('ignore_optional', bool, False),
))


@tools.provide
async def validate_references(
    path: str,
    depth: int = 3,
    verbose: bool = False,
    ignore_optional: bool = False,
) -> str:
    ("Validate all ObjectReference fields under path recursively."
     "\n    Returns [ERROR]/[MISSING] for broken refs. Summary: \"N ERROR, M OK\"."
     "\n    Use depth=1 for quick top-level scan, depth=3-5 for full subtree."
     "\n    verbose=True also shows [OK] lines (off by default to save tokens)."
     "\n    ignore_optional=True skips fields marked [Optional] (reduces noise)."
    )
    return await _send('validate_references', _args(
        path=rt.w_str(path),
        depth=rt.w_int(depth, raw=True),
        verbose=rt.w_bool(verbose, False),
        ignore_optional=rt.w_bool(ignore_optional, False),
    ))


tools.expect('validate_triggers', 'RO', "Check 3D trigger/collider overlaps. Warns if triggers closer than min_distance meters.", (
    ('root', str, "/"),
    ('min_distance', float, 3.0),
))


@tools.provide
async def validate_triggers(
    root: str = "/",
    min_distance: float = 3.0,
) -> str:
    ("Check 3D trigger/collider overlaps. Warns if triggers closer than min_distance meters."
    )
    return await _send('validate_triggers', _args(
        root=rt.w_str(root),
        min_distance=rt.w_float(min_distance),
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
tools.expect('vfx_intent', 'RW', "Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely.", (
    ('target', str),
    ('intent', str),
    ('kind', str, "auto"),
    ('dry_run', bool, False),
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


@tools.provide
async def wire_event(
    path: str,
    component: str,
    event: str,
    target: str,
    method: str,
    arg_type: str = "void",
    arg_value: ostr = None,
    target_component_type: ostr = None,
    parameter_types: ostr = None,
) -> str:
    ("Wire UnityEvent persistent listener. Mutates scene. No confirmation required."
     "\n    path: object with the event. component: type owning the event field."
     "\n    event: serialized field name (e.g. 'onClick', '_onComplete')."
     "\n    target: scene path or asset path. Auto-resolves component owning the method."
     "\n    method: method name (e.g. 'SetActive', 'Play')."
     "\n    arg_type: void|bool|int|float|string|object."
     "\n    arg_value: required when arg_type != void. For object: scene path or asset path."
     "\n    target_component_type: narrow component search to this type (e.g. 'Animator')."
     "\n    parameter_types: comma-separated param types to resolve overloads (e.g. 'string')."
    )
    return await _send('wire_event', _args(
        path=rt.w_str(path),
        component=rt.w_str(component),
        event=rt.w_str(event),
        target=rt.w_str(target),
        method=rt.w_str(method),
        arg_type=rt.w_str(arg_type),
        arg_value=rt.w_ostr(arg_value),
        target_component_type=rt.w_ostr(target_component_type),
        parameter_types=rt.w_ostr(parameter_types),
    ))


