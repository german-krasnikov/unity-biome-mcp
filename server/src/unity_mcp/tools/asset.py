from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._annotations import RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def _write_text_with_capture(path: str, content: str) -> str:
    from ..changeset_coordinator import get_coordinator
    from ..changeset_file_capture import snapshot_file
    from ..changeset_store import get_store

    store = get_store()
    coord = get_coordinator()

    before_ref = snapshot_file(path, store) if store else None
    result = await _send("asset", _args(action="write_text", path=path, content=content))
    after_ref = snapshot_file(path, store) if store else None

    if coord and after_ref != before_ref:
        coord.append_file_op("asset.write_text", path, before_ref, after_ref)

    return result


async def asset(action: str, path: str | None = None, type: str | None = None,
                name: str | None = None, folder: str | None = None,
                source: str | None = None, dest: str | None = None,
                prop: str | None = None, value: str | None = None,
                recursive: bool = False, labels: str | None = None,
                output: str | None = None, include_deps: bool = True,
                content: str | None = None,
                class_name: str | None = None,
                path_only: bool = False) -> str:
    """Asset database. Creates, moves, or deletes assets. No confirmation required. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package|read_text|write_text|reimport. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial|AnimatorController|ScriptableObject (class= required for SO). move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. validate_move path_only=True: syntax check only, skips AssetDatabase folder existence check (preflight). get_dependencies: forward deps. find_dependents: reverse deps (who references this asset). export_package: path+output[+include_deps=false to skip deps]. import_package: path (filesystem). read_text: path. write_text: path+content. reimport: path."""
    if action == "write_text" and path is not None and content is not None:
        return await _write_text_with_capture(path, content)
    return await _send("asset", _args(
        action=action, path=path, type=type, name=name, folder=folder,
        source=source, dest=dest, prop=prop, value=value,
        recursive="true" if recursive else None, labels=labels,
        output=output,
        include_deps="false" if not include_deps else None,
        content=content,
        path_only="true" if path_only else None,
        **{"class": class_name} if class_name is not None else {}))


async def project_settings(action: str, target: str, prop: str | None = None,
                           value: str | None = None, index: int | None = None,
                           build_target: str | None = None) -> str:
    """Project settings. Modifies project settings when action=set. No confirmation required. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input.
    tags set: prop=remove value=<tag> to remove; else adds.
    quality set prop=currentLevel: calls SetQualityLevel().
    player set prop=ScriptingBackend: needs build_target (Standalone|iOS|Android|etc) + value (Mono2x|IL2CPP)."""
    return await _send("project_settings", _args(
        action=action, target=target, prop=prop, value=value, index=index,
        build_target=build_target))


async def material(action: str, path: str | None = None, object_path: str | None = None,
                   shader: str | None = None, prop: str | None = None, value: str | None = None,
                   source: str | None = None, targets: str | None = None,
                   slot: int | None = None, filter: str | None = None,
                   target: str | None = None) -> str:
    """Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path. get_errors: path (shader asset). list_shaders: filter (optional name filter). set_fields: path+value (newline-separated prop=val). set target: shared|instance|asset (default shared)."""
    return await _send("material", _args(
        action=action, path=path, object_path=object_path, shader=shader,
        prop=prop, value=value, source=source, targets=targets, slot=slot,
        filter=filter, target=target))


async def prefab(action: str, path: str | None = None, asset_path: str | None = None,
                 base_path: str | None = None, variant_path: str | None = None,
                 component: str | None = None, prop: str | None = None,
                 value: str | None = None, add_component: str | None = None,
                 remove_component: str | None = None,
                 recursive: bool = False,
                 mode: str | None = None, scope: str | None = None,
                 format: str | None = None) -> str:
    """Prefab. Creates or modifies prefab assets. No confirmation required. action: save|create_variant|apply|revert|get_overrides|unpack|edit|instantiate.
    edit: asset_path + component + prop + value (set property on prefab asset).
    edit: asset_path + add_component or remove_component (manage components).
    save: path (scene) + asset_path [+ mode: new|overwrite (default)].
    revert: scope: object (default)|children.
    get_overrides: format: text (default)|structured.
    create_variant: base_path + variant_path.
    instantiate: asset_path (instantiate prefab into active scene)."""
    return await _send("prefab", _args(
        action=action, path=path, asset_path=asset_path,
        base_path=base_path, variant_path=variant_path,
        component=component, prop=prop, value=value,
        add_component=add_component, remove_component=remove_component,
        recursive="true" if recursive else None,
        mode=mode, scope=scope, format=format))


async def scriptable_object(action: str, path: str | None = None, type: str | None = None,
                            prop: str | None = None, value: str | None = None,
                            fields: str | None = None,
                            filter: str | None = None) -> str:
    """ScriptableObject. action: create|get|set|list_types|find. create: type+path[+fields]. get/set: path. set/create fields: \\n-separated prop=value pairs. get fields: comma-sep filter. find: type. list_types: filter."""
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
