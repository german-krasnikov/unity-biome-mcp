namespace UnityMCP.Editor.Wizard
{
    public enum CardAction { CopyText, WriteConfig, CopyPort }

    public readonly struct BackendCard
    {
        public readonly string Name;
        public readonly string Body;
        public readonly string BtnLabel;
        public readonly CardAction Action;
        public readonly string Payload;

        public BackendCard(string name, string body, string btnLabel, CardAction action, string payload)
        {
            Name = name; Body = body; BtnLabel = btnLabel; Action = action; Payload = payload;
        }
    }

    public static class AiToolCardFactory
    {
        public static BackendCard[] Build(int port)
        {
            return new[]
            {
                // ── Group A: External MCP Hosts ───────────────────────────────
                new BackendCard("Claude Code",
                    "Write mcpServers entry to ~/.claude.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    ClaudeCodePath()),

                new BackendCard("Claude Desktop",
                    "Write mcpServers entry to claude_desktop_config.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    ClaudeDesktopPath()),

                new BackendCard("Cursor",
                    "Write mcpServers entry to ~/.cursor/mcp.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    CursorPath()),

                new BackendCard("Windsurf",
                    "Write mcpServers entry to mcp_config.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    WindsurfPath()),

                // ── Group B: In-Unity Chat Backends ───────────────────────────
                new BackendCard("Gemini",
                    $"{BiomeLabel.DisplayName} auto-configured at chat start. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("Kimi K2",
                    $"{BiomeLabel.DisplayName} auto-configured at chat start. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("Codex",
                    $"{BiomeLabel.DisplayName} wired via CLI flags at spawn. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("OpenCode",
                    $"{BiomeLabel.DisplayName} config injected via env var. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),
            };
        }

        // ── Platform-aware config paths (dsl/data/clientconfigpath.data-contract, P1 #5) ──

        public static string ClaudeCodePath()
            => ClientConfigPathTable.Resolve("claude-code");

        public static string ClaudeDesktopPath()
            => ClientConfigPathTable.Resolve("claude-desktop");

        public static string CursorPath()
            => ClientConfigPathTable.Resolve("cursor");

        public static string WindsurfPath()
            => ClientConfigPathTable.Resolve("windsurf");
    }
}
