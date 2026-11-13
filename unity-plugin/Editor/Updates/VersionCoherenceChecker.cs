using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor
{
    internal static class VersionCoherenceChecker
    {
        static readonly Protocol Proto = ProtocolRows.All[0];

#if UNITY_INCLUDE_TESTS
        /// <summary>Override config path for tests. Set to null to use real path.</summary>
        internal static string _testConfigPath;

        private static string GetActiveConfigPath() =>
            _testConfigPath ?? GetActiveConfigPathImpl();
#else
        private static string GetActiveConfigPath() => GetActiveConfigPathImpl();
#endif

        /// <summary>Returns "X.Y.Z" if the MCP config pins @vX.Y.Z, null if unpinned (HEAD).</summary>
        internal static string GetServerPinnedRef()
        {
            var configPath = GetActiveConfigPath();
            if (configPath == null || !File.Exists(configPath)) return null;
            try
            {
                var json = File.ReadAllText(configPath);
                var m = Regex.Match(json, $@"@v({Proto.ServerRefPattern})#subdirectory={Proto.InstallSubdir}");
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }

        /// <summary>Coherent = both on same version, or server is unpinned (HEAD = latest).</summary>
        internal static bool IsCoherent(string pluginVersion, string serverRef) =>
            serverRef == null || serverRef == pluginVersion;

        private static string GetActiveConfigPathImpl()
        {
            // Project-scoped first (most specific)
            var projectLocal = Path.Combine(Application.dataPath, "..", ".mcp.json");
            if (File.Exists(projectLocal)) return Path.GetFullPath(projectLocal);
            // Global claude-code
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var claudeCode = Path.Combine(home, ".claude.json");
            if (File.Exists(claudeCode)) return claudeCode;
            return null;
        }
    }
}
