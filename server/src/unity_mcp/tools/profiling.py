"""AI profiling tools: get_frame_stats, profile sessions, get_memory (with include filter)."""
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def get_frame_stats(include: str = "") -> str:
    """Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No session needed.
    include: narrow output — e.g. 'gc' for GC stats only."""
    extra = {"include": include} if include else {}
    return await _send("get_frame_stats", extra)


async def profile(
    action: str,
    duration: float = 5.0,
    session: str = "",
    compare_with: str = "",
    focus: str = "",
    mode: str = "burst",
    threshold_ms: float = 33.3,
) -> str:
    """Profile CPU/GPU/memory over time.
    action: start|stop|status|analyze|compare|list_sessions
    mode: burst (auto-stop after duration) | manual (explicit stop) | triggered (on spike)
    focus: narrow analyze output to gc|rendering|physics|cpu"""
    return await _send("profile", _args(
        action=action,
        duration=str(duration) if action == "start" and mode != "manual" and mode != "triggered" else None,
        session=session or None,
        compare_with=compare_with or None,
        focus=focus or None,
        mode=mode if action == "start" else None,
        threshold_ms=str(threshold_ms) if mode == "triggered" else None,
    ))


async def get_memory(include: str = "all") -> str:
    """Memory snapshot.
    include: all|textures|meshes|audio|gc — narrow the asset-type breakdown."""
    return await _send("get_memory", _args(
        include=include if include != "all" else None,
    ))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_frame_stats)
    mcp.tool(annotations=_RW)(profile)
    mcp.tool(annotations=_RO)(get_memory)
