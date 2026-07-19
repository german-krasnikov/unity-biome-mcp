import os
import re
import time
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
    """Scene hierarchy as text tree. For finding specific object by name/type use search_scene. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call. full=True: bypass distillation. scene: filter to a single scene by name (multi-scene only)."""
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


async def scene(action: str, path: str | None = None, scene_name: str | None = None) -> str:
    """Scene management. action: new|open|save|discard|open_additive|close|set_active|list.
    path: required for open/save/open_additive/close/set_active. list requires no path.
    scene_name: save/discard target when multiple scenes loaded (identifies by name)."""
    return await _send("scene", _args(action=action, path=path, scene=scene_name))


async def search_scene(query: str, root: str | None = None, limit: int = 50,
                       scene: str | None = None) -> str:
    """Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.
    root: scope search to subtree (path or None for whole scene).
    limit: max results (default 50; 0=unlimited).
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


def _extract_saved_path(result: str) -> str:
    return result.split("Data saved to: ")[-1].strip()


async def save_session() -> str:
    """Save current scene state to .claude/session-context.json for cold-start recovery."""
    hierarchy = await _send("get_hierarchy", {"summary": "true"})
    path = os.path.join(os.getcwd(), ".claude", "session-context.json")
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(f"{time.time()}\n=== hierarchy ===\n{hierarchy}\n")
    except OSError as e:
        return f"Failed to save session: {e}"
    return f"Session saved to {path}"


async def load_session() -> str:
    """Load previous session context. Shows hierarchy diff since last save."""
    path = os.path.join(os.getcwd(), ".claude", "session-context.json")
    if not os.path.exists(path):
        return "No previous session found."
    try:
        with open(path, encoding="utf-8") as f:
            content = f.read()
        ts_str, _, hier = content.partition("\n=== hierarchy ===\n")
        ts = float(ts_str.strip())
    except (OSError, ValueError):
        return "Session file corrupt or unreadable"
    current = await _send("get_hierarchy", {"summary": "true"})
    label = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(ts))
    return f"Previous ({label}):\n{hier.strip()}\n\nCurrent:\n{current}"


async def screenshot_baseline(name: str = "default", width: int = 640, height: int = 480,
                               camera: str | None = None) -> str:
    """Save screenshot as baseline for visual regression. name: identifier for this baseline."""
    import shutil
    result = await _send("screenshot", _args(width=width, height=height, camera=camera))
    if "Data saved to:" not in result:
        return result
    src = _extract_saved_path(result)
    baseline_dir = os.path.join(os.getcwd(), ".claude", "baselines")
    os.makedirs(baseline_dir, exist_ok=True)
    baseline_path = os.path.join(baseline_dir, f"{name}.png")
    shutil.copy2(src, baseline_path)
    return f"Baseline saved: {baseline_path}"


async def get_changes(clear: bool = True) -> str:
    """Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,
    play mode, scene open/save, selection. Returns chronological event list or NO_CHANGES."""
    return await _send("get_changes", _args(clear="true" if clear else "false"))


async def screenshot_compare(name: str = "default", width: int = 640, height: int = 480,
                              camera: str | None = None, mode: str = "auto",
                              question: str | None = None) -> str:
    """Compare current screenshot with saved baseline.
    mode: auto (pixel->escalate), pixel (free), structural (Haiku general),
          targeted (needs question=), ui_layout|animation|color|position (specialized).
    Cached by image hashes. Cost: structural ~$0.005."""
    from ..visual_diff import visual_diff
    baseline_path = os.path.join(os.getcwd(), ".claude", "baselines", f"{name}.png")
    if not os.path.exists(baseline_path):
        return f"No baseline '{name}' found. Use screenshot_baseline first."
    result = await _send("screenshot", _args(width=width, height=height, camera=camera))
    if "Data saved to:" not in result:
        return "Could not capture current screenshot"
    current_path = _extract_saved_path(result)
    return await visual_diff(baseline_path, current_path, mode=mode, question=question)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(get_hierarchy)
    mcp.tool(annotations=_DEL)(scene)
    mcp.tool(annotations=_RO)(search_scene)
    mcp.tool(annotations=_RO)(fingerprint)
    mcp.tool(annotations=_RO)(scene_diff)
    mcp.tool(annotations=_RW)(scene_environment)
    mcp.tool(annotations=_RW)(save_session)
    mcp.tool(annotations=_RO)(load_session)
    mcp.tool(annotations=_RW)(screenshot_baseline)
    mcp.tool(annotations=_RO)(screenshot_compare)
    mcp.tool(annotations=_RO)(get_changes)
