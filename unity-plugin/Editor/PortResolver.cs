using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static class PortResolver
    {
        // 4-arg: env → projectSettings → cache → FindFreePort
        internal static int ResolvePort(string envValue, string projectJson, string cacheJson, int defaultStart)
        {
            if (envValue != null && int.TryParse(envValue, out var p) && IsValidPort(p)) return p;
            var fromProject = ParsePortFromJson(projectJson, "port");
            if (fromProject.HasValue && IsValidPort(fromProject.Value)) return fromProject.Value;
            var fromCache = ParsePortFromJson(cacheJson, "port");
            if (fromCache.HasValue && IsValidPort(fromCache.Value)) return fromCache.Value;
            return FindFreePort(defaultStart);
        }

        // 3-arg: backward compat — no projectSettings layer
        internal static int ResolvePort(string envValue, string cacheJson, int defaultStart)
            => ResolvePort(envValue, null, cacheJson, defaultStart);

        // 5-arg: env → projectSettings → cache → FindFreePort
        // Collision guard: if resolved port == mainPort, fall back to FindFreePort.
        internal static int ResolveChatPort(string envValue, string projectJson, string cacheJson, int mainPort, int defaultStart)
        {
            if (envValue != null && int.TryParse(envValue, out var p) && IsValidPort(p))
                return p == mainPort ? FindFreePort(defaultStart, skipPort: mainPort) : p;
            var fromProject = ParsePortFromJson(projectJson, "chatPort");
            if (fromProject.HasValue && IsValidPort(fromProject.Value))
                return fromProject.Value == mainPort ? FindFreePort(defaultStart, skipPort: mainPort) : fromProject.Value;
            var fromCache = ParsePortFromJson(cacheJson, "chatPort");
            if (fromCache.HasValue && IsValidPort(fromCache.Value))
                return fromCache.Value == mainPort ? FindFreePort(defaultStart, skipPort: mainPort) : fromCache.Value;
            return FindFreePort(defaultStart, skipPort: mainPort);
        }

        // 4-arg: backward compat — no projectSettings layer
        internal static int ResolveChatPort(string envValue, string cacheJson, int mainPort, int defaultStart)
            => ResolveChatPort(envValue, null, cacheJson, mainPort, defaultStart);

        internal static int? ParsePortFromJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var val)) return val;
            return null;
        }

        internal static bool IsValidPort(int port) => port >= 1024 && port <= 65535;

        internal static int FindFreePort(int startFrom, int skipPort = -1)
        {
            for (var port = startFrom; port <= 9699; port++)
            {
                if (port == skipPort) continue;
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            var fb = new TcpListener(IPAddress.Loopback, 0);
            fb.Start();
            var assigned = ((IPEndPoint)fb.LocalEndpoint).Port;
            fb.Stop();
            if (assigned == skipPort)
            {
                fb = new TcpListener(IPAddress.Loopback, 0);
                fb.Start();
                assigned = ((IPEndPoint)fb.LocalEndpoint).Port;
                fb.Stop();
            }
            return assigned;
        }

        internal static void SavePorts(string filePath, int port, int chatPort)
            => TrySavePorts(filePath, port, chatPort, System.IO.File.WriteAllText);

        internal static bool TrySavePorts(
            string filePath,
            int port,
            int chatPort,
            System.Action<string, string> writeAllText)
        {
            var tmp = filePath + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                // Merge-write: preserve reloadPort written by reload-package (if present).
                string existing = null;
                try { if (System.IO.File.Exists(filePath)) existing = System.IO.File.ReadAllText(filePath); }
                catch { }
                var reloadPort = ParsePortFromJson(existing, "reloadPort");
                var json = reloadPort.HasValue
                    ? $"{{\"port\":{port},\"chatPort\":{chatPort},\"reloadPort\":{reloadPort.Value}}}"
                    : $"{{\"port\":{port},\"chatPort\":{chatPort}}}";
                writeAllText(tmp, json);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                System.IO.File.Move(tmp, filePath);
                return true;
            }
            catch
            {
                try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); }
                catch { }
                return false;
            }
        }

        // Write user intent to ProjectSettings/MCPSettings.json (survives Library purge).
        internal static void SaveProjectSettings(string filePath, int port, int chatPort)
            => TrySaveProjectSettings(filePath, port, chatPort, System.IO.File.WriteAllText);

        internal static bool TrySaveProjectSettings(
            string filePath,
            int port,
            int chatPort,
            System.Action<string, string> writeAllText)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                writeAllText(filePath, $"{{\"port\":{port},\"chatPort\":{chatPort}}}");
                return true;
            }
            catch { return false; }
        }

        // Reads reloadPort from MCP_Port.json. Returns 0 if absent or file missing.
        public static int ReadReloadPort(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return 0;
                var json = System.IO.File.ReadAllText(filePath);
                var val = ParsePortFromJson(json, "reloadPort");
                return val ?? 0;
            }
            catch { return 0; }
        }
    }
}
