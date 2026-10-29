# Generated from dsl/tools.biome
# source-sha256=656282e8dd231f9a6dcec3d3df084647c65a101ac4035b113dc85e22d74f4e9a
# Edit the source, then run biome generate.

from .. import _catalog_runtime as rt
from .._common import bind

ostr = str | None
oint = int | None
ofloat = float | None
obool = bool | None

_send = None
_args = None
tools = rt.ToolSet()


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.install(mcp)


tools.expect('rename_object', 'RW_IDEM', "Rename a GameObject. Returns new scene path after rename.", (
    ('path', str),
    ('name', str),
))


@tools.provide
async def rename_object(
    path: str,
    name: str,
) -> str:
    ("Rename a GameObject. Returns new scene path after rename."
     "\n    path: current scene path, &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy). name: new name (non-empty)."
     "\n    Note: all subsequent MCP calls must use the new path."
    )
    return await _send('rename_object', _args(
        path=rt.w_str(path),
        name=rt.w_str(name),
    ))


tools.expect('search_scene', 'RO', "Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.", (
    ('query', str),
    ('root', ostr, None),
    ('limit', int, 50),
    ('scene', ostr, None),
))


@tools.provide
async def search_scene(
    query: str,
    root: ostr = None,
    limit: int = 50,
    scene: ostr = None,
) -> str:
    ("Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces."
     "\n    root: scope search to subtree (path or None for whole scene)."
     "\n    limit: max results (default 50; 0=unlimited)."
     "\n    scene: filter to a single scene by name (multi-scene only)."
    )
    return await _send('search_scene', _args(
        query=rt.w_str(query),
        root=rt.w_ostr(root),
        limit=rt.w_int(limit, 50),
        scene=rt.w_ostr(scene),
    ))


tools.expect('set_active', 'RW_IDEM', "Set GameObject active/inactive.", (
    ('path', str),
    ('active', bool),
))


@tools.provide
async def set_active(
    path: str,
    active: bool,
) -> str:
    ("Set GameObject active/inactive."
    )
    return await _send('set_active', _args(
        path=rt.w_str(path),
        active=rt.w_bool(active),
    ))


tools.expect('set_sibling_index', 'RW', "Set sibling index of a GameObject within its parent. index=0 moves to first child.", (
    ('path', str),
    ('index', int),
))


@tools.provide
async def set_sibling_index(
    path: str,
    index: int,
) -> str:
    ("Set sibling index of a GameObject within its parent. index=0 moves to first child."
    )
    return await _send('set_sibling_index', _args(
        path=rt.w_str(path),
        index=rt.w_int(index),
    ))


tools.expect('use_skill', 'RW', "Execute a previously saved skill. params: comma-separated key=value for substitution.", (
    ('name', str),
    ('params', ostr, None),
))
