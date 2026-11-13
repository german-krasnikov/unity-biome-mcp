// Generated from dsl/data/clientconfigpath.data-contract, dsl/data/clientconfigpath.core-data
// source-sha256=a558ec74ef26978a8a0012c02399e13d3463584f094a63d5e05de73ffe41778f
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.Wizard
{
    public sealed class ClientConfigPath
    {
        public readonly string Key;
        public readonly string Path;
        public readonly string? PathMac;
        public readonly string? PathWin;
        public ClientConfigPath(string key, string path, string? pathMac = null, string? pathWin = null)
        {
            this.Key = key;
            this.Path = path;
            this.PathMac = pathMac;
            this.PathWin = pathWin;
        }
    }

    public static class ClientConfigPathRows
    {
        public static readonly ClientConfigPath[] All =
        {
            new ClientConfigPath(key: "claude-code", path: "~/.claude.json"),
            new ClientConfigPath(
                key: "claude-desktop",
                path: "~/.config/Claude/claude_desktop_config.json",
                pathMac: "~/Library/Application Support/Claude/claude_desktop_config.json",
                pathWin: "{appdata}/Claude/claude_desktop_config.json"
            ),
            new ClientConfigPath(key: "cursor", path: "~/.cursor/mcp.json"),
            new ClientConfigPath(
                key: "windsurf",
                path: "~/.codeium/windsurf/mcp_config.json",
                pathWin: "{appdata}/Codeium/windsurf/mcp_config.json"
            ),
        };
    }
}
