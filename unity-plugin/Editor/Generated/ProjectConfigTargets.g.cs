// Generated from dsl/data/projectconfig.data-contract
// source-sha256=c40728a0942742aef27993eb1a44c0b384cedc96ea01c6a375d940d76bdfa0c3
// Edit the source, then run biome generate.

namespace UnityMCP.Editor.Wizard
{
    internal readonly struct ProjectConfigTarget
    {
        internal readonly string Key;
        internal readonly string RelativePath;
        internal readonly bool IsToml;
        internal readonly string RootKey;
        internal ProjectConfigTarget(string key, string relativePath, bool isToml, string rootKey = null)
        {
            Key = key;
            RelativePath = relativePath;
            IsToml = isToml;
            RootKey = rootKey;
        }
    }

    internal static class ProjectConfigTargetRows
    {
        internal static readonly ProjectConfigTarget[] All =
        {
            new ProjectConfigTarget(key: "claude-code", relativePath: ".mcp.json", isToml: false, rootKey: "mcpServers"),
            new ProjectConfigTarget(key: "cursor", relativePath: ".cursor/mcp.json", isToml: false, rootKey: "mcpServers"),
            new ProjectConfigTarget(key: "vscode", relativePath: ".vscode/mcp.json", isToml: false, rootKey: "servers"),
            new ProjectConfigTarget(key: "windsurf", relativePath: ".windsurf/mcp.json", isToml: false, rootKey: "mcpServers"),
            new ProjectConfigTarget(key: "codex", relativePath: ".codex/config.toml", isToml: true),
            new ProjectConfigTarget(key: "junie", relativePath: ".junie/mcp/mcp.json", isToml: false, rootKey: "mcpServers"),
        };
    }
}
