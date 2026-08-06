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
            if (!PortResolver.TrySaveProjectSettings(_projectSettingsPath, port, chatPort, File.WriteAllText))
                Debug.LogWarning("[MCP] Could not save port intent to ProjectSettings — changes may not survive a Library purge.");
            _port = port;
            _chatPort = chatPort;
            _portsResolved = true;
        }

        // Fallback-only save: updates runtime files (MCP_Port.json + {pid}.port) but NOT
        // MCPSettings.json (user intent). Prevents cascade port drift on Windows reload.
        internal static void SaveRuntimePorts(int port, int chatPort)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            var persisted = SaveRuntimePortsCore(
                port,
                chatPort,
                PortFilePath,
                Path.Combine(Application.temporaryCachePath, "mcp_port.txt"),
                DefaultDiscoveryDirectory(),
                System.Diagnostics.Process.GetCurrentProcess().Id,
                projectPath,
                Path.GetFileName(projectPath),
                File.WriteAllText,
                CommitResolvedPorts);
            if (!persisted)
                Debug.LogWarning("[MCP] Runtime port persistence failed; keeping the previous resolved ports.");
        }

        // Path-injected persistence core. Tests exercise runtime persistence through this
        // method so they cannot overwrite the live editor's cache or discovery endpoint.
        internal static bool SaveRuntimePortsCore(
            int port,
            int chatPort,
            string runtimePortFilePath,
            string temporaryCacheFilePath,
            string discoveryDirectory,
            int processId,
            string projectPath,
            string projectName)
            => SaveRuntimePortsCore(
                port,
                chatPort,
                runtimePortFilePath,
                temporaryCacheFilePath,
                discoveryDirectory,
                processId,
                projectPath,
                projectName,
                File.WriteAllText,
                null);

        // The writer and commit callback are injected so failure paths are deterministic in
        // tests and the production cache update cannot drift from persisted discovery data.
        internal static bool SaveRuntimePortsCore(
            int port,
            int chatPort,
            string runtimePortFilePath,
            string temporaryCacheFilePath,
            string discoveryDirectory,
            int processId,
            string projectPath,
            string projectName,
            Action<string, string> writeAllText,
            Action<int, int> commitResolvedPorts)
        {
            if (writeAllText == null ||
                !PortResolver.TrySavePorts(
                    runtimePortFilePath, port, chatPort, writeAllText))
                return false;

            if (!WriteRuntimePortFiles(
                port,
                chatPort,
                temporaryCacheFilePath,
                discoveryDirectory,
                processId,
                projectPath,
                projectName,
                writeAllText))
                return false;

            commitResolvedPorts?.Invoke(port, chatPort);
            return true;
        }

        private static void CommitResolvedPorts(int port, int chatPort)
        {
            _port = port;
            _chatPort = chatPort;
            _portsResolved = true;
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
                    if (IsProcessAlive(pid)) continue;
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pid))
                    return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        internal static void WritePortFile(int port)
        {
            var projectPath = Path.GetDirectoryName(Application.dataPath);
            WriteRuntimePortFiles(
                port,
                _chatPort,
                Path.Combine(Application.temporaryCachePath, "mcp_port.txt"),
                DefaultDiscoveryDirectory(),
                System.Diagnostics.Process.GetCurrentProcess().Id,
                projectPath,
                Path.GetFileName(projectPath),
                File.WriteAllText);
        }

        private static bool WriteRuntimePortFiles(
            int port,
            int chatPort,
            string temporaryCacheFilePath,
            string discoveryDirectory,
            int processId,
            string projectPath,
            string projectName,
            Action<string, string> writeAllText)
        {
            var succeeded = true;
            try
            {
                var cacheDirectory = Path.GetDirectoryName(temporaryCacheFilePath);
                if (!string.IsNullOrEmpty(cacheDirectory))
                    Directory.CreateDirectory(cacheDirectory);
                writeAllText(temporaryCacheFilePath, port.ToString());
            }
            catch (Exception e)
            {
                succeeded = false;
                Debug.LogWarning($"[MCP] Could not write runtime port cache: {e.Message}");
            }
            try
            {
                Directory.CreateDirectory(discoveryDirectory);
                var info = $"{port}\n{projectPath}\n{projectName}";
                writeAllText(Path.Combine(discoveryDirectory, $"{processId}.port"), info);
                if (chatPort > 0)
                {
                    var chatInfo = $"{chatPort}\n{projectPath}\n{projectName}";
                    writeAllText(
                        Path.Combine(discoveryDirectory, $"{processId}.chat-port"),
                        chatInfo);
                }
            }
            catch (Exception e)
            {
                succeeded = false;
                Debug.LogWarning($"[MCP] Could not write discovery file: {e.Message}");
            }
            return succeeded;
        }

        private static string DefaultDiscoveryDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unity-biome-mcp", "ports");

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
            WriteStateFile(state, DefaultStateDirectory());
        }

        internal static void WriteStateFile(string state, string stateDirectory)
        {
            try
            {
                Directory.CreateDirectory(stateDirectory);
                var path = StateFilePath(stateDirectory);
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
            DeleteStateFile(DefaultStateDirectory());
        }

        internal static void DeleteStateFile(string stateDirectory)
        {
            try
            {
                var path = StateFilePath(stateDirectory);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        internal static string StateFilePath(string stateDirectory) =>
            Path.Combine(stateDirectory, $"port-{Port}.state");

        internal static string DefaultStateDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unity-biome-mcp", "state");
    }
}
