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
        public readonly int  BridgeCount;  // count of server-{port}-*.lock files

        public ServerInfo(int port, int pid, bool alive, bool isCurrentProject, int bridgeCount = 0)
        {
            Port = port; Pid = pid; Alive = alive; IsCurrentProject = isCurrentProject;
            BridgeCount = bridgeCount;
        }
    }

    internal readonly struct McpConnectionInfo
    {
        public readonly int  BridgePid;
        public readonly bool BridgeAlive;
        public McpConnectionInfo(int pid, bool alive) { BridgePid = pid; BridgeAlive = alive; }
    }

    internal readonly struct UnityServerInfo
    {
        public readonly int  Port;
        public readonly int  UnityPid;
        public readonly bool IsCurrentProject;
        public readonly IReadOnlyList<McpConnectionInfo> Connections;
        public readonly int  LiveTcpCount;

        public UnityServerInfo(int port, int unityPid, bool isCurrent,
            IReadOnlyList<McpConnectionInfo> connections, int liveTcp)
        {
            Port = port; UnityPid = unityPid; IsCurrentProject = isCurrent;
            Connections = connections; LiveTcpCount = liveTcp;
        }
    }

    internal static class McpServerScanner
    {
        internal static string OverrideScanDir;                     // for tests: ports/ directory
        internal static string OverrideLockDir;                     // for tests: lock file directory
        internal static Func<int, int> OverrideLiveTcpCountGetter; // for tests: inject TCP count

        internal static IReadOnlyList<UnityServerInfo> ScanDetailed()
        {
            var (scanDir, lockDir) = GetDirs();
            if (!Directory.Exists(scanDir)) return new List<UnityServerInfo>();

            var results = new List<UnityServerInfo>();
            foreach (var portFile in Directory.GetFiles(scanDir, "*.port"))
            {
                var firstLine = File.ReadAllText(portFile).Split('\n')[0].Trim();
                if (!int.TryParse(firstLine, out var port)) continue;

                var connections = FindConnections(lockDir, port);
                var isCurrent = port == MCPServer.ServerPort;
                int unityPid = 0;
                var fileName = Path.GetFileNameWithoutExtension(portFile);
                if (int.TryParse(fileName, out var filePid)) unityPid = filePid;
                var liveTcp = isCurrent ? GetLiveTcpCount(port) : 0;
                results.Add(new UnityServerInfo(port, unityPid, isCurrent, connections, liveTcp));
            }
            return results;
        }

        internal static IReadOnlyList<ServerInfo> Scan()
        {
            var detailed = ScanDetailed();
            var results = new List<ServerInfo>(detailed.Count);
            foreach (var us in detailed)
            {
                // First-alive-BridgePid strategy preserved for backward compat
                int pid = 0; bool alive = false;
                foreach (var c in us.Connections)
                {
                    if (c.BridgeAlive) { pid = c.BridgePid; alive = true; break; }
                }
                if (pid == 0 && us.Connections.Count > 0) pid = us.Connections[0].BridgePid;
                if (pid == 0 && us.UnityPid != 0)
                {
                    // Backward-compat fallback: use PID from filename and check liveness
                    pid = us.UnityPid;
                    alive = IsProcessAlive(pid);
                }
                results.Add(new ServerInfo(us.Port, pid, alive, us.IsCurrentProject,
                    us.Connections.Count));
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
                    if (!int.TryParse(name, out var pid)) continue;
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

        private static IReadOnlyList<McpConnectionInfo> FindConnections(string lockDir, int port)
        {
            if (!Directory.Exists(lockDir)) return new List<McpConnectionInfo>();
            var locks = Directory.GetFiles(lockDir, $"server-{port}-*.lock");
            var result = new List<McpConnectionInfo>(locks.Length);
            foreach (var lf in locks)
            {
                var parts = Path.GetFileNameWithoutExtension(lf).Split('-');
                if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 1], out var pid)) continue;
                result.Add(new McpConnectionInfo(pid, IsProcessAlive(pid)));
            }
            result.Sort((a, b) => a.BridgePid.CompareTo(b.BridgePid));
            return result;
        }

        private static int GetLiveTcpCount(int port)
        {
            if (OverrideLiveTcpCountGetter != null) return OverrideLiveTcpCountGetter(port);
            // Lightweight: same-process only; 0 for other projects
            return MCPServer.ConnectedClientCount;
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
