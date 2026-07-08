from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def asset(action: str, path: str | None = None, type: str | None = None,
                name: str | None = None, folder: str | None = None,
                source: str | None = None, dest: str | None = None,
                prop: str | None = None, value: str | None = None,
                recursive: bool = False, labels: str | None = None,
                output: str | None = None, include_deps: bool = True) -> str:
    """Asset database. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial. move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. get_dependencies: forward deps. find_dependents: reverse deps (who references this asset). export_package: path+output[+include_deps=false to skip deps]. import_package: path (filesystem)."""
    return await _send("asset", _args(
        action=action, path=path, type=type, name=name, folder=folder,
        source=source, dest=dest, prop=prop, value=value,
        recursive="true" if recursive else None, labels=labels,
        output=output,
        include_deps="false" if not include_deps else None))


async def project_settings(action: str, target: str, prop: str | None = None,
                           value: str | None = None, index: int | None = None) -> str:
    """Project settings. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player."""
    return await _send("project_settings", _args(
        action=action, target=target, prop=prop, value=value, index=index))


async def material(action: str, path: str | None = None, object_path: str | None = None,
                   shader: str | None = None, prop: str | None = None, value: str | None = None,
                   source: str | None = None, targets: str | None = None,
                   slot: int | None = None) -> str:
    """Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path (lists all material slots)."""
    return await _send("material", _args(
        action=action, path=path, object_path=object_path, shader=shader,
        prop=prop, value=value, source=source, targets=targets, slot=slot))


async def prefab(action: str, path: str | None = None, asset_path: str | None = None,
                 base_path: str | None = None, variant_path: str | None = None,
                 component: str | None = None, prop: str | None = None,
                 value: str | None = None, add_component: str | None = None,
                 remove_component: str | None = None,
                 recursive: bool = False) -> str:
    """Prefab. action: save|create_variant|apply|revert|get_overrides|unpack|edit.
    edit: asset_path + component + prop + value (set property on prefab asset).
    edit: asset_path + add_component or remove_component (manage components).
    save: path (scene) + asset_path. create_variant: base_path + variant_path."""
    return await _send("prefab", _args(
        action=action, path=path, asset_path=asset_path,
        base_path=base_path, variant_path=variant_path,
        component=component, prop=prop, value=value,
        add_component=add_component, remove_component=remove_component,
        recursive="true" if recursive else None))


async def scriptable_object(action: str, path: str | None = None, type: str | None = None,
                            prop: str | None = None, value: str | None = None,
                            fields: str | None = None,
                            filter: str | None = None) -> str:
    """ScriptableObject. action: create|get|set|list_types|find. create: type+path. get/set: path. set: prop+value | fields (\\n-separated prop=value pairs, 1 save). find: type. list_types: filter."""
    return await _send("scriptable_object", _args(
        action=action, path=path, type=type, prop=prop, value=value, fields=fields, filter=filter))


async def get_enabled_tools() -> str:
    """List enabled tool names, comma-separated."""
    return await _send("get_enabled_tools", {})


async def material_audit(
    action: str = "summary",
    platform: str | None = None,
) -> str:
    """Material/texture scene-wide audit.
    action: summary|materials|textures|duplicates|compression|recommendations.
    platform: Android|iOS|Standalone|Default (for compression check)."""
    return await _send("material_audit", _args(action=action, platform=platform))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(asset)
    mcp.tool(annotations=_RW_IDEM)(project_settings)
    mcp.tool(annotations=_RW)(material)
    mcp.tool(annotations=_RW)(prefab)
    mcp.tool(annotations=_RW)(scriptable_object)
    mcp.tool(annotations=_RO)(get_enabled_tools)
    mcp.tool(annotations=_RO)(material_audit)
