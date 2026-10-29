"""AI profiling tools: get_frame_stats, profile sessions, get_memory (with include filter)."""
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import get_frame_stats, get_memory, tools  # noqa: F401

_send = None
_args = None


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


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, profile)
