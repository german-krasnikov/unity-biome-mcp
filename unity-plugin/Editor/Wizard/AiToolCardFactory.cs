using System;
using System.IO;

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

        // ── Platform-aware config paths (mirrors Python clients.py) ──────────

        public static string ClaudeCodePath()
            => Path.Combine(Home(), ".claude.json");

        public static string ClaudeDesktopPath()
        {
#if UNITY_EDITOR_WIN
            var appdata = Environment.GetEnvironmentVariable("APPDATA")
                ?? Path.Combine(Home(), "AppData", "Roaming");
            return Path.Combine(appdata, "Claude", "claude_desktop_config.json");
#elif UNITY_EDITOR_OSX
            return Path.Combine(Home(), "Library", "Application Support", "Claude", "claude_desktop_config.json");
#else
            return Path.Combine(Home(), ".config", "Claude", "claude_desktop_config.json");
#endif
        }

        public static string CursorPath()
            => Path.Combine(Home(), ".cursor", "mcp.json");

        public static string WindsurfPath()
        {
#if UNITY_EDITOR_WIN
            var appdata = Environment.GetEnvironmentVariable("APPDATA")
                ?? Path.Combine(Home(), "AppData", "Roaming");
            return Path.Combine(appdata, "Codeium", "windsurf", "mcp_config.json");
#else
            return Path.Combine(Home(), ".codeium", "windsurf", "mcp_config.json");
#endif
        }

        private static string Home()
            => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
