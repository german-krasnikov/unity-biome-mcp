import re
from ._annotations import RO as _RO, RW as _RW, DEL as _DEL
from ._common import bind

_RE_SLOT = re.compile(r'slot_\d+\s+\[\]\s+#')
_RE_POINT = re.compile(r'point_\d+\s+\[\]\s+#')
_RE_MESH = re.compile(r'\[MeshFilter,MeshRenderer\]\s+#')

_send = None
_args = None


def _count_group(lines: list[str], i: int, regex, extra_check=None) -> tuple[str, int]:
    """Count consecutive lines matching regex (with optional extra_check). Returns (indent, count)."""
    indent = lines[i][:len(lines[i]) - len(lines[i].lstrip())]
    count = 1
    while i + count < len(lines) and regex.search(lines[i + count]):
        if extra_check and not extra_check(lines[i + count]):
            break
        count += 1
    return indent, count


def compress_hierarchy(text: str) -> str:
    """Compress hierarchy output: group identical siblings, collapse visual-only subtrees."""
    lines = text.split('\n')
    result = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if _RE_SLOT.search(line):
            indent, count = _count_group(lines, i, _RE_SLOT)
            result.append(f"{indent}[{count}x slot]")
            i += count
            continue
        if _RE_POINT.search(line):
            indent, count = _count_group(lines, i, _RE_POINT)
            result.append(f"{indent}[{count}x point]")
            i += count
            continue
        if _RE_MESH.search(line) and '...' not in line:
            indent, count = _count_group(lines, i, _RE_MESH, lambda l: '...' not in l)
            if count >= 3:
                result.append(f"{indent}[{count}x visual mesh]")
                i += count
                continue
        result.append(line)
        i += 1
    return '\n'.join(result)


async def get_hierarchy(depth: int = 2, root: str | None = None, filter: str | None = None,
                        components: bool = False, compress: bool = False,
                        summary: bool = False, incremental: bool = False, full: bool = False,
                        scene: str | None = None) -> str:
    """Scene hierarchy as text tree. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call. full=True: bypass distillation. scene: filter to a single scene by name (multi-scene only)."""
    no_distill = {"_no_distill": True} if full else {}
    if summary:
        return await _send("get_hierarchy", _args(root=root, summary="true", scene=scene, **no_distill))
    result = await _send("get_hierarchy", _args(
        depth=depth, root=root, filter=filter,
        components="true" if components else None,
        incremental="true" if incremental else None,
        scene=scene,
        **no_distill))
    if compress:
        result = compress_hierarchy(result)
    return result


async def scene(action: str, path: str | None = None) -> str:
    """Scene management. action: new|open|save|discard|open_additive|close|set_active|list.
    path: required for open/save/open_additive/close/set_active. list requires no path."""
    return await _send("scene", _args(action=action, path=path))


async def search_scene(query: str, root: str | None = None, limit: int = 50,
                       scene: str | None = None) -> str:
    """Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.
    root: scope search to subtree (path or None for whole scene).
    limit: max results (default 50; 0=unlimited). Default not sent over wire.
    scene: filter to a single scene by name (multi-scene only)."""
    return await _send("search_scene", _args(
        query=query, root=root,
        limit=str(limit) if limit != 50 else None,
        scene=scene))


async def fingerprint(path: str | None = None, depth: int = 3) -> str:
    """Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens."""
    return await _send("fingerprint", _args(path=path, depth=depth))


async def scene_environment(action: str = "get", prop: str | None = None,
                            value: str | None = None) -> str:
    """Read/write scene environment: ambient light, fog, skybox, reflections.
    action: get|set. set requires prop and value.
    Props: ambientMode, ambientLight, ambientIntensity, ambientSkyColor, ambientEquatorColor,
    ambientGroundColor, fog, fogColor, fogMode, fogDensity, fogStartDistance, fogEndDistance,
    reflectionIntensity, reflectionBounces, subtractiveShadowColor, defaultReflectionResolution."""
    return await _send("scene_environment", _args(action=action, prop=prop, value=value))


async def scene_diff() -> str:
    """Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines."""
    return await _send("scene_diff", {})


# Re-exports from scene_session for backward compatibility
from .scene_session import (  # noqa: E402
    save_session, load_session, screenshot_baseline, screenshot_compare,
    get_changes, _extract_saved_path,
)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_hierarchy)
    mcp.tool(annotations=_DEL)(scene)
    mcp.tool(annotations=_RO)(search_scene)
    mcp.tool(annotations=_RO)(fingerprint)
    mcp.tool(annotations=_RO)(scene_diff)
    mcp.tool(annotations=_RW)(scene_environment)
    from . import scene_session
    scene_session.register(mcp, send, args)
