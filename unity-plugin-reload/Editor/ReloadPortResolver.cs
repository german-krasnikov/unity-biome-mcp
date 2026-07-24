// ReloadPortResolver — port discovery and persistence for the reload mini-server.
// No dep on UnityMCP.Editor. All public (CS0122 avoidance).
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityMCP.Reload
{
    public static class ReloadPortResolver
    {
        // Overridable for tests (set before calling MergePersist).
        public static string PortFilePath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Port.json"));

        // Overridable for tests (set before calling WriteReloadPortFile).
        public static string PortsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp", "ports");

        // Scan [startFrom..startFrom+100], fallback to OS-assigned port.
        public static int FindFreePort(int startFrom)
        {
            for (var port = startFrom; port <= startFrom + 100; port++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, port);
                    l.Start(); l.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            var fb = new TcpListener(IPAddress.Loopback, 0);
            fb.Start();
            var assigned = ((IPEndPoint)fb.LocalEndpoint).Port;
            fb.Stop();
            return assigned;
        }

        // env UNITY_MCP_RELOAD_PORT → Library/MCP_Port.json["reloadPort"] → FindFreePort(9600)
        public static int GetReloadPort()
        {
            var env = Environment.GetEnvironmentVariable("UNITY_MCP_RELOAD_PORT");
            if (env != null && int.TryParse(env, out var ep) && ep >= 1024 && ep <= 65535)
                return ep;

            try
            {
                if (File.Exists(PortFilePath))
                {
                    var json = File.ReadAllText(PortFilePath);
                    var m = Regex.Match(json, "\"reloadPort\"\\s*:\\s*(\\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var sp) && sp >= 1024 && sp <= 65535)
                        return sp;
                }
            }
            catch { }

            return FindFreePort(9600);
        }

        // Read MCP_Port.json, add/update "reloadPort", write back preserving other fields.
        // Approach: parse all known keys → rebuild JSON from scratch (no string surgery).
        public static void MergePersist(int reloadPort)
        {
            try
            {
                var path = PortFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                string existing = null;
                try { if (File.Exists(path)) existing = File.ReadAllText(path); }
                catch { }

                var mainPort  = ParseInt(existing, "port");
                var chatPort  = ParseInt(existing, "chatPort");

                var json = BuildPortJson(mainPort, chatPort, reloadPort);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch { }
        }

        // ParseInt + BuildPortJson below intentionally duplicate PortResolver.ParsePortFromJson /
        // BuildPortJson from UnityMCP.Editor. Reason: asmdef references:[] — the reload package
        // must compile standalone even when the main plugin is broken, so cross-assembly import is
        // forbidden. Any logic change here must be mirrored in PortResolver.cs by hand.

        // Extract int value for key from JSON string via regex. Returns null on any failure.
        private static int? ParseInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var val)) return val;
            return null;
        }

        // Build {"port":X,"chatPort":Y,"reloadPort":Z} — omits keys whose value is null.
        private static string BuildPortJson(int? port, int? chatPort, int? reloadPort)
        {
            var sb = new System.Text.StringBuilder("{");
            var sep = "";
            if (port.HasValue)      { sb.Append(sep).Append("\"port\":").Append(port.Value);       sep = ","; }
            if (chatPort.HasValue)  { sb.Append(sep).Append("\"chatPort\":").Append(chatPort.Value); sep = ","; }
            if (reloadPort.HasValue){ sb.Append(sep).Append("\"reloadPort\":").Append(reloadPort.Value); }
            sb.Append("}");
            return sb.ToString();
        }

        // Write ~/.unity-biome-mcp/ports/{pid}.reload-port = "port\nProjectDir\nProjectName"
        // F2: mirrors MCPServer.WritePortFile format for CWD-based disambiguation.
        public static void WriteReloadPortFile(int pid, int port,
            string projectDir = "", string projectName = "")
        {
            try
            {
                Directory.CreateDirectory(PortsDir);
                var content = $"{port}\n{projectDir}\n{projectName}";
                File.WriteAllText(Path.Combine(PortsDir, $"{pid}.reload-port"), content);
            }
            catch { }
        }

        // Delete ~/.unity-biome-mcp/ports/{pid}.reload-port
        public static void DeleteReloadPortFile(int pid)
        {
            try
            {
                var path = Path.Combine(PortsDir, $"{pid}.reload-port");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
