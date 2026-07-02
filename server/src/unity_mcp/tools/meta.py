"""Meta tools — discover_tools, doctor, resolve_tool_schema, set_llm_config.

Moved out of server.py's composition root (M2) into the standard
register(mcp, send, args) pattern used by every other tools/*.py module.
"""
from mcp.server.fastmcp import Context

from ._common import bind
from .gating import discover_tools as _discover_tools_impl
from .schema_registry import _registry as _schema_registry
from ..doctor import run_doctor, format_report
from ..llm_config import parse_tcp_config, apply_config


async def discover_tools(category: str | None = None, enable: bool = True, ctx: Context = None) -> str:
    """Find and enable tools by category.
    Categories: object, animation, asset, advanced, ui, runtime, connection, session.
    Pass enable=False to browse without enabling."""
    result = await _discover_tools_impl(category, enable)
    if enable and category and ctx:
        await ctx.session.send_tool_list_changed()
    return result


async def doctor(fix: bool = False) -> str:
    """Run health diagnostics. Use fix=True to auto-repair safe issues."""
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
    Features: visual_verify, screenshot_describe, visual_diff, do_intent, summarize, distiller."""
    parsed = parse_tcp_config(config)
    if not parsed:
        return "err: no valid entries parsed"
    apply_config(parsed)
    return f"ok: updated {', '.join(parsed)}"


def register(mcp, send, args):
    bind(globals(), send, args)  # unused by these 4 tools today, kept for M3 uniformity
    mcp.tool()(discover_tools)
    mcp.tool()(doctor)
    mcp.tool()(resolve_tool_schema)
    mcp.tool()(set_llm_config)
