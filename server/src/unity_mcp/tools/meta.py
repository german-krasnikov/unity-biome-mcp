"""Meta tools — discover_tools, doctor, resolve_tool_schema, set_llm_config, alias_status.

Moved out of server.py's composition root (M2) into the standard
register(mcp, send, args) pattern used by every other tools/*.py module.
"""

from mcp.server.fastmcp import Context  # noqa: TC002 — fastmcp eval_str=True requires runtime
from mcp.server.fastmcp.exceptions import ToolError

from ..doctor import format_report, run_doctor
from ..llm_config import apply_config, parse_tcp_config
from . import code_intel as _ci
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import _guard_read_only, bind
from .gating import discover_tools as _discover_tools_impl
from .schema_registry import _registry as _schema_registry

_send = None
_args = None
_get_slot = None

# Budget for the get_status liveness probe. Independent of brief_builder's
# _PROVIDER_TIMEOUT (LLM attachment providers) despite the matching value --
# a quick TCP status query and an LLM call are unrelated concerns.
_STATUS_TIMEOUT_S = 5.0


async def discover_tools(category: str | None = None, enable: bool = True,
                         include_legacy: bool = False, structured: bool = False,
                         ctx: Context = None) -> str:
    """Find and enable tools by category.
    Canonical 10: SCENE, COMPONENTS, ASSETS, UGUI, UITOOLKIT, MEDIA,
    VERIFY, RUNTIME, TESTS, SYSTEM.
    include_legacy=True adds legacy aliases (object, animation, etc.).
    structured=True adds surface/mutability info. enable=False to browse only."""
    result = await _discover_tools_impl(category, enable, include_legacy, structured)
    if enable and category:
        session = ctx.session if ctx else None
        if session is None:
            from ..server_filtering import get_active_session
            session = get_active_session()
        if session is not None:
            await session.send_tool_list_changed()
    return result


async def doctor(fix: bool = False) -> str:
    """Run health diagnostics. fix=True removes safe stale port/lock files."""
    if fix:
        _guard_read_only("doctor")
    results = await run_doctor(fix=fix)
    return format_report(results)


async def resolve_tool_schema(tools: str) -> str:
    """Return full parameter schemas for deferred tools. tools=comma-separated names."""
    names = [n.strip() for n in tools.split(",") if n.strip()]
    text = _schema_registry.format_text(names)
    if not text:
        unknown = ", ".join(names)
        return f"No schema found for: {unknown}"
    return text


async def set_llm_config(config: str) -> str:
    """Override LLM profiles for sampling features. Format: feature:model,turns,timeout,max_tokens (one per line).
    Features: visual_verify, screenshot_describe, visual_diff, do_intent, ui_intent, vfx_intent, animator_intent, summarize, distiller."""
    parsed = parse_tcp_config(config)
    if not parsed:
        return "err: no valid entries parsed"
    apply_config(parsed)
    return f"ok: updated {', '.join(parsed)}"


async def alias_status() -> str:
    """Check alias table health: loaded/empty/stale, sources, and total alias count."""
    return await _send("alias_status", {})


def _fmt_num(val) -> str:
    """Format a numeric bridge diagnostic. Anything non-numeric — an honest
    None (e.g. bridge.last_contact_age_s before first contact, or in the
    sub-ping-interval null window right after a reconnect) or an unset
    Mock auto-attribute in tests — reports 'n/a' rather than crashing or
    fabricating a value (ARC-7 T3)."""
    if isinstance(val, bool):
        return "n/a"
    if isinstance(val, (int, float)):
        return f"{val:.1f}" if isinstance(val, float) else str(val)
    return "n/a"


def _fmt_bool(val) -> str:
    return "true" if val is True else "false" if val is False else "n/a"


async def mcp_status() -> str:
    """Compact MCP status: scene, dirty, play/compile state, port, alias count,
    version — plus Python-side liveness diagnostics (ARC-7 T3) that answer
    honestly, never raising, when Unity itself is unreachable."""
    from .. import __version__
    try:
        cs_status = await _send("get_status", {}, timeout=_STATUS_TIMEOUT_S)
        unity_status = "reachable"
    except ToolError:
        cs_status = ""
        unity_status = "unreachable"

    slot = _get_slot() if _get_slot else None
    bridge = slot.bridge if slot is not None else None

    liveness = getattr(bridge, "status", None)
    liveness = liveness if isinstance(liveness, str) else "unknown"

    # C1 #7: bridge.status stays "connected" through a tolerated stall window
    # (writer not yet closed) while the independent get_status probe above
    # already reported unreachable — surface the stall instead of printing
    # a bare "connected" that contradicts unity_status=unreachable.
    if liveness == "connected" and unity_status == "unreachable":
        liveness = "connected-stalled"

    pid_alive = None
    probe = getattr(bridge, "_probe", None)
    if probe is not None:
        dead = probe.is_process_dead()
        if isinstance(dead, bool):
            pid_alive = not dead

    lines = [
        f"liveness={liveness}",
        f"unity_status={unity_status}",
        f"pid_alive={_fmt_bool(pid_alive)}",
        f"last_contact_s={_fmt_num(getattr(bridge, 'last_contact_age_s', None))}",
        f"ping_fail={_fmt_num(getattr(bridge, '_ping_failures', None))}",
        f"ping_stall={_fmt_num(getattr(bridge, '_ping_stall_failures', None))}",
        f"queue_depth={_fmt_num(getattr(bridge, 'pending_queue_depth', None))}",
        cs_status,
        f"python_version={__version__}",
    ]
    return "\n".join(x for x in lines if x)


async def release_smoke() -> str:
    """Run release readiness checks: status, aliases, compile. Returns PASS/FAIL summary."""
    results = []

    status = await _send("get_status", {})
    results.append(f"status: {'ok' if 'err' not in status.lower() else 'FAIL'}")

    aliases = await _send("alias_status", {})
    results.append(f"aliases: {'ok' if 'err' not in aliases.lower() else 'FAIL'}")

    compile_r = await _ci.await_compile(timeout=10.0)
    results.append(f"compile: {'ok' if not compile_r or 'clean' in compile_r.lower() else 'FAIL'}")

    passed = all("FAIL" not in r for r in results)
    return f"{'PASS' if passed else 'FAIL'}\n" + "\n".join(results)


def register(mcp, send, args, *, get_slot=None):
    global _get_slot
    bind(globals(), send, args)
    _get_slot = get_slot
    mcp.tool()(discover_tools)
    # Conservative annotation: fix=True may remove stale local files. Runtime
    # read-only enforcement remains argument-aware in doctor() itself.
    mcp.tool(annotations=_RW)(doctor)
    mcp.tool()(resolve_tool_schema)
    mcp.tool()(set_llm_config)
    mcp.tool(annotations=_RO)(alias_status)
    mcp.tool(annotations=_RO)(mcp_status)
    mcp.tool(annotations=_RO)(release_smoke)
