"""Write session — batch N .cs writes into one domain reload.

Call start_write_session() before writing multiple .cs files via asset(write_text).
One domain reload fires on end_write_session(). Auto-releases after 120s watchdog.
"""
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None
# Injectable seam for tests — set to None means lazy-load await_compile on first call.
_await_compile_fn = None


async def start_write_session() -> str:
    """Open a write session — lock assemblies + disable auto-refresh.
    Call before writing multiple .cs files via asset(action='write_text').
    All writes batch into one domain reload. Close with end_write_session().
    Auto-releases after 120s watchdog if the session is not ended explicitly."""
    return await _send("start_write_session", {})


async def end_write_session(sync: bool = True) -> str:
    """Release write session lock and trigger one domain reload.
    sync=True (default): waits for compile to finish before returning.
    sync=False: returns immediately after releasing the lock."""
    result = await _send("end_write_session", {})
    if sync and not (result or "").startswith("err"):
        compile_fn = _await_compile_fn or _lazy_await_compile()
        compile_result = await compile_fn()
        return f"{result}\n{compile_result}"
    return result


def _lazy_await_compile():
    """Return await_compile lazily to avoid circular import at module load."""
    from .code_intel import await_compile
    return await_compile


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(start_write_session)
    mcp.tool(annotations=_RW)(end_write_session)
