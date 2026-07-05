using System;
using System.IO;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Pure static diagnostics — no Unity I/O, fully unit-testable.</summary>
    public static class SetupDiagnostics
    {
        // ── Repo root ─────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the repo root directory (parent of unity-plugin/) by delegating to
        /// InstallSourceDetector.LocalRepoRoot(). Returns null for UPM git/registry installs.
        /// </summary>
        public static string ResolveRepoRoot()
            => InstallSourceDetector.LocalRepoRoot();

        // ── Python ────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a usable Python executable exists for the given server dir.
        /// Resolution order: .venv/Scripts/python.exe (Windows) → .venv/bin/python → python3/python fallback.
        /// </summary>
        public static (bool ok, string detail) CheckPython(string serverDir)
        {
            if (string.IsNullOrEmpty(serverDir) || !Directory.Exists(serverDir))
                return (false, "Server directory not found");

            var cmd = ResolvePythonCmd(serverDir);
            var isFallback = cmd == "python3" || cmd == "python";
            if (isFallback)
                return (true, $"Python via {cmd} (system fallback)");

            if (File.Exists(cmd))
                return (true, $"Python at {cmd}");

            return (false, $"Python not found (tried {cmd})");
        }

        private static string ResolvePythonCmd(string serverDir)
        {
            var win = Path.Combine(serverDir, ".venv", "Scripts", "python.exe");
            if (File.Exists(win)) return win;

            var venv = Path.Combine(serverDir, ".venv", "bin", "python");
            if (File.Exists(venv)) return venv;

            return UnityEngine.SystemInfo.operatingSystemFamily
                == UnityEngine.OperatingSystemFamily.Windows ? "python" : "python3";
        }

        // ── Server ────────────────────────────────────────────────────────────

        /// <summary>Returns running state of the MCP TCP server.</summary>
        public static (bool ok, string detail) CheckServer()
        {
            if (!MCPServer.IsRunning)
                return (false, "MCP server not running");

            return (true, $"Server on :{MCPServer.ServerPort}");
        }

        // ── Snippet ───────────────────────────────────────────────────────────

        /// <summary>Builds the <c>claude mcp add</c> command for the given port.</summary>
        public static string BuildClaudeCodeSnippet(int port)
            => $"claude mcp add unity -- env UNITY_MCP_PORT={port} uvx --from {WizardConfigWriter.GitInstallUrl} unity-mcp";

        // ── Port file ─────────────────────────────────────────────────────────

        /// <summary>Checks whether ~/.unity-mcp/ports/ contains at least one .port file.</summary>
        public static (bool ok, string detail) CheckPortFile()
        {
            var portsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-mcp", "ports");
            if (!Directory.Exists(portsDir))
                return (false, "~/.unity-mcp/ports not found");
            var files = Directory.GetFiles(portsDir, "*.port");
            return files.Length > 0
                ? (true, $"{files.Length} port file(s)")
                : (false, "no .port files (Unity not started yet?)");
        }

        // ── AI config ─────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether at least one AI tool config file contains a "unity-mcp" entry.
        /// Returns the path of the first matching config, or an error detail.
        /// </summary>
        public static (bool ok, string detail) CheckAiConfig()
        {
            var paths = new[]
            {
                AiToolCardFactory.ClaudeCodePath(),
                AiToolCardFactory.ClaudeDesktopPath(),
                AiToolCardFactory.CursorPath(),
                AiToolCardFactory.WindsurfPath(),
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                try
                {
                    if (File.ReadAllText(p).Contains($"\"{PermissionConfig.SERVER_NAME}\""))
                        return (true, p);
                }
                catch (IOException) { }
            }
            return (false, "no AI tool config found with unity-mcp entry");
        }
    }
}
