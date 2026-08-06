using System.Collections.Generic;
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

        internal static void Kill() => KillAll();

        internal static void KillAll()
        {
            var dir = OverrideLockDir ?? System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp");
            if (!Directory.Exists(dir))
            {
                UnityEngine.Debug.LogWarning("[MCP] Kill: no ~/.unity-biome-mcp dir");
                return;
            }

            // Glob ALL MCP lock files — port-agnostic kill covers port-mismatch after UI change
            var files = new List<string>(Directory.GetFiles(dir, "server-*.lock"));

            int killed = 0, stale = 0;
            foreach (var f in files)
            {
                var text = File.ReadAllText(f).Trim().Split(new char[] { '\n', '\r', ' ', '\0' })[0];
                if (!int.TryParse(text, out var pid)) { TryDelete(f); stale++; continue; }
                try
                {
                    Process.GetProcessById(pid).Kill();
                    killed++;
                }
                catch (System.ArgumentException) { TryDelete(f); stale++; }  // already dead
                catch (System.InvalidOperationException) { TryDelete(f); stale++; }  // exited between lookup & kill
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[MCP] Kill PID {pid}: {ex.Message}");
                }
            }
            InvokeRelay("Stop");
            UnityEngine.Debug.Log($"[MCP] Kill All: {killed} killed, {stale} stale cleaned");
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
                UnityEngine.Debug.Log("[MCP] Plugin reimported — recompiling...");
            }
            else
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                UnityEngine.Debug.Log("[MCP] AssetDatabase.Refresh forced");
            }
        }
    }
}
