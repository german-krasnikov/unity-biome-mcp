using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>Shared MCP action methods — used by status window and status bar widget.</summary>
    internal static class MCPActions
    {
        internal enum TerminateResult { Killed, Stale, NotFound }

        // Test seam: override to isolate KillAll from real ~/.unity-biome-mcp in tests.
        internal static string OverrideLockDir;

        internal static void Restart()
        {
            MCPServer.Stop();
            MCPServer.StartAsync();
        }

        internal static void RestartRelay()
        {
            InvokeRelay("Stop");
            EditorApplication.delayCall += () => {
                try { InvokeRelay("EnsureRunning"); }
                catch { /* status bar reflects result on next PulseTick */ }
            };
        }

        internal static void Kill() => KillCurrent();

        internal static void KillCurrent()
        {
            if (MCPServer.ServerPort == 0) return;  // server not started
            var dir = GetLockDir();
            if (!Directory.Exists(dir)) return;
            var pattern = $"server-{MCPServer.ServerPort}-*.lock";
            int killed = 0, stale = 0;
            foreach (var f in Directory.GetFiles(dir, pattern))
            {
                var (k, s) = KillLockFile(f);
                if (k) killed++; if (s) stale++;
            }
            InvokeRelay("Stop");
            UnityEngine.Debug.Log($"{BiomeLabel.Tag} Kill current: {killed} killed, {stale} stale");
        }

        internal static void KillByPort(int port)
        {
            var dir = GetLockDir();
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, $"server-{port}-*.lock"))
                KillLockFile(f);
            CleanPortDiscoveryFiles(port);
            if (port == MCPServer.ServerPort) InvokeRelay("Stop");
        }

        internal static TerminateResult TerminateByPid(int port, int pid)
        {
            var lockPath = Path.Combine(GetLockDir(), $"server-{port}-{pid}.lock");
            if (!File.Exists(lockPath)) return TerminateResult.NotFound;
            var (killed, stale) = KillLockFile(lockPath);
            if (CountBridgesOnPort(port) == 0)
            {
                CleanPortDiscoveryFiles(port);
                if (port == MCPServer.ServerPort) InvokeRelay("Stop");
            }
            return stale ? TerminateResult.Stale : TerminateResult.Killed;
        }

        internal static int CountBridgesOnPort(int port)
        {
            var dir = GetLockDir();
            if (!Directory.Exists(dir)) return 0;
            return Directory.GetFiles(dir, $"server-{port}-*.lock").Length;
        }

        private static void CleanPortDiscoveryFiles(int port)
        {
            var portsDir = Path.Combine(GetLockDir(), "ports");
            if (!Directory.Exists(portsDir)) return;
            foreach (var pf in Directory.GetFiles(portsDir, "*.port"))
            {
                var content = File.ReadAllText(pf).Split('\n')[0].Trim();
                if (!int.TryParse(content, out var p) || p != port) continue;
                var name = Path.GetFileNameWithoutExtension(pf);
                TryDelete(pf);
                TryDelete(Path.Combine(portsDir, name + ".chat-port"));
                TryDelete(Path.Combine(portsDir, name + ".reload-port"));
            }
        }

        internal static void StopAllOnPort(int port)
        {
            var dir = GetLockDir();
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, $"server-{port}-*.lock"))
                KillLockFile(f);
            CleanPortDiscoveryFiles(port);
            if (port == MCPServer.ServerPort) InvokeRelay("Stop");
        }

        internal static void KillAll()
        {
            var dir = GetLockDir();
            if (!Directory.Exists(dir))
            {
                UnityEngine.Debug.LogWarning($"{BiomeLabel.Tag} Kill: no ~/.unity-biome-mcp dir");
                return;
            }

            // Glob ALL MCP lock files — port-agnostic kill covers port-mismatch after UI change
            int killed = 0, stale = 0;
            foreach (var f in Directory.GetFiles(dir, "server-*.lock"))
            {
                var (k, s) = KillLockFile(f);
                if (k) killed++; if (s) stale++;
            }
            InvokeRelay("Stop");
            UnityEngine.Debug.Log($"{BiomeLabel.Tag} Kill All: {killed} killed, {stale} stale cleaned");
        }

        private static string GetLockDir() =>
            OverrideLockDir ?? Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp");

        private static (bool killed, bool stale) KillLockFile(string filePath)
        {
            var text = File.ReadAllText(filePath).Trim().Split(new char[] { '\n', '\r', ' ', '\0' })[0];
            if (!int.TryParse(text, out var pid)) { TryDelete(filePath); return (false, true); }
            try
            {
                Process.GetProcessById(pid).Kill();
                return (true, false);
            }
            catch (System.ArgumentException) { TryDelete(filePath); return (false, true); }  // already dead
            catch (System.InvalidOperationException) { TryDelete(filePath); return (false, true); }  // exited between lookup & kill
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"{BiomeLabel.Tag} Kill PID {pid}: {ex.Message}");
                return (false, false);
            }
        }

        // Reflection bridge: Chat.CLI assembly depends on Editor, so we can't depend back.
        private static void InvokeRelay(string method)
        {
            const string typeName = "UnityMCP.Editor.Chat.RelaySpawner, UnityMCP.Editor.Chat.CLI";
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type.GetType(typeName)?.GetMethod(method, flags)?.Invoke(null, null);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { }
        }

        internal static void Reimport()
        {
            var guids = AssetDatabase.FindAssets("t:asmdef", new[] { "Packages/com.unity-biome-mcp.editor" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                UnityEngine.Debug.Log($"{BiomeLabel.Tag} Plugin reimported — recompiling...");
            }
            else
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                UnityEngine.Debug.Log($"{BiomeLabel.Tag} AssetDatabase.Refresh forced");
            }
        }
    }
}
