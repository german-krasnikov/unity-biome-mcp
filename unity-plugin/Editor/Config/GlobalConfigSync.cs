// Syncs EditorPrefs ↔ ~/.unity-biome-mcp/global-config.json.
// JSON format matches Python's GlobalConfig dataclass (global_config.py).
// Never throws on load — returns/writes defaults on error.
// Atomic write: tmp file + Delete + Move (no 3-arg File.Move in .NET Standard 2.1).
using System;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class GlobalConfigSync
    {
        // Test seam: set to a temp path in tests; null = production path.
        internal static string ConfigPathOverride;

        internal static string ConfigPath =>
            ConfigPathOverride
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-biome-mcp",
                "global-config.json");

        internal static void SaveToDisk()
        {
            try
            {
                var path = ConfigPath;
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = BuildJson();
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json, System.Text.Encoding.UTF8);

                // .NET Standard 2.1 has no File.Move(src, dst, overwrite) — Delete + Move.
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Biome] GlobalConfigSync.SaveToDisk failed: {e.Message}");
            }
        }

        internal static void LoadFromDisk()
        {
            try
            {
                var path = ConfigPath;
                if (!File.Exists(path))
                {
                    WriteDefaults();
                    return;
                }
                var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                ApplyJson(text);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Biome] GlobalConfigSync.LoadFromDisk failed: {e.Message}");
                WriteDefaults();
            }
        }

        // ── private helpers ──────────────────────────────────────────────────

        private static string BuildJson()
        {
            var idleAutoSuspend   = EditorPrefs.GetBool(PrefKeys.IdleAutoSuspend, true);
            var idleTimeoutMin    = EditorPrefs.GetInt(PrefKeys.IdleTimeoutMin, 30);
            var terminateOrphan   = EditorPrefs.GetBool(PrefKeys.TerminateOrphan, true);
            var orphanGraceMin    = EditorPrefs.GetInt(PrefKeys.OrphanGraceMin, 2);

            return $"{{\n" +
                   $"  \"idle_timeout_min\": {idleTimeoutMin},\n" +
                   $"  \"idle_auto_suspend\": {BoolLit(idleAutoSuspend)},\n" +
                   $"  \"bridge_terminate_orphan\": {BoolLit(terminateOrphan)},\n" +
                   $"  \"bridge_orphan_grace_min\": {orphanGraceMin}\n" +
                   $"}}";
        }

        private static void ApplyJson(string json)
        {
            EditorPrefs.SetInt(PrefKeys.IdleTimeoutMin,
                ExtractInt(json, "idle_timeout_min", 30));
            EditorPrefs.SetBool(PrefKeys.IdleAutoSuspend,
                ExtractBool(json, "idle_auto_suspend", true));
            EditorPrefs.SetBool(PrefKeys.TerminateOrphan,
                ExtractBool(json, "bridge_terminate_orphan", true));
            EditorPrefs.SetInt(PrefKeys.OrphanGraceMin,
                ExtractInt(json, "bridge_orphan_grace_min", 2));
        }

        private static void WriteDefaults()
        {
            EditorPrefs.SetInt(PrefKeys.IdleTimeoutMin, 30);
            EditorPrefs.SetBool(PrefKeys.IdleAutoSuspend, true);
            EditorPrefs.SetBool(PrefKeys.TerminateOrphan, true);
            EditorPrefs.SetInt(PrefKeys.OrphanGraceMin, 2);
        }

        private static string BoolLit(bool v) => v ? "true" : "false";

        // Minimal JSON value extractors — no external JSON library required.
        private static int ExtractInt(string json, string key, int def)
        {
            var marker = $"\"{key}\"";
            var idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return def;
            idx = json.IndexOf(':', idx + marker.Length);
            if (idx < 0) return def;
            idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\n')) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.TryParse(json.Substring(idx, end - idx), out var v) ? v : def;
        }

        private static bool ExtractBool(string json, string key, bool def)
        {
            var marker = $"\"{key}\"";
            var idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return def;
            idx = json.IndexOf(':', idx + marker.Length);
            if (idx < 0) return def;
            idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\n')) idx++;
            if (idx + 4 <= json.Length && json.Substring(idx, 4) == "true")  return true;
            if (idx + 5 <= json.Length && json.Substring(idx, 5) == "false") return false;
            return def;
        }
    }
}
