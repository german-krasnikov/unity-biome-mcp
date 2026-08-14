// T23: Compute stable project fingerprint — mirrors Python sha256[:12] formula.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnityMCP.Editor.Chat.CLI
{
    internal static class ProjectFingerprint
    {
        // Test seam — override in tests.
        internal static Func<string> GetProjectId = ComputeProjectId;

        internal static string Compute()
        {
            var projectId = GetProjectId();
            return Sha256Hex(projectId).Substring(0, 12);
        }

        internal static string BiomeHistoryDir()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".unity-biome-mcp", "projects", Compute(), "history");
        }

        private static string ComputeProjectId()
        {
            var cloudId = UnityEditor.PlayerSettings.cloudProjectId;
            if (!string.IsNullOrEmpty(cloudId)) return cloudId;
            var root = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Sha256Hex(root).Substring(0, 12);
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
