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

        internal static bool? ParseBoolFromJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)");
            if (m.Success) return m.Groups[1].Value == "true";
            return null;
        }

        internal static bool IsValidPort(int port) => port >= 1024 && port <= 65535;

        // Retry-loop off-by-one helpers (ARC-8 T1). attempt is 0-based; maxAttempts
        // is the same-port retry budget. attempt == maxAttempts is the one fallback
        // iteration that follows the exhausted same-port budget.
        internal static bool IsSamePortAttempt(int attempt, int maxAttempts) => attempt < maxAttempts;

        internal static int BackoffDelayMs(int attemptIndex, int baseDelayMs) => baseDelayMs * (attemptIndex + 1);

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

        // Scan startFrom..startFrom+199, skip TWO ports, OS-assigned fallback.
        // Best-effort probe: TOCTOU window remains (port released before caller binds).
        private static int FindFreePortExcluding(int startFrom, int skip1, int skip2)
        {
            for (var port = startFrom; port <= startFrom + 199; port++)
            {
                if (port == skip1 || port == skip2) continue;
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, port);
                    l.Start(); l.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            // OS-assigned fallback — one retry if OS gives us a skip port
            var fb = new TcpListener(IPAddress.Loopback, 0);
            fb.Start(); var p = ((IPEndPoint)fb.LocalEndpoint).Port; fb.Stop();
            if (p != skip1 && p != skip2) return p;
            fb = new TcpListener(IPAddress.Loopback, 0);
            fb.Start(); p = ((IPEndPoint)fb.LocalEndpoint).Port; fb.Stop();
            return p;
        }

        // Return an already-started TcpListener on a free port. Caller owns Stop().
        // Applies platform socket options (mirrors MCPServer.StartAsync bind logic).
        internal static TcpListener BindFreePort(int startFrom, int skipPort = -1, int skipPort2 = -1)
        {
            for (var port = startFrom; port <= 9699; port++)
            {
                if (port == skipPort || port == skipPort2) continue;
                var l = TryBindWithOptions(port);
                if (l != null) return l;
            }
            // OS-assigned fallback
            var fb = TryBindWithOptions(0);
            if (fb != null)
            {
                var assigned = ((IPEndPoint)fb.LocalEndpoint).Port;
                if (assigned != skipPort && assigned != skipPort2) return fb;
                fb.Stop();
            }
            var fb2 = TryBindWithOptions(0);
            if (fb2 != null) return fb2;
            // Should never reach here; last-resort plain bind
            var last = new TcpListener(IPAddress.Loopback, 0);
            last.Start();
            return last;
        }

        private static TcpListener TryBindWithOptions(int port)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                ApplySocketOptions(l);
                l.Start();
                return l;
            }
            catch (SocketException) { return null; }
        }

        private static void ApplySocketOptions(TcpListener l)
        {
#if UNITY_EDITOR_WIN
            l.Server.ExclusiveAddressUse = true;
#else
            l.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if UNITY_EDITOR_OSX
            try { l.Server.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x0200, true); } catch { }
#endif
#endif
        }

        // env → cache (validate != mainPort AND != chatPort) → FindFreePortExcluding
        internal static int ResolveReloadPort(
            string envValue, string cacheJson, int mainPort, int chatPort, int defaultStart)
        {
            if (envValue != null && int.TryParse(envValue, out var ep) && IsValidPort(ep))
                return ep;
            var fromCache = ParsePortFromJson(cacheJson, "reloadPort");
            if (fromCache.HasValue && IsValidPort(fromCache.Value)
                && fromCache.Value != mainPort && fromCache.Value != chatPort)
                return fromCache.Value;
            return FindFreePortExcluding(defaultStart, mainPort, chatPort);
        }

        // Atomic tmp+move write of all three port fields. No merge-read.
        internal static bool TrySaveAllPorts(
            string filePath, int port, int chatPort, int reloadPort,
            System.Action<string, string> writeAllText)
        {
            var tmp = filePath + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                var json = $"{{\"port\":{port},\"chatPort\":{chatPort},\"reloadPort\":{reloadPort}}}";
                writeAllText(tmp, json);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                System.IO.File.Move(tmp, filePath);
                return true;
            }
            catch
            {
                try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch { }
                return false;
            }
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
                // Merge-write: preserve readOnly flag when ports are reassigned via wizard.
                string existing = null;
                try { if (System.IO.File.Exists(filePath)) existing = System.IO.File.ReadAllText(filePath); }
                catch { }
                var readOnly = ParseBoolFromJson(existing, "readOnly");
                var json = readOnly.HasValue
                    ? $"{{\"port\":{port},\"chatPort\":{chatPort},\"readOnly\":{(readOnly.Value ? "true" : "false")}}}"
                    : $"{{\"port\":{port},\"chatPort\":{chatPort}}}";
                writeAllText(filePath, json);
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
