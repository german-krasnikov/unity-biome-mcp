"""Rendering analysis MCP tool.

render_analyze dispatches to RenderAnalyzer.cs on the Unity side.
FrameDebugHelper.cs handles frame_debug action via reflection.
"""
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def bake(target: str, action: str | None = None) -> str:
    """Bake operations.
    target: lighting|occlusion.
    action (lighting): start(default)|status|cancel|clear|settings.
    action (occlusion): start(default)|status|clear.
    Poll status after start — lighting bake is async."""
    return await _send("bake", _args(target=target, action=action))


async def render_analyze(
    action: str,
    path: str | None = None,
    detail: str = "brief",
    baseline_id: str | None = None,
    max_events: int | None = None,
) -> str:
    """Rendering analysis.
    action: stats|materials|shaders|lights|batching|overdraw|audit|compare
            |frame_debug|shadow_audit|probe_audit|light_optimize
    stats: draw calls, batches, tris, verts, set-pass from UnityStats.
    batching: SRP Batcher / static / dynamic / GPU instancing analysis.
    audit: full rendering health check (all sections, brief).
    compare: diff against last baseline snapshot.
    frame_debug: per-draw-call data via FrameDebugger reflection (pauses rendering briefly).
    detail: brief (default) | full.  path: optional subtree root."""
    return await _send("render_analyze", _args(
        action=action, path=path, detail=detail,
        baseline_id=baseline_id, max_events=max_events))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(bake)
    mcp.tool(annotations=_RO)(render_analyze)
