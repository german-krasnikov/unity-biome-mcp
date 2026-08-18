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
    ),
    "claude-code": ClientInfo(
        name="Claude Code",
        config_path=pathlib.Path.home() / ".claude.json",
        scope="global",
    ),
    "cursor": ClientInfo(
        name="Cursor",
        config_path=pathlib.Path.home() / ".cursor" / "mcp.json",
        scope="global",
    ),
    "windsurf": ClientInfo(
        name="Windsurf",
        config_path=_windsurf_path(),
        scope="global",
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
    ),
    "generic": ClientInfo(
        name="Generic (stdout)",
        config_path=pathlib.Path(os.devnull),
        scope="global",
        stdout_only=True,
    ),
}


def detect_installed() -> list[str]:
    """Return keys of clients whose config file or parent dir exists. Skips stdout_only."""
    found = []
    for key, info in CLIENT_REGISTRY.items():
        if info.stdout_only:
            continue
        if info.config_path.exists() or info.config_path.parent.exists():
            found.append(key)
    return found
