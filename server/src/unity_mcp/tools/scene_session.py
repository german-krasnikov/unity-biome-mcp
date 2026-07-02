import os
import time
from ._annotations import RW as _RW, RO as _RO
from ._common import bind

_send = None
_args = None


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
    mcp.tool(annotations=_RW)(save_session)
    mcp.tool(annotations=_RO)(load_session)
    mcp.tool(annotations=_RW)(screenshot_baseline)
    mcp.tool(annotations=_RO)(screenshot_compare)
    mcp.tool(annotations=_RO)(get_changes)
