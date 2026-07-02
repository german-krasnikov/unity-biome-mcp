// Single source of truth for the per-project MCP config file targets — avoids repeating
// paths/root-keys across the orchestrator. See Plans/Install/11-phase1a-design.md.
namespace UnityMCP.Editor.Wizard
{
    internal readonly struct ProjectConfigTarget
    {
        internal readonly string Key;          // matches BackendDescriptor.Key, e.g. "cursor"
        internal readonly string RelativePath; // e.g. ".vscode/mcp.json"
        internal readonly string RootKey;      // "mcpServers" | "servers" | null (TOML)
        internal readonly bool IsToml;

        internal ProjectConfigTarget(string key, string relativePath, string rootKey, bool isToml)
        {
            Key = key;
            RelativePath = relativePath;
            RootKey = rootKey;
            IsToml = isToml;
        }
    }

    internal static class ProjectConfigTargets
    {
        // Junie's root key is assumed "mcpServers" (no local grounding in this repo's
        // existing config code) — flagged in 11-phase1a-design.md Risks; if wrong, only
        // this one row changes.
        internal static readonly ProjectConfigTarget[] All =
        {
            new ProjectConfigTarget("claude-code", ".mcp.json", "mcpServers", false),
            new ProjectConfigTarget("cursor", ".cursor/mcp.json", "mcpServers", false),
            new ProjectConfigTarget("vscode", ".vscode/mcp.json", "servers", false),
            new ProjectConfigTarget("windsurf", ".windsurf/mcp.json", "mcpServers", false),
            new ProjectConfigTarget("codex", ".codex/config.toml", null, true),
            new ProjectConfigTarget("junie", ".junie/mcp/mcp.json", "mcpServers", false),
        };

        // Single source of truth for BackendDescriptor.Key → per-project relative path —
        // used by ConfigureScreen so the UI module doesn't keep its own duplicate map.
        internal static string RelativePathFor(string key)
        {
            foreach (var t in All)
                if (t.Key == key) return t.RelativePath;
            return null;
        }
    }
}
