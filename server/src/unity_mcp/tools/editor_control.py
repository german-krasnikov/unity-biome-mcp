"""Editor chrome: play/pause/stop, selection, ping, undo, checkpoints, capabilities.
(B2: split from scene.py)"""
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind
from ._source_patch_intent import set_cached_intent as _set_cached_intent

_send = None
_args = None


async def editor(action: str = "state", path: str | None = None,
                 paths: str | None = None, enable: bool | None = None) -> str:
    """Editor state/control. action: state|play|pause|stop|select|project_path|mutation_mode.
    select: path (single) or paths (comma-sep multi, e.g. "/Player,/Enemy,/NPC").
    mutation_mode: omit enable to query current intent; enable=True/False to set it."""
    t = 15.0 if action in ("play", "stop", "pause") else 30.0
    result = await _send("editor", _args(
        action=action, path=path, paths=paths,
        enable=None if enable is None else ("true" if enable else "false")), timeout=t)
    if action == "mutation_mode" and enable is not None and not result.startswith("err:"):
        _set_cached_intent(enable)
    return result


async def ping_object(path: str) -> str:
    """Highlight object in Hierarchy and Project, and select it."""
    return await _send("ping_object", _args(path=path))


async def get_selection() -> str:
    """Currently selected GameObject: path and component list."""
    return await _send("get_selection", {})


async def checkpoint(label: str = "checkpoint") -> str:
    """Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity."""
    return await _send("checkpoint", _args(label=label))


async def undo_last(turns: int = 1) -> str:
    """Undo the last N AI turns in the Unity Undo stack. Default: 1.
    warn: file-system operations (asset creation/deletion via asset tool) are
    not reversed by undo. Only scene-object and component mutations are undoable."""
    return await _send("undo_last", _args(turns=turns))


async def get_capabilities() -> str:
    """Unity version, platform, render pipeline, scripting backend, and optional packages available."""
    return await _send("get_capabilities", {})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(editor)
    mcp.tool(annotations=_RW)(ping_object)
    mcp.tool(annotations=_RO)(get_selection)
    mcp.tool(annotations=_RW)(checkpoint)
    mcp.tool(annotations=_RW)(undo_last)
    mcp.tool(annotations=_RO)(get_capabilities)
