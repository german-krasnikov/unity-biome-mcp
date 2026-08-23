"""Editor chrome: play/pause/stop, selection, ping, undo, checkpoints, capabilities.
(B2: split from scene.py)"""
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def editor(action: str = "state", path: str | None = None,
                 paths: str | None = None, enable: str | None = None) -> str:
    """Editor state/control. action: state|play|pause|stop|select|project_path|fast_play_mode|mutation_mode.
    select: path (single) or paths (comma-sep multi, e.g. "/Player,/Enemy,/NPC").
    fast_play_mode/mutation_mode: enable='true'|'false' to toggle."""
    t = 15.0 if action in ("play", "stop", "pause") else 30.0
    return await _send("editor", _args(action=action, path=path, paths=paths, enable=enable), timeout=t)


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
