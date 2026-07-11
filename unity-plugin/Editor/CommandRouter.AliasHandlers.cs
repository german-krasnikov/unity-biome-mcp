using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        // Returns "--- ALIASES ---\nname=path|comp|field\n---" from PlaytestConfig,
        // or null when no config / no aliases. Called on main thread (AssetDatabase safe).
        internal static string BuildAliasSection(PlaytestConfig config = null)
        {
            if (config == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig"))
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
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig"))
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
    }
}
