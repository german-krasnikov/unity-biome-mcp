"""get_metrics MCP tool — telemetry snapshot (resets counters when reset=True)."""
import json as _json

from ..metrics import METRICS
from ._annotations import RW as _RW
from ._common import _guard_read_only, bind


async def get_metrics(format: str = "text", reset: bool = False) -> str:
    """Returns telemetry snapshot. Clears counters when reset=True. No confirmation required. format: text|json."""
    if reset:
        _guard_read_only("get_metrics")
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
    mcp.tool(annotations=_RW)(get_metrics)
