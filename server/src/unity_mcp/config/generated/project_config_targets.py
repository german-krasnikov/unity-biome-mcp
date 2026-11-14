# Generated from dsl/data/projectconfig.data-contract, dsl/data/projectconfig.core-data
# source-sha256=c40728a0942742aef27993eb1a44c0b384cedc96ea01c6a375d940d76bdfa0c3
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ProjectConfigTarget:
    key: str
    relativePath: str
    isToml: bool
    rootKey: str | None = None


PROJECT_CONFIG_TARGET_ROWS: tuple[ProjectConfigTarget, ...] = (
    ProjectConfigTarget(
        key="claude-code",
        relativePath=".mcp.json",
        isToml=False,
        rootKey="mcpServers",
    ),
    ProjectConfigTarget(
        key="cursor",
        relativePath=".cursor/mcp.json",
        isToml=False,
        rootKey="mcpServers",
    ),
    ProjectConfigTarget(
        key="vscode",
        relativePath=".vscode/mcp.json",
        isToml=False,
        rootKey="servers",
    ),
    ProjectConfigTarget(
        key="windsurf",
        relativePath=".windsurf/mcp.json",
        isToml=False,
        rootKey="mcpServers",
    ),
    ProjectConfigTarget(key="codex", relativePath=".codex/config.toml", isToml=True),
    ProjectConfigTarget(
        key="junie",
        relativePath=".junie/mcp/mcp.json",
        isToml=False,
        rootKey="mcpServers",
    ),
)
