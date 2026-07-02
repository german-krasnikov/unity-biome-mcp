namespace UnityMCP.Editor.Wizard
{
    public enum InstallMechanism { PythonConfig, CliCommand, ChatAuto, ManualInstructions }

    public sealed class BackendDescriptor
    {
        public string Key;
        public string DisplayName;
        public string Icon;
        public string Description;
        public InstallMechanism Mechanism;
        public string BinaryName; // for PATH check via which/where
        public string ConfigDir;  // for dir existence check (~ expanded at runtime)
        public bool AutoProjectConfig; // true when ProjectConfigWriter auto-generates a per-project file for this backend
        public string Instructions;    // populated only for ManualInstructions entries

        public static readonly BackendDescriptor[] All = new[]
        {
            new BackendDescriptor
            {
                Key = "claude-code", DisplayName = "Claude Code", Icon = "◆",
                Description = "Anthropic's CLI — writes ~/.claude.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "claude", ConfigDir = "~/.claude",
                AutoProjectConfig = true
            },
            new BackendDescriptor
            {
                Key = "claude-desktop", DisplayName = "Claude Desktop", Icon = "◆",
                Description = "Desktop app — writes mcpServers config",
                Mechanism = InstallMechanism.PythonConfig,
#if UNITY_EDITOR_WIN
                ConfigDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Claude")
#elif UNITY_EDITOR_OSX
                ConfigDir = "~/Library/Application Support/Claude"
#else
                ConfigDir = "~/.config/Claude"
#endif
            },
            new BackendDescriptor
            {
                Key = "cursor", DisplayName = "Cursor", Icon = "▶",
                Description = "AI-first editor — writes ~/.cursor/mcp.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "cursor", ConfigDir = "~/.cursor",
                AutoProjectConfig = true
            },
            new BackendDescriptor
            {
                Key = "windsurf", DisplayName = "Windsurf", Icon = "◈",
                Description = "Codeium's editor — writes mcp_config.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "windsurf",
#if UNITY_EDITOR_WIN
                ConfigDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Codeium", "windsurf"),
#else
                ConfigDir = "~/.codeium",
#endif
                AutoProjectConfig = true
            },
            new BackendDescriptor
            {
                Key = "vscode", DisplayName = "VS Code", Icon = "◧",
                Description = "Visual Studio Code with Copilot MCP",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "code",
                AutoProjectConfig = true
            },
            new BackendDescriptor
            {
                Key = "codex", DisplayName = "Codex", Icon = "◉",
                Description = "OpenAI Codex CLI — writes MCP config file",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "codex",
                AutoProjectConfig = true
            },
            new BackendDescriptor
            {
                Key = "kimi", DisplayName = "Kimi K2", Icon = "◎",
                Description = "Moonshot AI — MCP auto-configured at chat start",
                Mechanism = InstallMechanism.ChatAuto
            },
            new BackendDescriptor
            {
                Key = "opencode", DisplayName = "OpenCode", Icon = "◌",
                Description = "Open-source CLI — writes MCP config file",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "opencode"
            },
            new BackendDescriptor
            {
                Key = "antigravity", DisplayName = "Antigravity", Icon = "◑",
                Description = "In-Unity chat backend — auto-configured",
                Mechanism = InstallMechanism.ChatAuto
            },
            new BackendDescriptor
            {
                Key = "rider-ai", DisplayName = "Rider AI Assistant", Icon = "◐",
                Description = "JetBrains Rider — MCP configured via Settings UI, no config file",
                Mechanism = InstallMechanism.ManualInstructions,
                Instructions = "Settings → Tools → AI Assistant → MCP → + → paste command below"
            },
        };
    }
}
