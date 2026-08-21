import asyncio
import contextlib
import os
import re
import shutil
import time

from mcp.server.fastmcp.exceptions import ToolError

from ._annotations import DEL as _DEL
from ._annotations import RO as _RO
from ._annotations import RW as _RW
from ._common import _guard_read_only, bind

_OBJECT_REF = r'(?:&[0-9A-Za-z]+|#[^\s]+|\$[0-9A-Fa-f]+)'
_RE_SLOT = re.compile(rf'slot_\d+\s+\[\]\s+{_OBJECT_REF}')
_RE_POINT = re.compile(rf'point_\d+\s+\[\]\s+{_OBJECT_REF}')
_RE_MESH = re.compile(rf'\[MeshFilter,MeshRenderer\]\s+{_OBJECT_REF}')
_RE_TREE_PREFIX = re.compile(r'(?:(?:│ {2}| {3}))*(?:├─ |└─ )')

_send = None
_args = None
_path_locks: dict[str, asyncio.Lock] = {}


def _session_context_path() -> str:
    return os.path.join(os.getcwd(), ".claude", "session-context.json")


def _read_file(path: str) -> str:
    with open(path, encoding="utf-8") as f:
        return f.read()


def _write_file_atomic(path: str, content: str) -> None:
    parent = os.path.dirname(path) or "."
    os.makedirs(parent, exist_ok=True)
    tmp_path = f"{path}.{os.getpid()}.{time.time_ns()}.tmp"
    with open(tmp_path, "w", encoding="utf-8") as f:
        f.write(content)
        f.flush()
        os.fsync(f.fileno())
    try:
        os.replace(tmp_path, path)
    except OSError:
        with contextlib.suppress(OSError):
            os.unlink(tmp_path)
        raise


def _copy_file_atomic(src: str, dst: str) -> None:
    parent = os.path.dirname(dst) or "."
    os.makedirs(parent, exist_ok=True)
    tmp_path = f"{dst}.{os.getpid()}.{time.time_ns()}.tmp"
    try:
        with open(src, "rb") as src_file, open(tmp_path, "wb") as dst_file:
            shutil.copyfileobj(src_file, dst_file)
            dst_file.flush()
            os.fsync(dst_file.fileno())
        os.replace(tmp_path, dst)
    except OSError:
        with contextlib.suppress(OSError):
            os.unlink(tmp_path)
        raise


def _path_lock(path: str) -> asyncio.Lock:
    lock = _path_locks.get(path)
    if lock is None:
        lock = asyncio.Lock()
        _path_locks[path] = lock
    return lock


def _indent_key(line: str) -> tuple[str, int, str]:
    """Return hierarchy depth identity plus the prefix to preserve in output."""
    tree = _RE_TREE_PREFIX.match(line)
    if tree:
        prefix = tree.group(0)
        return "tree", len(prefix) // 3, prefix
    prefix = line[:len(line) - len(line.lstrip())]
    return "space", len(prefix), prefix


def _count_group(lines: list[str], i: int, regex, extra_check=None) -> tuple[str, int]:
    """Count consecutive lines matching regex (with optional extra_check). Returns (indent, count)."""
    indent_kind, indent_depth, indent = _indent_key(lines[i])
    count = 1
    while i + count < len(lines) and regex.search(lines[i + count]):
        candidate = lines[i + count]
        candidate_kind, candidate_depth, _ = _indent_key(candidate)
        if ((candidate_kind, candidate_depth) != (indent_kind, indent_depth)
                or (extra_check and not extra_check(candidate))):
            break
        count += 1
    # Preserve the final sibling marker (└─ when the compressed run ends a subtree).
    _, _, indent = _indent_key(lines[i + count - 1])
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
        compress="true" if compress else None,
        scene=scene,
        **no_distill))
    if compress:
        result = compress_hierarchy(result)
    return result


async def scene(action: str, path: str | None = None, scene: str | None = None,
                include_unsaved: bool = True) -> str:
    """Scene management. action: new|open|save|discard|open_additive|close|set_active|list|save_copy.
    path: required for open/open_additive/close/set_active/save_copy. For save,
    omit it to save to the current path; an untitled scene requires a path.
    scene: save/discard/save_copy target when multiple scenes loaded (identifies by name).
    save_copy: writes current dirty state to path as backup; active scene reference unchanged.
    include_unsaved: always True — save_copy always captures current in-memory state."""
    return await _send("scene", _args(action=action, path=path, scene=scene))


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


def _safe_baseline_name(name: str) -> str:
    """Reject baseline names that could escape the managed baseline directory."""
    if not name or "/" in name or "\\" in name or ".." in name:
        raise ToolError(
            "Invalid baseline name: use a non-empty identifier without '/', '\\', or '..'"
        )
    return name


async def save_session() -> str:
    """Save current scene state to .claude/session-context.json for cold-start recovery."""
    _guard_read_only("save_session")
    hierarchy = await _send("get_hierarchy", {"summary": "true"})
    path = _session_context_path()
    try:
        payload = f"{time.time()}\n=== hierarchy ===\n{hierarchy}\n"
        async with _path_lock(path):
            await asyncio.to_thread(_write_file_atomic, path, payload)
    except OSError as e:
        return f"Failed to save session: {e}"
    return f"Session saved to {path}"


async def load_session() -> str:
    """Load previous session context beside the current hierarchy."""
    path = _session_context_path()
    async with _path_lock(path):
        if not os.path.exists(path):
            return "No previous session found."
        try:
            content = await asyncio.to_thread(_read_file, path)
            ts_str, _, hier = content.partition("\n=== hierarchy ===\n")
            ts = float(ts_str.strip())
        except (OSError, ValueError):
            return "Session file corrupt or unreadable"
    current = await _send("get_hierarchy", {"summary": "true"})
    label = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(ts))
    return f"Previous ({label}):\n{hier.strip()}\n\nCurrent:\n{current}"


async def screenshot_baseline(name: str = "default", width: int = 640, height: int = 480,
                               camera: str | None = None) -> str:
    """Save screenshot as baseline for visual regression. name: file-safe identifier."""
    name = _safe_baseline_name(name)
    result = await _send("screenshot", _args(width=width, height=height, camera=camera))
    if "Data saved to:" not in result:
        return result
    src = _extract_saved_path(result)
    baseline_dir = os.path.join(os.getcwd(), ".claude", "baselines")
    baseline_path = os.path.join(baseline_dir, f"{name}.png")
    async with _path_lock(baseline_path):
        await asyncio.to_thread(_copy_file_atomic, src, baseline_path)
    return f"Baseline saved: {baseline_path}"


async def get_changes(clear: bool = True) -> str:
    """Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,
    play mode, scene open/save, selection. Returns chronological event list or NO_CHANGES."""
    return await _send("get_changes", _args(clear="true" if clear else "false"))


async def screenshot_compare(name: str = "default", width: int = 640, height: int = 480,
                              camera: str | None = None, mode: str = "auto",
                              question: str | None = None) -> str:
    """Compare current screenshot with saved baseline.
    mode: auto (pixel->escalate), pixel (local), structural (general),
          targeted (needs question=), ui_layout|animation|color|position|regression.
    Model-assisted modes require configured sampling. Cached by image hashes."""
    from ..visual_diff import visual_diff
    name = _safe_baseline_name(name)
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
    mcp.tool(annotations=_RW)(screenshot_compare)
    mcp.tool(annotations=_RW)(get_changes)
