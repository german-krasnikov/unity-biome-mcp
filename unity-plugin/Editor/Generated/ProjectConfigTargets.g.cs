// Generated from dsl/data/projectconfig.data-contract, dsl/data/projectconfig.core-data
// source-sha256=c40728a0942742aef27993eb1a44c0b384cedc96ea01c6a375d940d76bdfa0c3
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Wizard
{
    public sealed class ProjectConfigTarget
    {
        public readonly string key;
        public readonly string relativePath;
        public readonly bool isToml;
        public readonly string? rootKey;
        public ProjectConfigTarget(string key, string relativePath, bool isToml, string? rootKey = null)
        {
            this.key = key;
            this.relativePath = relativePath;
            this.isToml = isToml;
            this.rootKey = rootKey;
        }
    }

    public static class ProjectConfigTargetRows
    {
        public static readonly ProjectConfigTarget[] All =
        {
            new ProjectConfigTarget(
                key: "claude-code",
                relativePath: ".mcp.json",
                isToml: false,
                rootKey: "mcpServers"
            ),
            new ProjectConfigTarget(
                key: "cursor",
                relativePath: ".cursor/mcp.json",
                isToml: false,
                rootKey: "mcpServers"
            ),
            new ProjectConfigTarget(key: "vscode", relativePath: ".vscode/mcp.json", isToml: false, rootKey: "servers"),
            new ProjectConfigTarget(
                key: "windsurf",
                relativePath: ".windsurf/mcp.json",
                isToml: false,
                rootKey: "mcpServers"
            ),
            new ProjectConfigTarget(key: "codex", relativePath: ".codex/config.toml", isToml: true),
            new ProjectConfigTarget(
                key: "junie",
                relativePath: ".junie/mcp/mcp.json",
                isToml: false,
                rootKey: "mcpServers"
            ),
        };
    }
}
