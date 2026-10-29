from ._common import bind
from ._source_patch_intent import get_cached_intent as _get_cached_intent

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import (  # noqa: F401
    get_enabled_tools,
    material,
    material_audit,
    prefab,
    project_settings,
    scriptable_object,
    tools,
)

_send = None
_args = None


def _source_patch_mutation_is_on() -> bool:
    """Route seam for `.cs` writes (§3.2 in the FSR handoff plan). Reads the
    local intent cache (P0-70) — never a network round-trip; see
    tools/_source_patch_intent.py for why, and editor_control.editor()'s
    mutation_mode branch for the writer side. C#'s SourcePatchHost is the
    authoritative pre-effect gate if this cache is ever stale."""
    return _get_cached_intent()


async def _write_text_with_capture(path: str, content: str) -> str:
    from ..changeset_coordinator import get_coordinator
    from ..changeset_file_capture import snapshot_file
    from ..changeset_store import get_store

    store = get_store()
    coord = get_coordinator()

    before_ref = snapshot_file(path, store) if store else None
    # §3.2: exactly one route, never a write-first probe, never a fallback
    # after an uncertain send.
    if path.endswith(".cs") and _source_patch_mutation_is_on():
        result = await _send("source_patch_write", _args(path=path, content=content))
    else:
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


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, asset)
