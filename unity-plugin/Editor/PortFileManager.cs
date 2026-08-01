using System;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Port resolution + discovery-file I/O extracted from MCPServer (Phase 2, M1).
    // Owns MCP_Port.json / MCPSettings.json / ~/.unity-biome-mcp/{ports,state} writes and
    // the resolved Port/ChatPort values shared across the server.
    internal static class PortFileManager
    {
        private static readonly string PortFilePath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Port.json"));
        private static readonly string _projectSettingsPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "MCPSettings.json"));
        private static int _port;
        private static int _chatPort;
        private static bool _portsResolved;

#if UNITY_INCLUDE_TESTS
        internal static void ResetForTests() { _port = 0; _chatPort = 0; _portsResolved = false; }
#endif

        private static string ReadPortFileOrNull()
        {
            try { return File.Exists(PortFilePath) ? File.ReadAllText(PortFilePath) : null; }
            catch { return null; }
        }

        private static string ReadProjectSettingsOrNull()
        {
            try { return File.Exists(_projectSettingsPath) ? File.ReadAllText(_projectSettingsPath) : null; }
            catch { return null; }
        }

        private static void EnsurePorts()
        {
            if (_portsResolved) return;
            var env = Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
            var projectJson = ReadProjectSettingsOrNull();
            var cacheJson = ReadPortFileOrNull();
            _port = PortResolver.ResolvePort(env, projectJson, cacheJson, 9500);
            var chatEnv = Environment.GetEnvironmentVariable("UNITY_MCP_CHAT_PORT");
            _chatPort = PortResolver.ResolveChatPort(chatEnv, projectJson, cacheJson, _port, _port + 1);
            _portsResolved = true;
        }

        internal static int Port { get { EnsurePorts(); return _port; } }
        internal static int ChatPort { get { EnsurePorts(); return _chatPort; } }

        // Reads reloadPort from MCP_Port.json. Returns 0 if reload-package is not installed.
        internal static int ServerReloadPort => PortResolver.ReadReloadPort(PortFilePath);

        internal static void SavePorts(int port, int chatPort)
        {
            PortResolver.SavePorts(PortFilePath, port, chatPort);
            PortResolver.SaveProjectSettings(_projectSettingsPath, port, chatPort);
            _port = port;
            _chatPort = chatPort;
            _portsResolved = true;
        }

        // Fallback-only save: updates runtime files (MCP_Port.json + {pid}.port) but NOT
        // MCPSettings.json (user intent). Prevents cascade port drift on Windows reload.
        internal static void SaveRuntimePorts(int port, int chatPort)
        {
            PortResolver.SavePorts(PortFilePath, port, chatPort);
            _port = port;
            _chatPort = chatPort;
            _portsResolved = true;
            WritePortFile(port);
        }

        // Removes port files from dead PIDs to prevent stale discovery entries accumulating
        // after hard crashes. Called once at startup before WritePortFile().
        internal static void CleanStalePeerPortFiles()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp", "ports");
            CleanStalePeerPortFiles(dir);
        }

        // Testable overload: accepts the ports directory so tests can pass a temp path.
        // Iterates all files — handles *.port, *.reload-port, *.chat-port from dead PIDs.
        internal static void CleanStalePeerPortFiles(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var file in Directory.GetFiles(dir))
                {
                    var name = Path.GetFileName(file);
                    var dot = name.IndexOf('.');
                    if (dot < 0) continue;
                    if (!int.TryParse(name.Substring(0, dot), out var pid) || pid == currentPid) continue;
                    try { System.Diagnostics.Process.GetProcessById(pid); }  // alive — skip
                    catch (ArgumentException) { try { File.Delete(file); } catch { } }
                }
            }
            catch { }
        }

        internal static void WritePortFile(int port)
        {
            try
            {
                var cachePath = Path.Combine(Application.temporaryCachePath, "mcp_port.txt");
                File.WriteAllText(cachePath, port.ToString());
            }
            catch { }
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-biome-mcp", "ports");
                Directory.CreateDirectory(dir);
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var project = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
                var info = $"{port}\n{Path.GetDirectoryName(Application.dataPath)}\n{project}";
                File.WriteAllText(Path.Combine(dir, $"{pid}.port"), info);
                if (_chatPort > 0)
                {
                    var chatInfo = $"{_chatPort}\n{Path.GetDirectoryName(Application.dataPath)}\n{project}";
                    File.WriteAllText(Path.Combine(dir, $"{pid}.chat-port"), chatInfo);
                }
            }
            catch (Exception e) { Debug.LogWarning($"[MCP] Could not write discovery file: {e.Message}"); }
        }

        internal static void DeletePortFile()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-biome-mcp", "ports");
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var path = Path.Combine(dir, $"{pid}.port");
                if (File.Exists(path)) File.Delete(path);
                var chatPath = Path.Combine(dir, $"{pid}.chat-port");
                if (File.Exists(chatPath)) File.Delete(chatPath);
            }
            catch { }
        }

        internal static void WriteStateFile(string state)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-biome-mcp", "state");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"port-{Port}.state");
                var tmp = path + ".tmp";
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var epoch = SyncHelper.CurrentEpoch;
                // Invariant culture so decimal separator is always '.'
                File.WriteAllText(tmp,
                    $"{state}\n{ts.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n{pid}\n{epoch}");
                // Unity editor scripting is still Mono/netstandard2.1 — no
                // File.Move(string,string,bool) overload (CS1739). Delete+Move:
                // tiny non-atomic window, readers retry so it's acceptable.
                try { File.Delete(path); } catch { }
                File.Move(tmp, path);
            }
            catch { }
        }

        internal static void DeleteStateFile()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".unity-biome-mcp", "state", $"port-{Port}.state");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
