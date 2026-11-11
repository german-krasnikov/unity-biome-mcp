from unity_mcp.compressor import collapse_empty_sections as _collapse_empty_sections
from unity_mcp.compressor import project_fields as _project_fields
from unity_mcp.input_normalizer import normalize_value as _normalize_value

from ._common import bind

# The tool catalog (dsl/tools.biome) generates the wrappers named here (server.py and the tests import them from this module);
# `tools` registers the others.
from .generated.catalog_tools import (  # noqa: F401
    create_object,
    delete_object,
    find_objects,
    get_component,
    get_components_list,
    get_object_detail,
    get_unity_events,
    inspect,
    list_events,
    manage_component,
    object_diff,
    rename_object,
    set_active,
    set_material,
    set_parent,
    set_property,
    set_property_delta,
    set_sibling_index,
    tools,
    transfer_object,
    unwire_event,
    wire_event,
)

_send = None
_args = None


async def _get_component_body(path: str, type: str = "", fields: str | None = None, full: bool = False, compress: bool = False,
                        component: str | None = None) -> str:
    """Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N.
    fields: comma-separated field names to keep (e.g. 'mass,position') — projects the result to save tokens; shows requested fields even at default values. Aliases: position, rotation, scale, mass, enabled, active, name.
    full=True: bypass distillation, return raw response. compress=True: strip default values before transfer.
    component: alias for type= (backward-compat with set_property naming). type= wins when both provided."""
    effective_type = type or component or ""
    if not effective_type:
        return "error: type= or component= required"
    args: dict = {"path": path, "type": effective_type}
    if full:
        args["_no_distill"] = True
    if fields:
        args["_no_distill"] = True
        args["_no_strip"] = True
        result = await _send("get_component", args)
        return _project_fields(result, fields)
    if compress:
        args["compress"] = "true"
        result = await _send("get_component", args)
        return _collapse_empty_sections(result)
    return await _send("get_component", args)


async def _inspect_body(paths: str | None = None, components: str | None = None, fields: str | None = None, full: bool = False, compress: bool = False, find_type: str | None = None) -> str:
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
        result = await _send("inspect", _args(paths=paths, find_type=find_type, components=components, **extra))
        return _collapse_empty_sections(result)
    return await _send("inspect", _args(paths=paths, find_type=find_type, components=components, **extra))


async def _set_property_body(path: str | None = None, component: str = "", prop: str = "", value=None,
                       dry_run: bool = False, find_type: str | None = None,
                       ref_component_type: str | None = None) -> str:
    """Set component property (Edit Mode, SerializedObject — for Play Mode use `invoke_method` or `execute_code`).
    find_type: component type — bulk-sets prop on all matching objects without specifying paths.
    For GO rename use rename_object(). ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy), or 'null'. dry_run=True shows what would change without applying.
    ref_component_type: when value is a plain scene path and the field expects a specific Component type (e.g. 'BoxCollider'), appends '::TypeName' to the value so C# resolves the correct component. Ignored when value already contains '::'.
    effects: mutates scene via SerializedObject, creates an undo entry. Verify with get_component after."""
    if value is not None:
        value = _normalize_value(value)
        if ref_component_type and "::" not in str(value):
            value = f"{value}::{ref_component_type}"
    args = _args(path=path, find_type=find_type, component=component or None,
                 prop=prop or None, value=value)
    if dry_run:
        args["dry_run"] = "true"
    return await _send("set_property", args)


async def _delete_object_body(id: int | None = None, path: str | None = None, force: bool = False) -> str:
    """Delete GameObject by instance ID or scene path. Deletes scene objects. No confirmation required. Provide one. force=True to delete non-empty containers."""
    if id is None and not path:
        raise ValueError("delete_object: id or path required")
    args = {}
    if id is not None: args["id"] = id
    if path: args["path"] = path
    if force: args["force"] = "true"
    return await _send("delete_object", args)


async def _get_object_detail_body(id: int, full: bool = False) -> str:
    """Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation."""
    args: dict = {"id": id}
    if full:
        args["_no_distill"] = True
    return await _send("get_object_detail", args)


def register(mcp, send, args):
    bind(globals(), send, args)
