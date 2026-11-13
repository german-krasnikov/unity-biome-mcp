# Generated from dsl/data/clientconfigpath.data-contract
# source-sha256=a558ec74ef26978a8a0012c02399e13d3463584f094a63d5e05de73ffe41778f
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ClientConfigPath:
    key: str
    path: str
    pathMac: str | None = None
    pathWin: str | None = None


CLIENT_CONFIG_PATH_ROWS: tuple[ClientConfigPath, ...] = (
    ClientConfigPath(key="claude-code", path="~/.claude.json"),
    ClientConfigPath(
        key="claude-desktop",
        path="~/.config/Claude/claude_desktop_config.json",
        pathMac="~/Library/Application Support/Claude/claude_desktop_config.json",
        pathWin="{appdata}/Claude/claude_desktop_config.json",
    ),
    ClientConfigPath(key="cursor", path="~/.cursor/mcp.json"),
    ClientConfigPath(
        key="windsurf",
        path="~/.codeium/windsurf/mcp_config.json",
        pathWin="{appdata}/Codeium/windsurf/mcp_config.json",
    ),
)
