using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        internal static Func<string[]> FindPlaytestConfigGuidsForTest;

        private static string[] FindPlaytestConfigGuids() =>
            FindPlaytestConfigGuidsForTest?.Invoke() ??
            UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig");

        // Returns "--- ALIASES ---\nname=path|comp|field\n---" from PlaytestConfig,
        // or null when no config / no aliases. Called on main thread (AssetDatabase safe).
        internal static string BuildAliasSection(PlaytestConfig config = null)
        {
            if (config == null)
            {
                foreach (var guid in FindPlaytestConfigGuids())
                {
                    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (c?.aliases?.Count > 0) { config = c; break; }
                }
                if (config == null) return null;
            }
            if (config?.aliases == null || config.aliases.Count == 0) return null;
            var sb = new StringBuilder("--- ALIASES ---\n");
            bool any = false;
            foreach (var a in config.aliases)
            {
                if (a.type == AliasType.VarRuntime) continue;
                any = true;
                sb.Append(a.alias).Append('=');
                if (a.type == AliasType.ValConst)
                    sb.Append(a.constValue);
                else
                    sb.Append(a.path).Append('|').Append(a.component).Append('|').Append(a.field);
                sb.Append('\n');
            }
            if (!any) return null;
            sb.Append("---");
            return sb.ToString();
        }

        // Strips the --- ALIASES --- header and --- footer, returns bare name=value lines.
        private static string GetAliasesText()
        {
            var section = BuildAliasSection();
            if (section == null) return "no aliases";
            var sb = new StringBuilder();
            foreach (var raw in section.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (!line.StartsWith("---") && line.Length > 0)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no aliases";
        }

        private static string ExecAliasStatus(string _)
        {
            var sources = new List<string>();
            int count = 0;
            foreach (var guid in FindPlaytestConfigGuids())
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(path);
                if (cfg?.aliases == null) continue;
                sources.Add(path);
                count += cfg.aliases.Count;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"loaded: {(AliasExpander.IsStale ? "stale" : count > 0 ? "true" : "empty")}");
            foreach (var s in sources) sb.AppendLine($"source: {s}");
            sb.AppendLine($"count: {count}");
            sb.Append($"stale: {AliasExpander.IsStale}");
            return sb.ToString();
        }

        // Overwrite PlaytestConfig.asset aliases from .defs text file.
        // defs: project-relative path to .defs file; asset: asset path to PlaytestConfig.
        private static string ExecSyncFromDefs(string argsJson)
        {
            var defsPath  = JsonHelper.ExtractString(argsJson, "defs") ?? "Assets/PlaytestDefs/game_core.defs";
            var assetPath = JsonHelper.ExtractString(argsJson, "asset");

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absDefsPath = Path.IsPathRooted(defsPath) ? defsPath : Path.Combine(projectRoot, defsPath);
            if (!File.Exists(absDefsPath)) return $"err: defs file not found: {defsPath}";

            PlaytestConfig cfg = null;
            if (!string.IsNullOrEmpty(assetPath))
            {
                cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(assetPath);
                if (cfg == null) return $"err: asset not found: {assetPath}";
            }
            else
            {
                foreach (var guid in FindPlaytestConfigGuids())
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(p);
                    if (cfg != null) { assetPath = p; break; }
                }
                if (cfg == null) return "err: no PlaytestConfig found";
            }

            try
            {
                var defsAliases = PlaytestAliasHelpers.ParseDefsToAliases(File.ReadAllText(absDefsPath));
                cfg.aliases.Clear();
                cfg.aliases.AddRange(defsAliases);
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                AliasExpander.Invalidate();
                return $"synced: {defsAliases.Count} aliases -> {assetPath}";
            }
            catch (ArgumentException ex)
            {
                return $"err: parse error in {defsPath}: {ex.Message}";
            }
        }

        // Export PlaytestConfig.asset aliases to a .defs text file.
        // asset: asset path to PlaytestConfig; defs: project-relative output path.
        private static string ExecExportToDefs(string argsJson)
        {
            var assetPath = JsonHelper.ExtractString(argsJson, "asset");
            var defsPath  = JsonHelper.ExtractString(argsJson, "defs") ?? "Assets/PlaytestDefs/game_core.defs";

            PlaytestConfig cfg = null;
            if (!string.IsNullOrEmpty(assetPath))
            {
                cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(assetPath);
                if (cfg == null) return $"err: asset not found: {assetPath}";
            }
            else
            {
                foreach (var guid in FindPlaytestConfigGuids())
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(p);
                    if (cfg != null) break;
                }
                if (cfg == null) return "err: no PlaytestConfig found";
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absDefsPath = Path.IsPathRooted(defsPath) ? defsPath : Path.Combine(projectRoot, defsPath);
            var dir = Path.GetDirectoryName(absDefsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var block = PlaytestAliasHelpers.FormatVALBlock(cfg.aliases);
            File.WriteAllText(absDefsPath, block, Encoding.UTF8);
            AssetDatabase.Refresh();
            return $"exported: {cfg.aliases.Count} aliases -> {defsPath}";
        }

        // Compare alias .defs file vs PlaytestConfig.asset.
        // defs: project-relative path to .defs file (default: Assets/PlaytestDefs/game_core.defs).
        // asset: asset path to PlaytestConfig (default: first found via FindAssets).
        private static string ExecValidateAliases(string argsJson)
        {
            var defsPath  = JsonHelper.ExtractString(argsJson, "defs") ?? "Assets/PlaytestDefs/game_core.defs";
            var assetPath = JsonHelper.ExtractString(argsJson, "asset");

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absDefsPath = Path.IsPathRooted(defsPath)
                ? defsPath
                : Path.Combine(projectRoot, defsPath);

            if (!File.Exists(absDefsPath))
                return $"err: defs file not found: {defsPath}";

            PlaytestConfig cfg = null;
            if (!string.IsNullOrEmpty(assetPath))
            {
                cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(assetPath);
                if (cfg == null) return $"err: asset not found: {assetPath}";
            }
            else
            {
                foreach (var guid in FindPlaytestConfigGuids())
                {
                    var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(p);
                    if (cfg != null) { assetPath = p; break; }
                }
                if (cfg == null) return "err: no PlaytestConfig found";
            }

            try
            {
                var defsAliases = PlaytestAliasHelpers.ParseDefsToAliases(
                    File.ReadAllText(absDefsPath));
                return PlaytestAliasHelpers.ValidateAliases(defsAliases, cfg.aliases);
            }
            catch (ArgumentException ex)
            {
                return $"err: parse error in {defsPath}: {ex.Message}";
            }
        }
    }
}
