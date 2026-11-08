"""Editor chrome: play/pause/stop, selection, ping, undo, checkpoints, capabilities.
(B2: split from scene.py)"""
from unity_mcp.constants import NOOP_RECOVERY_RESULT as _NOOP_RECOVERY_RESULT

from ._common import bind
from ._source_patch_intent import set_cached_intent as _set_cached_intent

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import checkpoint, get_capabilities, get_selection, tools, undo_last  # noqa: F401

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
    if (action == "mutation_mode" and enable is not None and not result.startswith("err:")
            and result != _NOOP_RECOVERY_RESULT):
        # noop_recovery: the reload port ACKed a no-op it can't verify and
        # Unity moved to Recovery, not Off -- caching enable=False here would
        # misrepresent Unity's real state to asset.py's .cs write router.
        _set_cached_intent(enable)
    return result


async def ping_object(path: str) -> str:
    """Highlight object in Hierarchy and Project, and select it."""
    return await _send("ping_object", _args(path=path))


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, editor, ping_object)
