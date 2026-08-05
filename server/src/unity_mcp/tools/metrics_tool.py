"""get_metrics MCP tool — read-only telemetry snapshot."""
import json as _json

from ..metrics import METRICS
from ._annotations import RO as _RO
from ._common import bind


async def get_metrics(format: str = "text", reset: bool = False) -> str:
    """Returns telemetry snapshot. format: text|json. reset=true clears counters atomically."""
    if reset:
        snap = METRICS.snapshot_and_reset()
        if format == "json":
            return _json.dumps(snap, ensure_ascii=False)
        # Format from the captured snapshot — METRICS already cleared
        # Use a temporary registry for formatting
        return _format_snapshot(snap)
    if format == "json":
        return _json.dumps(METRICS.snapshot(), ensure_ascii=False)
    return METRICS.format_report()


def _format_snapshot(snap: dict) -> str:
    """Format a snapshot dict as text (used after snapshot_and_reset)."""
    lines = [f"=== Unity Biome MCP Metrics (uptime {snap.get('uptime_s', 0):.0f}s) ==="]
    c = snap.get("counters", {})
    if c:
        lines.append("")
        lines.extend(f"  {k}: {c[k]}" for k in sorted(c))
    return "\n".join(lines)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_metrics)
