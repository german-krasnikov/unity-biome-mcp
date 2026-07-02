"""Rendering analysis MCP tool.

render_analyze dispatches to RenderAnalyzer.cs on the Unity side.
FrameDebugHelper.cs handles frame_debug action via reflection.
"""
from ._common import bind
from ._annotations import RO as _RO

_send = None
_args = None


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
    mcp.tool(annotations=_RO)(render_analyze)
