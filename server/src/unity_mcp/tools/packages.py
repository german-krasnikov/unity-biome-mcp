"""Package Manager MCP tool."""
from ._common import bind
from .generated.catalog_tools import tools

_send = None
_args = None


async def package(
    action: str,
    name: str | None = None,
    version: str | None = None,
    query: str | None = None,
) -> str:
    """Package manager. Adds or removes packages. No confirmation required. action: list|search|add|remove.
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
    tools.register(mcp, package)
