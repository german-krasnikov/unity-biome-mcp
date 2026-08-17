"""Meta tools — discover_tools, doctor, resolve_tool_schema, set_llm_config, alias_status.

Moved out of server.py's composition root (M2) into the standard
register(mcp, send, args) pattern used by every other tools/*.py module.
"""
from mcp.server.fastmcp import Context

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


async def mcp_status() -> str:
    """Compact MCP status: scene, dirty, play/compile state, port, alias count."""
    return await _send("get_status", {})


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


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool()(discover_tools)
    # Conservative annotation: fix=True may remove stale local files. Runtime
    # read-only enforcement remains argument-aware in doctor() itself.
    mcp.tool(annotations=_RW)(doctor)
    mcp.tool()(resolve_tool_schema)
    mcp.tool()(set_llm_config)
    mcp.tool(annotations=_RO)(alias_status)
    mcp.tool(annotations=_RO)(mcp_status)
    mcp.tool(annotations=_RO)(release_smoke)
