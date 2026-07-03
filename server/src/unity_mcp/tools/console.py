"""Console logs + compilation error reporting (B2: split from scene.py)."""
from ._annotations import RO as _RO, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def get_console(count: int = 10, level: str | None = None, first: int = 0,
                      keyword: str | None = None, count_only: bool = False,
                      since: float | None = None) -> str:
    """Recent console logs. keyword: case-insensitive substring filter. count_only: return N matches as string. since: only logs from last N seconds."""
    return await _send("get_console", _args(
        count=count, level=level,
        first=first if first > 0 else None,
        keyword=keyword,
        count_only="true" if count_only else None,
        since=since,
    ))


async def get_compile_errors() -> str:
    """Compilation errors with file:line:column. Not lost on Console.Clear(). Structured, typed."""
    from .. import editor_log
    return editor_log.corroborate(await _send("get_compile_errors", {}))


async def recompile() -> str:
    """Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done."""
    return await _send("recompile", {}, timeout=60.0)


def register(mcp, send, args):
    bind(globals(), send, args)
    from .. import editor_log
    editor_log.init_corroboration()
    mcp.tool(annotations=_RO)(get_console)
    mcp.tool(annotations=_RO)(get_compile_errors)
    mcp.tool(annotations=_RW_IDEM)(recompile)
