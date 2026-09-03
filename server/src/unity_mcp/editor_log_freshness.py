"""Plugin source discovery and DLL freshness checks.

Pure stdlib (pathlib only) — no intra-package imports.
"""
from pathlib import Path


def find_plugin_source_files(plugin_dir: Path | None = None) -> list[Path]:
    """Return .cs files under plugin_dir that belong ONLY to UnityMCP.Editor assembly.

    Excludes .cs files under any directory that contains a .asmdef whose stem is
    NOT 'UnityMCP.Editor' (e.g. Chat, Tests, Chat.Tests asmdefs).
    plugin_dir=None → auto-resolve from repo root (same as find_plugin_source_dir).
    """
    if plugin_dir is None:
        repo_root = Path(__file__).resolve().parents[3]
        plugin_dir = repo_root / "unity-plugin"
    if not plugin_dir.exists():
        return []

    # Find all dirs that have a foreign asmdef (stem != 'UnityMCP.Editor')
    excluded_dirs: set[Path] = set()
    for asmdef in plugin_dir.rglob("*.asmdef"):
        if asmdef.stem != "UnityMCP.Editor":
            excluded_dirs.add(asmdef.parent.resolve())

    result = []
    for cs in plugin_dir.rglob("*.cs"):
        cs_resolved = cs.resolve()
        if not any(
            cs_resolved == excl or excl in cs_resolved.parents
            for excl in excluded_dirs
        ):
            result.append(cs)
    return result


def find_plugin_source_dir() -> list[Path] | None:
    """Return [repo/unity-plugin] if it exists and contains .cs files; else None.

    Walks up from this file: server/src/unity_mcp/editor_log.py → parents[3] = repo root.
    In an installed/standalone server there is no sibling unity-plugin → None (correct:
    end users don't edit the plugin so freshness is undeterminable → trust C#).
    """
    repo_root = Path(__file__).resolve().parents[3]
    plugin_dir = repo_root / "unity-plugin"
    if plugin_dir.exists() and next(plugin_dir.rglob("*.cs"), None) is not None:
        return [plugin_dir]
    return None


def check_dll_freshness(
    project_path: Path,
    source_dirs: list[Path] | None = None,
    grace_s: float = 10.0,
    source_files: list[Path] | None = None,
) -> bool | None:
    """Compare UnityMCP.Editor.dll mtime vs plugin .cs files.

    source_files (preferred): explicit list of .cs files — no rglob of foreign asmdefs.
    source_dirs (legacy): directories to rglob for *.cs (may include foreign asmdefs).
    source_dirs=None/[] and source_files=None → None (undeterminable).
    Returns True (fresh), False (stale), None (undeterminable).
    """
    dll = project_path / "Library" / "ScriptAssemblies" / "UnityMCP.Editor.dll"
    if not dll.exists():
        return None

    if source_files is not None:
        cs_files = source_files
    elif source_dirs:
        cs_files = [f for d in source_dirs for f in d.rglob("*.cs")]
    else:
        return None

    if not cs_files:
        return None

    dll_mtime = dll.stat().st_mtime
    # A cached source_files list can outlive the process that built it — a file
    # deleted after the cache was populated must not crash freshness checks
    # (DEV-67). Treat a missing path as absent, not as an error.
    mtimes = []
    for f in cs_files:
        try:
            mtimes.append(f.stat().st_mtime)
        except FileNotFoundError:
            continue
    if not mtimes:
        return None
    return (dll_mtime + grace_s) >= max(mtimes)
