"""Package Manager MCP tool."""
from ._common import bind
from ._annotations import RW as _RW

_send = None
_args = None


async def package(
    action: str,
    name: str | None = None,
    version: str | None = None,
    query: str | None = None,
) -> str:
    """Package manager. action: list|search|add|remove.
    list: all installed packages.
    search: query required.
    add: name required, version optional.
    remove: name required."""
    return await _send("package", _args(
        action=action,
        name=name,
        version=version,
        query=query,
    ), timeout=60.0)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(package)
