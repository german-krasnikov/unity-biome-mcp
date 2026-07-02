from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM, DEL as _DEL
from unity_mcp.input_normalizer import normalize_value as _normalize_value
from unity_mcp.compressor import project_fields as _project_fields
from ._common import bind

_send = None
_args = None


async def get_component(path: str, type: str, fields: str | None = None, full: bool = False) -> str:
    """Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N.
    fields: comma-separated field names to keep (e.g. 'mass,position') — projects the result to save tokens; shows requested fields even at default values. Aliases: position, rotation, scale, mass, enabled, active, name.
    full=True: bypass distillation, return raw response."""
    args: dict = {"path": path, "type": type}
    if full:
        args["_no_distill"] = True
    if fields:
        args["_no_strip"] = True
        result = await _send("get_component", args)
        return _project_fields(result, fields)
    return await _send("get_component", args)


async def inspect(paths: str, components: str | None = None, fields: str | None = None, full: bool = False) -> str:
    """Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all).
    fields: comma-separated field names to keep across all objects — projects the result to save tokens. full=True: bypass distillation."""
    extra = {"_no_distill": True} if full else {}
    if fields:
        result = await _send("inspect", _args(paths=paths, components=components, _no_strip=True, **extra))
        return _project_fields(result, fields)
    return await _send("inspect", _args(paths=paths, components=components, **extra))


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


async def set_property(path: str, component: str, prop: str, value, dry_run: bool = False) -> str:
    """Set component property. ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), #instanceID, or 'null'. dry_run=True shows what would change without applying."""
    args = {"path": path, "component": component, "prop": prop, "value": _normalize_value(value)}
    if dry_run:
        args["dry_run"] = "true"
    return await _send("set_property", args)


async def create_object(
    name: str, parent: str | None = None, components: str | None = None,
    primitive: str | None = None, prefab_path: str | None = None,
    scene: str | None = None,
) -> str:
    """Create new GameObject. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene)."""
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
                     arg_type: str = "void", arg_value: str | None = None) -> str:
    """Wire UnityEvent persistent listener.
    path: object with the event. component: type owning the event field.
    event: serialized field name (e.g. 'onClick', '_onComplete').
    target: scene path or asset path. Auto-resolves component owning the method.
    method: method name (e.g. 'SetActive', 'Play').
    arg_type: void|bool|int|float|string|object.
    arg_value: required when arg_type != void. For object: scene path or asset path."""
    return await _send("wire_event", _args(
        path=path, component=component, event=event,
        target=target, method=method, arg_type=arg_type, arg_value=arg_value))


async def unwire_event(path: str, component: str, event: str, index: int | None = None) -> str:
    """Remove persistent listener(s) from UnityEvent.
    index: remove specific entry (0-based). Omit to clear all."""
    return await _send("unwire_event", _args(
        path=path, component=component, event=event, index=index))


async def delete_object(id: int | None = None, path: str | None = None, force: bool = False) -> str:
    """Delete GameObject by instance ID or scene path. Provide one. force=True to delete non-empty containers."""
    if id is None and not path:
        raise ValueError("delete_object: id or path required")
    args = {}
    if id is not None: args["id"] = id
    if path: args["path"] = path
    if force: args["force"] = "true"
    return await _send("delete_object", args)


async def manage_component(path: str, type: str, action: str) -> str:
    """Add or remove a component. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button')."""
    return await _send("manage_component", {"path": path, "type": type, "action": action})


async def get_object_detail(id: int, full: bool = False) -> str:
    """Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation."""
    args: dict = {"id": id}
    if full:
        args["_no_distill"] = True
    return await _send("get_object_detail", args)


async def set_material(path: str, color: str, shader: str | None = None) -> str:
    """Set object material color. color: hex (#FF0000). shader: URP/Standard auto."""
    return await _send("set_material", _args(path=path, color=color, shader=shader))


async def set_property_delta(path: str, component: str, prop: str, delta: str) -> str:
    """Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new."""
    return await _send("set_property_delta", _args(path=path, component=component, prop=prop, delta=delta))


async def set_parent(path: str, parent: str | None = None, world_position_stays: bool = True) -> str:
    """Reparent existing GameObject. parent=null → move to scene root."""
    args = {"path": path, "world_position_stays": "true" if world_position_stays else "false"}
    if parent is not None:
        args["parent"] = parent
    return await _send("set_parent", args)


async def object_diff(path_a: str, path_b: str) -> str:
    """Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'."""
    return await _send("object_diff", {"pathA": path_a, "pathB": path_b})


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
    mcp.tool(annotations=_RO)(object_diff)
