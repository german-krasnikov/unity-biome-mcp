"""Performance and diagnostics tools — Play Mode and editor. get_memory lives in profiling.py."""
from ._annotations import RO as _RO
from ._common import bind

_send = None


async def get_perf() -> str:
    """[Play Mode] Snapshot FPS, frame time, Mono memory, and GC stats."""
    return await _send("get_perf", {})


async def debug_animator(path: str) -> str:
    """[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile).
    path: scene path to GameObject with Animator component."""
    return await _send("debug_animator", {"path": path})


async def debug_physics(path: str, radius: float = 5.0) -> str:
    """[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile).
    path: scene path to GameObject.
    radius: overlap sphere radius for nearby detection (default 5m)."""
    return await _send("debug_physics", {"path": path, "radius": radius})


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_perf)
    mcp.tool(annotations=_RO)(debug_animator)
    mcp.tool(annotations=_RO)(debug_physics)
