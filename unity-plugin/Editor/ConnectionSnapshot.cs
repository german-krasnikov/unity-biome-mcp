using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnityMCP.Editor
{
    /// <summary>Lightweight descriptor for a bridge process with no active TCP connection.</summary>
    internal readonly struct DormantInfo
    {
        internal readonly int    BridgePid;
        internal readonly string Kind;  // "Unknown" — no protocol data without a connection
        internal readonly string Cwd;   // null — not available without a connection
        internal DormantInfo(int pid) { BridgePid = pid; Kind = "Unknown"; Cwd = null; }
    }

    /// <summary>
    /// Scans lock files for bridge processes that hold a lock file for the given port
    /// but do NOT have an active TCP slot (i.e. dormant bridges).
    /// </summary>
    internal static class DormantBridgeScanner
    {
        // Test seam: override to isolate from real ~/.unity-biome-mcp in tests.
        internal static string OverrideLockDir;

        internal static IReadOnlyList<DormantInfo> Scan(int port, IReadOnlyList<int> activePids)
        {
            var lockDir = OverrideLockDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp");
            if (!Directory.Exists(lockDir)) return new List<DormantInfo>();

            var result = new List<DormantInfo>();
            foreach (var lockFile in Directory.GetFiles(lockDir, $"server-{port}-*.lock"))
            {
                var parts = Path.GetFileNameWithoutExtension(lockFile).Split('-');
                if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 1], out var pid))
                    continue;
                if (!IsAlive(pid)) continue;
                if (ContainsPid(activePids, pid)) continue;
                result.Add(new DormantInfo(pid));
            }
            return result;
        }

        private static bool ContainsPid(IReadOnlyList<int> list, int pid)
        {
            foreach (var p in list) if (p == pid) return true;
            return false;
        }

        private static bool IsAlive(int pid)
        {
            try { using var p = Process.GetProcessById(pid); return !p.HasExited; }
            catch { return false; }
        }
    }
}
