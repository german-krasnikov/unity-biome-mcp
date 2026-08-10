from unity_mcp.compressor import project_fields as _project_fields
from unity_mcp.input_normalizer import normalize_value as _normalize_value

from ._annotations import DEL as _DEL
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._annotations import RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def get_component(path: str, type: str, fields: str | None = None, full: bool = False, compress: bool = False) -> str:
    """Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N.
    fields: comma-separated field names to keep (e.g. 'mass,position') — projects the result to save tokens; shows requested fields even at default values. Aliases: position, rotation, scale, mass, enabled, active, name.
    full=True: bypass distillation, return raw response. compress=True: strip default values before transfer."""
    args: dict = {"path": path, "type": type}
    if full:
        args["_no_distill"] = True
    if fields:
        args["_no_distill"] = True
        args["_no_strip"] = True
        result = await _send("get_component", args)
        return _project_fields(result, fields)
    if compress:
        args["compress"] = "true"
    return await _send("get_component", args)


async def inspect(paths: str | None = None, components: str | None = None, fields: str | None = None, full: bool = False, compress: bool = False, find_type: str | None = None) -> str:
    """Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all).
    find_type: component type to find — populates paths automatically (replaces explicit paths).
    fields: comma-separated field names to keep across all objects — projects the result to save tokens. full=True: bypass distillation. compress=True: strip default values before transfer."""
    extra = {"_no_distill": True} if full else {}
    if fields:
        extra["_no_distill"] = True
        result = await _send("inspect", _args(paths=paths, find_type=find_type, components=components, _no_strip=True, **extra))
        return _project_fields(result, fields)
    if compress:
        extra["compress"] = "true"
    return await _send("inspect", _args(paths=paths, find_type=find_type, components=components, **extra))


async def get_components_list(id: int) -> str:
    """List all components on object by instance ID."""
    return await _send("get_components_list", {"id": id})


async def find_objects(
    name: str | None = None,
    tag: str | None = None,
    layer: str | None = None,
    component: str | None = None,
) -> str:
    """Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace)."""
    return await _send("find_objects", _args(name=name, tag=tag, layer=layer, component=component))


async def set_property(path: str | None = None, component: str = "", prop: str = "", value=None,
                       dry_run: bool = False, find_type: str | None = None,
                       ref_component_type: str | None = None) -> str:
    """Set component property (Edit Mode, SerializedObject — for Play Mode use `invoke_method` or `execute_code`).
    find_type: component type — bulk-sets prop on all matching objects without specifying paths.
    For GO rename use rename_object(). ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), $hexId (e.g. $3E8) or #instanceID (legacy), or 'null'. dry_run=True shows what would change without applying.
    ref_component_type: when value is a plain scene path and the field expects a specific Component type (e.g. 'BoxCollider'), appends '::TypeName' to the value so C# resolves the correct component. Ignored when value already contains '::'."""
    if value is not None:
        value = _normalize_value(value)
        if ref_component_type and "::" not in str(value):
            value = f"{value}::{ref_component_type}"
    args = _args(path=path, find_type=find_type, component=component or None,
                 prop=prop or None, value=value)
    if dry_run:
        args["dry_run"] = "true"
    return await _send("set_property", args)


async def create_object(
    name: str, parent: str | None = None, components: str | None = None,
    primitive: str | None = None, prefab_path: str | None = None,
    scene: str | None = None,
) -> str:
    """Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene)."""
    return await _send("create_object", _args(name=name, parent=parent, components=components,
                                              primitive=primitive, prefab_path=prefab_path,
                                              scene=scene))


async def transfer_object(
    path: str, action: str,
    target_scene: str | None = None,
    parent: str | None = None,
    world_position_stays: bool = True,
) -> str:
    """Move or copy a GameObject to another loaded scene. action: move|copy.
    target_scene: destination scene name. Omit = same scene (copy = duplicate).
    parent: target parent path in destination scene.
    world_position_stays: preserve world transform (default True)."""
    wps = None if world_position_stays else "false"
    return await _send("transfer_object", _args(
        path=path, action=action, target_scene=target_scene,
        parent=parent, world_position_stays=wps))


async def set_active(path: str, active: bool) -> str:
    """Set GameObject active/inactive."""
    return await _send("set_active", {"path": path, "active": "true" if active else "false"})


