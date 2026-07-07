"""Playtest scenario persistence — save/load/list/run .playtest files."""
import os
import pathlib
import re

from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None


def _validate_name(name: str) -> str:
    if not re.match(r'^[a-zA-Z0-9_-]+$', name):
        raise ValueError(f"Invalid scenario name: '{name}'")
    return name


def _scenarios_dir(create: bool = False) -> str:
    """Return path to scenarios directory, optionally creating it."""
    proj = os.environ.get("UNITY_PROJECT_PATH", "")
    if not proj:
        from ..lockfile import read_project_path_from_port_file
        from ..paths import ports_dir
        pd = ports_dir()
        if pd.exists():
            for f in pd.glob("*.port"):
                try:
                    lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
                    port = int(lines[0])
                    p = read_project_path_from_port_file(port)
                    if p:
                        proj = str(p)
                        break
                except (ValueError, OSError):
                    continue
    if not proj:
        raise RuntimeError("Cannot determine Unity project path. Set UNITY_PROJECT_PATH.")
    d = pathlib.Path(proj) / "Assets" / "Tests" / "PlayMode" / "Scenarios"
    if create:
        d.mkdir(parents=True, exist_ok=True)
    return str(d)


async def save_scenario(name: str, script: str) -> str:
    """Save a playtest DSL script as a named .playtest file.
    name: alphanumeric with dashes/underscores, no extension.
    script: DSL script content (same format as run_playtest input)."""
    try:
        _validate_name(name)
    except ValueError:
        return "Error: name must be alphanumeric with dashes/underscores only"
    path = pathlib.Path(_scenarios_dir(create=True)) / f"{name}.playtest"
    path.write_text(script, encoding="utf-8")
    return f"Saved: {name}"


async def load_scenario(name: str) -> str:
    """Load a .playtest scenario file by name (without .playtest extension)."""
    try:
        _validate_name(name)
    except ValueError as e:
        return f"Error: {e}"
    path = pathlib.Path(_scenarios_dir()) / f"{name}.playtest"
    if not path.exists():
        return f"Error: scenario '{name}' not found"
    return path.read_text(encoding="utf-8")


async def list_scenarios() -> str:
    """List all saved .playtest scenario files, alphabetically."""
    d = _scenarios_dir()
    if not os.path.exists(d):
        return "No scenarios found"
    names = sorted(f[:-9] for f in os.listdir(d) if f.endswith(".playtest"))
    return "\n".join(names) if names else "No scenarios found"


async def run_scenario(name: str, timeout: float = 120.0,
                       abort_on_fail: bool = False) -> str:
    """Load and run a saved playtest scenario. Equivalent to load_scenario + run_playtest.
    timeout: seconds for run_playtest execution (default 120).
    abort_on_fail: stop Play Mode on first assertion failure."""
    script = await load_scenario(name)
    if script.startswith("Error:"):
        return script
    return await _send("run_playtest", _args(
        script=script,
        timeout=str(timeout),
        abort_on_fail="true" if abort_on_fail else None),
        timeout=timeout + 20.0)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(save_scenario)
    mcp.tool(annotations=_RO)(load_scenario)
    mcp.tool(annotations=_RO)(list_scenarios)
    mcp.tool(annotations=_RW)(run_scenario)
