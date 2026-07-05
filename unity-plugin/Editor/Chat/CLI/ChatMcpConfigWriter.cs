// Generates the --mcp-config JSON for the Claude CLI, deriving the server path
// from the UPM package location. Eliminates the hardcoded ~/.claude/mcp.json path.
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Chat
{
    public static class ChatMcpConfigWriter
    {
        private const string PackageId = "com.unity-mcp.editor";

#if UNITY_INCLUDE_TESTS
        private static string _packageRootOverride;
        internal static void SetPackageRootForTest(string root) => _packageRootOverride = root;
        internal static void ClearPackageRootForTest() => _packageRootOverride = null;
#endif

        /// <summary>
        /// Resolves the UPM package root, honouring the test override.
        /// Shared by <see cref="GetOrCreateConfigPath(string, int)"/> and <c>RelaySpawner</c>
        /// so both Python entrypoints agree on where "local install" lives.
        /// </summary>
        internal static string PackageRoot()
        {
#if UNITY_INCLUDE_TESTS
            if (_packageRootOverride != null) return _packageRootOverride;
#endif
            return Path.GetFullPath($"Packages/{PackageId}");
        }

        /// <summary>Returns per-port config filename. Port=0 falls back to legacy bare name.</summary>
        public static string ConfigFileName(int port) =>
            port > 0 ? $"unity-mcp-config-{port}.json" : "unity-mcp-config.json";

        // ── Pure helpers ──────────────────────────────────────────────────────

        /// <summary>Returns <c>packageRoot/../server</c> normalized. No I/O.</summary>
        public static string DeriveServerPath(string packageRoot) =>
            Path.GetFullPath(Path.Combine(packageRoot.TrimEnd('/', '\\'), "..", "server"));

        /// <summary>Formats the MCP config JSON blob expected by --mcp-config.</summary>
        public static string BuildClaudeConfigJson(string command, string[] args, int port = 0)
        {
            var sb = new StringBuilder();
            sb.Append("{\"mcpServers\":{\"");
            sb.Append(PermissionConfig.SERVER_NAME);
            sb.Append("\":{\"command\":\"");
            sb.Append(JsonHelper.EscapeJson(command));
            sb.Append("\",\"args\":[");
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(JsonHelper.EscapeJson(args[i]));
                sb.Append('"');
            }
            sb.Append(']');
            if (port > 0)
            {
                // UNITY_MCP_CHAT=1 here (not in CLI process env) ensures only THIS server
                // gets it — prevents "unity-kiss" from ~/.mcp.json connecting to chat port.
                sb.Append(",\"env\":{\"UNITY_MCP_PORT\":\"");
                sb.Append(port);
                sb.Append("\",\"UNITY_MCP_CHAT\":\"1\"}");
            }
            sb.Append("}}}");
            return sb.ToString();
        }

        // ── I/O helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the absolute server directory if it contains a pyproject.toml, else null.
        /// </summary>
        public static string ResolveServerDir(string packageRoot)
        {
            var dir = DeriveServerPath(packageRoot);
            return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "pyproject.toml"))
                ? dir
                : null;
        }

        /// <summary>
        /// Resolves the Python command and args for the server.
        /// Resolution order: .venv/Scripts/python.exe (Windows) → .venv/bin/python →
        ///   uv (resolvedUvPath) → python (Windows) / python3 (others).
        /// <para>Pass a pre-resolved absolute uv path (or null) to keep this unit-testable
        /// without depending on the host having uv in PATH.</para>
        /// </summary>
        public static (string command, string[] args) ResolvePythonCommand(string serverDir, string resolvedUvPath)
        {
            // Windows venv: Scripts/python.exe (checked first — File.Exists is pure/cross-platform)
            var winVenvPy = Path.Combine(serverDir, ".venv", "Scripts", "python.exe");
            if (File.Exists(winVenvPy))
                return (winVenvPy, new[] { "-m", "unity_mcp.server" });

            var venvPython = Path.Combine(serverDir, ".venv", "bin", "python");
            if (File.Exists(venvPython))
                return (venvPython, new[] { "-m", "unity_mcp.server" });

            if (!string.IsNullOrEmpty(resolvedUvPath))
                return (resolvedUvPath, new[] { "run", "--directory", serverDir, "unity-mcp" });

            // Platform-aware fallback: python on Windows, python3 elsewhere
            var fallback = SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows
                ? "python" : "python3";
            return (fallback, new[] { "-m", "unity_mcp.server" });
        }

        /// <summary>
        /// Resolves uv's absolute path via the login shell (DRY — delegates to ChatBinaryResolver).
        /// Returns null if uv is not found.
        /// </summary>
        private static string ResolveUvPath() => ChatBinaryResolver.Resolve("uv");

        /// <summary>
        /// Resolves everything, writes the config to a stable temp file, and returns its path.
        /// Returns null (+ LogError) if the server directory cannot be found.
        /// Callers that existed before the port-scoped fix need not change.
        /// </summary>
        public static string GetOrCreateConfigPath() =>
            GetOrCreateConfigPath(Path.GetTempPath(), MCPServer.ServerChatPort);

        /// <summary>
        /// Injectable overload (testability seam). No MCPServer/EditorPrefs references — pure/testable.
        /// Writes unity-mcp-config-{port}.json into configDir and returns the absolute path.
        /// </summary>
        public static string GetOrCreateConfigPath(string configDir, int port)
        {
            var source = InstallSourceDetector.Detect();

            // Non-local install: always use uvx (server available via PyPI)
            if (source != InstallSourceDetector.Source.Local)
            {
                var uvxJson = BuildClaudeConfigJson("uvx",
                    new[] { "--from", WizardConfigWriter.GitInstallUrl, "unity-mcp" },
                    port);
                var uvxPath = Path.Combine(configDir, ConfigFileName(port));
                AtomicWrite(uvxPath, uvxJson);
                return uvxPath;
            }

            var packageRoot = PackageRoot();
            var serverDir   = ResolveServerDir(packageRoot);
            if (serverDir == null)
            {
                Debug.LogError($"[MCP Chat] Server not found at expected path (package={packageRoot}). " +
                               "Check the UPM package is installed correctly.");
                return null;
            }

            const string LastServerDirPref = "UnityMCP_LastServerDir";
            var lastDir = EditorPrefs.GetString(LastServerDirPref, "");
            if (!string.IsNullOrEmpty(lastDir) && lastDir != serverDir)
                Debug.LogWarning($"[MCP] Server directory changed ({lastDir} → {serverDir}). Run: python install.py update");
            EditorPrefs.SetString(LastServerDirPref, serverDir);

            var uvPath      = ResolveUvPath();
            var (cmd, args) = ResolvePythonCommand(serverDir, uvPath);
            var json        = BuildClaudeConfigJson(cmd, args, port);
            var configPath  = Path.Combine(configDir, ConfigFileName(port));
            AtomicWrite(configPath, json);
            return configPath;
        }

        // ── Cleanup helpers ───────────────────────────────────────────────────

        /// <summary>Deletes stale per-port config files older than maxAgeHours. Best-effort.</summary>
        public static void CleanupStaleConfigs(string configDir = null, double maxAgeHours = 2.0)
        {
            var dir    = configDir ?? Path.GetTempPath();
            var cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);
            foreach (var pattern in new[] { "unity-mcp-config-*.json", "opencode-unity-mcp-*.json",
                                            "unity-mcp-config.json", "opencode-unity-mcp.json" })
                foreach (var f in Directory.GetFiles(dir, pattern))
                    try { if (File.GetLastWriteTimeUtc(f) < cutoff) File.Delete(f); } catch { }
        }

        /// <summary>Deletes the config file for a specific port. Best-effort.</summary>
        public static void DeleteConfig(string configDir, int port)
        {
            try { File.Delete(Path.Combine(configDir ?? Path.GetTempPath(), ConfigFileName(port))); }
            catch { }
        }

        /// <summary>Deletes the config file for the current server's chat port. Best-effort.</summary>
        public static void DeleteOwnConfig() =>
            DeleteConfig(Path.GetTempPath(), MCPServer.ServerChatPort);

        // ── Private helpers ───────────────────────────────────────────────────

        private static void AtomicWrite(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, JsonHelper.Utf8NoBom);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