async def wire_event(path: str, component: str, event: str, target: str, method: str,
                     arg_type: str = "void", arg_value: str | None = None,
                     target_component_type: str | None = None,
                     parameter_types: str | None = None) -> str:
    """Wire UnityEvent persistent listener. Mutates scene. No confirmation required.
    path: object with the event. component: type owning the event field.
    event: serialized field name (e.g. 'onClick', '_onComplete').
    target: scene path or asset path. Auto-resolves component owning the method.
    method: method name (e.g. 'SetActive', 'Play').
    arg_type: void|bool|int|float|string|object.
    arg_value: required when arg_type != void. For object: scene path or asset path.
    target_component_type: narrow component search to this type (e.g. 'Animator').
    parameter_types: comma-separated param types to resolve overloads (e.g. 'string')."""
    return await _send("wire_event", _args(
        path=path, component=component, event=event,
        target=target, method=method, arg_type=arg_type, arg_value=arg_value,
        target_component_type=target_component_type, parameter_types=parameter_types))


async def unwire_event(path: str, component: str, event: str, index: int | None = None) -> str:
    """Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation required.
    index: remove specific entry (0-based). Omit to clear all."""
    return await _send("unwire_event", _args(
        path=path, component=component, event=event, index=index))


async def delete_object(id: int | None = None, path: str | None = None, force: bool = False) -> str:
    """Delete GameObject by instance ID or scene path. Deletes scene objects. No confirmation required. Provide one. force=True to delete non-empty containers."""
    if id is None and not path:
        raise ValueError("delete_object: id or path required")
    args = {}
    if id is not None: args["id"] = id
    if path: args["path"] = path
    if force: args["force"] = "true"
    return await _send("delete_object", args)


async def manage_component(path: str, type: str, action: str) -> str:
    """Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button')."""
    return await _send("manage_component", {"path": path, "type": type, "action": action})


async def get_object_detail(id: int, full: bool = False) -> str:
    """Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation."""
    args: dict = {"id": id}
    if full:
        args["_no_distill"] = True
    return await _send("get_object_detail", args)


async def set_material(path: str, color: str, shader: str | None = None) -> str:
    """Set scene object material color (for full asset management use `material`). color: hex (#FF0000). shader: URP/Standard auto."""
    return await _send("set_material", _args(path=path, color=color, shader=shader))


async def set_property_delta(path: str, component: str, prop: str, delta: str) -> str:
    """Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new."""
    return await _send("set_property_delta", _args(path=path, component=component, prop=prop, delta=delta))


async def set_parent(path: str, parent: str | None = None, world_position_stays: bool = True) -> str:
    """Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent."""
    args: dict = {"path": path}
    if not world_position_stays:
        args["world_position_stays"] = "false"
    if parent is not None:
        args["parent"] = parent
    return await _send("set_parent", args)


async def rename_object(path: str, name: str) -> str:
    """Rename a GameObject. Returns new scene path after rename.
    path: current scene path, $hexId (e.g. $3E8) or #instanceID (legacy). name: new name (non-empty).
    Note: all subsequent MCP calls must use the new path."""
    return await _send("rename_object", {"path": path, "name": name})


async def set_sibling_index(path: str, index: int) -> str:
    """Set sibling index of a GameObject within its parent. index=0 moves to first child."""
    return await _send("set_sibling_index", {"path": path, "index": str(index)})


async def object_diff(path_a: str, path_b: str) -> str:
    """Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'."""
    return await _send("object_diff", {"path_a": path_a, "path_b": path_b})


async def get_unity_events(path: str | None = None) -> str:
    """List all UnityEvent persistent listeners in the active scene.
    path: optional scene-path prefix filter (e.g. '/UI' to scan only the UI subtree)."""
    return await _send("get_unity_events", _args(path=path))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_component)
    mcp.tool(annotations=_RO)(inspect)
    mcp.tool(annotations=_RO)(get_components_list)
    mcp.tool(annotations=_RO)(find_objects)
    mcp.tool(annotations=_RW_IDEM)(set_property)
    mcp.tool(annotations=_RW)(create_object)
    mcp.tool(annotations=_RW)(transfer_object)
    mcp.tool(annotations=_RW_IDEM)(set_active)
    mcp.tool(annotations=_RW)(wire_event)
    mcp.tool(annotations=_DEL)(unwire_event)
    mcp.tool(annotations=_DEL)(delete_object)
    mcp.tool(annotations=_RW)(manage_component)
    mcp.tool(annotations=_RO)(get_object_detail)
    mcp.tool(annotations=_RW_IDEM)(set_material)
    mcp.tool(annotations=_RW)(set_property_delta)
    mcp.tool(annotations=_RW_IDEM)(set_parent)
    mcp.tool(annotations=_RW_IDEM)(rename_object)
    mcp.tool(annotations=_RW)(set_sibling_index)
    mcp.tool(annotations=_RO)(object_diff)
    mcp.tool(annotations=_RO)(get_unity_events)
