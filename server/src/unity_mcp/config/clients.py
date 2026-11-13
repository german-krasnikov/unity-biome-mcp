"""Client registry: known MCP-compatible AI tools and their config paths."""
import os
import pathlib
import sys
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass, field

from unity_mcp.config.generated.client_config_path_rows import CLIENT_CONFIG_PATH_ROWS
from unity_mcp.config.generated.install_dir_rows import INSTALL_DIR_ROWS

_INSTALL_DIR_BY_KEY = {row.key: row for row in INSTALL_DIR_ROWS}
_CLIENT_CONFIG_PATH_BY_KEY = {row.key: row for row in CLIENT_CONFIG_PATH_ROWS}


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
    # is created on first `claude` run, before ~/.claude.json exists). Resolved
    # from dsl/data/installdir.data-contract, the single generated source of
    # truth for "is this client installed" (ARC-0b), shared with
    # unity-plugin/Editor/Wizard/BackendDescriptor.cs's native reader. None for
    # clients with no InstallDir row (kimi, opencode, junie, generic).
    install_dir: pathlib.Path | None = None


def _client_config_path(key: str) -> pathlib.Path:
    """Resolve the ClientConfigPath row for `key` (dsl/data/clientconfigpath.data-contract) on this
    platform. "~" and "{appdata}" are substituted the same way _install_dir() substitutes them for
    the installdir rows, and the same way ClientConfigPathTable.cs's native C# reader substitutes
    them for the identical generated rows (P1 #5)."""
    row = _CLIENT_CONFIG_PATH_BY_KEY[key]
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", str(pathlib.Path.home() / "AppData" / "Roaming"))
        return pathlib.Path((row.pathWin or row.path).replace("{appdata}", appdata))
    template = row.pathMac if sys.platform == "darwin" and row.pathMac else row.path
    return pathlib.Path(template.replace("~", str(pathlib.Path.home())))


def _vscode_path() -> pathlib.Path:
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Code" / "User" / "mcp.json"
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Code" / "User" / "mcp.json"
    return pathlib.Path.home() / ".config" / "Code" / "User" / "mcp.json"


def _codex_path() -> pathlib.Path:
    return pathlib.Path.home() / ".codex" / "config.toml"


def _install_dir(key: str) -> pathlib.Path:
    """Resolve the InstallDir row for `key` (dsl/data/installdir.data-contract) on this
    platform. "~" and "{appdata}" are substituted the same way BackendDescriptor.cs's
    native C# reader substitutes them for the identical generated rows (ARC-0b)."""
    row = _INSTALL_DIR_BY_KEY[key]
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", str(pathlib.Path.home() / "AppData" / "Roaming"))
        return pathlib.Path((row.dirWin or row.dir).replace("{appdata}", appdata))
    template = row.dirMac if sys.platform == "darwin" and row.dirMac else row.dir
    return pathlib.Path(template.replace("~", str(pathlib.Path.home())))


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
    # Preserve unknown keys (e.g. "_pin", DEV-58/B2-P7) — but not source keys
    # already consumed above under a different shape/name ("args" folded into
    # "command", "env" renamed to "environment").
    consumed = {"command", "args", "env"}
    result.update({k: v for k, v in entry.items() if k not in result and k not in consumed})
    return result


def _vscode_transform(entry: dict) -> dict:
    """Reformat standard entry into VS Code's typed stdio format."""
    result: dict = {"type": "stdio", "command": entry["command"], "args": entry.get("args", [])}
    if "env" in entry:
        result["env"] = entry["env"]
    result.update({k: v for k, v in entry.items() if k not in result})  # preserve "_pin" etc. (DEV-58/B2-P7)
    return result


CLIENT_REGISTRY: dict[str, ClientInfo] = {
    "claude-desktop": ClientInfo(
        name="Claude Desktop",
        config_path=_client_config_path("claude-desktop"),
        scope="global",
        install_dir=_install_dir("claude-desktop"),
    ),
    "claude-code": ClientInfo(
        name="Claude Code",
        config_path=_client_config_path("claude-code"),
        scope="global",
        install_dir=_install_dir("claude-code"),
    ),
    "cursor": ClientInfo(
        name="Cursor",
        config_path=_client_config_path("cursor"),
        scope="global",
        install_dir=_install_dir("cursor"),
    ),
    "windsurf": ClientInfo(
        name="Windsurf",
        config_path=_client_config_path("windsurf"),
        scope="global",
        install_dir=_install_dir("windsurf"),
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
        install_dir=_install_dir("vscode"),
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
        install_dir=_install_dir("codex"),
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
