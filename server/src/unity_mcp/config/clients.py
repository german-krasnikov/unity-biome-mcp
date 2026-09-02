"""Client registry: known MCP-compatible AI tools and their config paths."""
import os
import pathlib
import sys
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass, field


@dataclass
class ClientInfo:
    name: str
    config_path: pathlib.Path
    scope: str  # "global" or "project"
    stdout_only: bool = False  # if True: print config to stdout instead of writing file
    root_key: str = "mcpServers"  # JSON key that holds server entries
    entry_transformer: Callable[[dict], dict] | None = field(default=None, repr=False)
    is_toml: bool = False  # Codex uses TOML, not JSON
    # Directory whose mere existence signals the client CLI/app is installed,
    # independent of whether config_path has been written yet (e.g. ~/.claude
    # is created on first `claude` run, before ~/.claude.json exists). Mirrors
    # unity-plugin/Editor/Wizard/BackendDescriptor.cs's ConfigDir — the single
    # source of truth for "is this client installed" (ARC-0b). None for
    # clients where BackendDescriptor sets no ConfigDir (kimi, opencode, junie).
    install_dir: pathlib.Path | None = None


def _claude_desktop_path() -> pathlib.Path:
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Claude" / "claude_desktop_config.json"
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Claude" / "claude_desktop_config.json"
    return pathlib.Path.home() / ".config" / "Claude" / "claude_desktop_config.json"


def _windsurf_path() -> pathlib.Path:
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Codeium" / "windsurf" / "mcp_config.json"
    return pathlib.Path.home() / ".codeium" / "windsurf" / "mcp_config.json"


def _vscode_path() -> pathlib.Path:
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Code" / "User" / "mcp.json"
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Code" / "User" / "mcp.json"
    return pathlib.Path.home() / ".config" / "Code" / "User" / "mcp.json"


def _codex_path() -> pathlib.Path:
    return pathlib.Path.home() / ".codex" / "config.toml"


def _claude_desktop_install_dir() -> pathlib.Path:
    """Mirrors BackendDescriptor.cs ConfigDir for claude-desktop (per-platform)."""
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Claude"
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Claude"
    return pathlib.Path.home() / ".config" / "Claude"


def _windsurf_install_dir() -> pathlib.Path:
    """Mirrors BackendDescriptor.cs ConfigDir for windsurf: ~/.codeium (non-Windows),
    one level above config_path's parent — the Codeium install root, not the
    windsurf-specific subfolder."""
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Codeium" / "windsurf"
    return pathlib.Path.home() / ".codeium"


def _vscode_install_dir() -> pathlib.Path:
    """Mirrors BackendDescriptor.cs ConfigDir for vscode: the Code app-support
    dir itself, one level above config_path's parent (.../Code/User)."""
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Code"
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Code"
    return pathlib.Path.home() / ".config" / "Code"


def _opencode_path() -> pathlib.Path:
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "opencode" / "opencode.json"
    return pathlib.Path.home() / ".config" / "opencode" / "opencode.json"


def _opencode_transform(entry: dict) -> dict:
    """Reformat standard entry into OpenCode's command-as-array format."""
    cmd = [entry["command"]] + entry.get("args", [])
    result: dict = {"type": "local", "command": cmd, "enabled": True}
    if "env" in entry:
        result["environment"] = entry["env"]  # OpenCode's key is "environment", not "env"
    return result


def _vscode_transform(entry: dict) -> dict:
    """Reformat standard entry into VS Code's typed stdio format."""
    result: dict = {"type": "stdio", "command": entry["command"], "args": entry.get("args", [])}
    if "env" in entry:
        result["env"] = entry["env"]
    return result


CLIENT_REGISTRY: dict[str, ClientInfo] = {
    "claude-desktop": ClientInfo(
        name="Claude Desktop",
        config_path=_claude_desktop_path(),
        scope="global",
        install_dir=_claude_desktop_install_dir(),
    ),
    "claude-code": ClientInfo(
        name="Claude Code",
        config_path=pathlib.Path.home() / ".claude.json",
        scope="global",
        install_dir=pathlib.Path.home() / ".claude",
    ),
    "cursor": ClientInfo(
        name="Cursor",
        config_path=pathlib.Path.home() / ".cursor" / "mcp.json",
        scope="global",
        install_dir=pathlib.Path.home() / ".cursor",
    ),
    "windsurf": ClientInfo(
        name="Windsurf",
        config_path=_windsurf_path(),
        scope="global",
        install_dir=_windsurf_install_dir(),
    ),
    "kimi": ClientInfo(
        name="Kimi",
        config_path=pathlib.Path.home() / ".kimi-code" / "mcp.json",
        scope="global",
    ),
    "junie": ClientInfo(
        name="Junie",
        # Mirrors ProjectConfigTargets.cs's ".junie/mcp/mcp.json" project-relative path,
        # rooted at $HOME (same convention as codex: ".codex/config.toml" -> "~/.codex/config.toml").
        config_path=pathlib.Path.home() / ".junie" / "mcp" / "mcp.json",
        scope="global",
    ),
    "vscode": ClientInfo(
        name="VS Code",
        config_path=_vscode_path(),
        scope="global",
        root_key="servers",
        entry_transformer=_vscode_transform,
        install_dir=_vscode_install_dir(),
    ),
    "opencode": ClientInfo(
        name="OpenCode",
        config_path=_opencode_path(),
        scope="global",
        root_key="mcp",
        entry_transformer=_opencode_transform,
    ),
    "codex": ClientInfo(
        name="Codex",
        config_path=_codex_path(),
        scope="global",
        is_toml=True,
        install_dir=pathlib.Path.home() / ".codex",
    ),
    "generic": ClientInfo(
        name="Generic (stdout)",
        config_path=pathlib.Path(os.devnull),
        scope="global",
        stdout_only=True,
    ),
}


def detect_installed() -> list[str]:
    """Return keys of clients whose config file, parent dir, or install_dir exists.
    Skips stdout_only.

    The parent-dir fallback only counts when the parent is an app-specific
    directory (e.g. ~/.cursor). For clients whose config_path lives directly
    under $HOME (e.g. claude-code's ~/.claude.json), the parent is $HOME
    itself — always present — so it is never treated as an install signal on
    its own. install_dir (mirrors BackendDescriptor.cs ConfigDir, ARC-0b) is
    the authoritative "client is installed" signal and is checked
    independently of config_path, since the CLI/app dir (e.g. ~/.claude) is
    typically created before the MCP config file is ever written.
    """
    found = []
    home = pathlib.Path.home()
    for key, info in CLIENT_REGISTRY.items():
        if info.stdout_only:
            continue
        parent = info.config_path.parent
        if (
            info.config_path.exists()
            or (parent != home and parent.exists())
            or (info.install_dir is not None and info.install_dir.exists())
        ):
            found.append(key)
    return found
