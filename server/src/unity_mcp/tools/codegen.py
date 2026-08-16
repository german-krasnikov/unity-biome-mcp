"""C# code execution, schema introspection, and sampling-backed error fixing."""
import re

from ..console_levels import PROBLEM_LEVELS
from ..metrics import METRICS
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def execute_code(code: str, undo_label: str = "execute_code") -> str:
    """Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile.
    Security uses a configurable source-pattern scan; the default AllowAll level skips it. Execution is not sandboxed.
    Bare statements are auto-wrapped in a static class — no boilerplate needed.
    Example: \"var go = new GameObject(\\\"Test\\\"); return go.name;\""""
    return await _send("execute_code", _args(code=code, undo_label=undo_label))


async def get_schema(type: str) -> str:
    """Get all serialized fields of a component type with types. Use before set_property to know exact field names."""
    return await _send("get_schema", {"type": type})


try:
    from mcp.server.fastmcp import Context as _Context
    _has_context = True
except ImportError:
    _Context = object
    _has_context = False


async def auto_fix(ctx: _Context) -> str:
    """Analyze recent Unity errors and ask MCP client sampling for a fix suggestion.
    This read-only tool does not edit files or apply the suggested change."""
    from .. import editor_log
    console = await _send("get_console", {"count": 10, "level": PROBLEM_LEVELS})
    compile_errors = editor_log.corroborate(await _send("get_compile_errors", {}))
    if "No compilation errors" in compile_errors and not console:
        return "No errors to fix."
    errors = []
    if "No compilation errors" not in compile_errors:
        errors.append(f"Compilation:\n{compile_errors}")
    if console:
        errors.append(f"Console:\n{console}")
    error_text = "\n".join(errors)
    try:
        response = await ctx.session.create_message(
            messages=[{"role": "user", "content": {"type": "text",
                "text": f"Unity errors:\n{error_text}\n\nSuggest exact fix (file path + code change). Be specific."}}],
            max_tokens=500,
        )
        METRICS.inc("codegen_calls")
        suggestion = getattr(response.content, "text", "No suggestion")
        return f"ERRORS:\n{error_text}\n\nSUGGESTED FIX:\n{suggestion}"
    except Exception as e:
        return f"ERRORS:\n{error_text}\n\n(Auto-fix unavailable: {e})"


async def smart_build(description: str, ctx: _Context) -> str:
    """Build scene objects from natural language description using MCP sampling + execute_code."""
    try:
        response = await ctx.session.create_message(
            messages=[{"role": "user", "content": {"type": "text",
                "text": f"Write Unity C# code (bare statements, no class) to: {description}\nUse: new GameObject(), AddComponent, transform.position, etc."}}],
            max_tokens=1000,
        )
        METRICS.inc("codegen_calls")
        code = getattr(response.content, "text", "")
        m = re.search(r"```(?:csharp|cs)?\n(.*?)```", code, re.DOTALL)
        if m:
            code = m.group(1).strip()
        if not code.strip():
            return "Sampling returned empty code."
        opens, closes = code.count('{'), code.count('}')
        if opens != closes:
            return (f"err: LLM produced unbalanced braces ({opens} open, {closes} close), "
                     "retry smart_build with simpler description.")
        return await _send("execute_code", {"code": code})
    except Exception as e:
        return f"Sampling unavailable: {e}. Use execute_code manually."


def register(mcp, send, args):
    bind(globals(), send, args)
    from .. import editor_log
    editor_log.init_corroboration()
    mcp.tool(annotations=_RW)(execute_code)
    mcp.tool(annotations=_RO)(get_schema)
    mcp.tool(annotations=_RO)(auto_fix)
    mcp.tool(annotations=_RW)(smart_build)
