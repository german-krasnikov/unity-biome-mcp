"""Console logs + compilation error reporting (B2: split from scene.py)."""
import time as _time

from ._annotations import RO as _RO, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


async def get_console(count: int = 10, level: str | None = None, first: int = 0,
                      keyword: str | None = None, count_only: bool = False,
                      since: float | None = None) -> str:
    """Recent console logs. For C# compile errors use get_compile_errors instead. keyword: case-insensitive substring filter. count_only: return N matches as string. since: only logs from last N seconds."""
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
    csharp = await _send("get_compile_errors", {})
    try:
        raw = await _send("compile_status", {})
        state = raw.split("|")[0] if "|" in raw else raw
    except Exception:
        state = ""
    return editor_log.corroborate(csharp, compile_status=state)


async def recompile() -> str:
    """Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done."""
    return await _send("recompile", {}, timeout=60.0)


async def console_mark(label: str = "") -> str:
    """Create a console watermark. Returns mark_id encoding current timestamp.
    Pass to get_console_since() to retrieve only logs after this point.
    Pure Python — no TCP call."""
    ts = f"mark:{_time.time()}"
    return f"{ts}:{label}" if label else ts


async def get_console_since(mark_id: str, level: str | None = None,
                             count: int = 500) -> str:
    """Console entries after the watermark created by console_mark().
    mark_id: string from console_mark().
    level: optional filter ('error,exception,assert').
    count: max entries to return (default 500)."""
    try:
        ts = float(mark_id.split(":")[1])
    except (IndexError, ValueError):
        return "err: invalid mark_id"
    since_s = _time.time() - ts
    if since_s < 0:
        return "err: mark_id timestamp in future"
    return await get_console(count=count, level=level, since=since_s)


def register(mcp, send, args):
    bind(globals(), send, args)
    from .. import editor_log
    editor_log.init_corroboration()
    mcp.tool(annotations=_RO)(get_console)
    mcp.tool(annotations=_RO)(get_compile_errors)
    mcp.tool(annotations=_RW_IDEM)(recompile)
    mcp.tool(annotations=_RO)(console_mark)
    mcp.tool(annotations=_RO)(get_console_since)
