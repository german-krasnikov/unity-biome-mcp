using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnityMCP.Editor
{
    internal readonly struct ServerInfo
    {
        public readonly int  Port;
        public readonly int  Pid;
        public readonly bool Alive;
        public readonly bool IsCurrentProject;

        public ServerInfo(int port, int pid, bool alive, bool isCurrentProject)
        {
            Port = port; Pid = pid; Alive = alive; IsCurrentProject = isCurrentProject;
        }
    }

    internal static class McpServerScanner
    {
        internal static string OverrideScanDir; // for tests: ports/ directory
        internal static string OverrideLockDir; // for tests: lock file directory (root level)

        internal static IReadOnlyList<ServerInfo> Scan()
        {
            var (scanDir, lockDir) = GetDirs();

            if (!Directory.Exists(scanDir))
                return new List<ServerInfo>();

            var results = new List<ServerInfo>();
            foreach (var portFile in Directory.GetFiles(scanDir, "*.port"))
            {
                var fileName = Path.GetFileNameWithoutExtension(portFile);
                var firstLine = File.ReadAllText(portFile).Split('\n')[0].Trim();
                if (!int.TryParse(firstLine, out var port)) continue;

                var (pid, alive) = FindLock(lockDir, port);
                if (pid == 0 && int.TryParse(fileName, out var filePid))
                    (pid, alive) = (filePid, IsProcessAlive(filePid));
                results.Add(new ServerInfo(port, pid, alive, port == MCPServer.ServerPort));
            }
            return results;
        }

        internal static void CleanPhantomFiles()
        {
            var (scanDir, lockDir) = GetDirs();

            if (Directory.Exists(scanDir))
            {
                foreach (var portFile in Directory.GetFiles(scanDir, "*.port"))
                {
                    var name = Path.GetFileNameWithoutExtension(portFile);
                    if (!int.TryParse(name, out var pid)) continue; // skip .chat/.reload variants
                    if (IsProcessAlive(pid)) continue;

                    TryDelete(portFile);
                    TryDelete(Path.Combine(scanDir, name + ".chat-port"));
                    TryDelete(Path.Combine(scanDir, name + ".reload-port"));
                }
            }

            if (Directory.Exists(lockDir))
            {
                foreach (var lockFile in Directory.GetFiles(lockDir, "server-*.lock"))
                {
                    var parts = Path.GetFileNameWithoutExtension(lockFile).Split('-');
                    if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 1], out var pid)) continue;
                    if (!IsProcessAlive(pid))
                        TryDelete(lockFile);
                }
            }
        }

        private static (string scanDir, string lockDir) GetDirs()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mcpRoot = Path.Combine(home, ".unity-biome-mcp");
            var scanDir = OverrideScanDir ?? Path.Combine(mcpRoot, "ports");
            var lockDir = OverrideLockDir ?? mcpRoot;
            return (scanDir, lockDir);
        }

        private static (int pid, bool alive) FindLock(string lockDir, int port)
        {
            if (!Directory.Exists(lockDir)) return (0, false);

            var locks = Directory.GetFiles(lockDir, $"server-{port}-*.lock");
            if (locks.Length == 0) return (0, false);

            var parts = Path.GetFileNameWithoutExtension(locks[0]).Split('-');
            if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 1], out var pid))
                return (0, false);

            return (pid, IsProcessAlive(pid));
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch { return false; }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}
