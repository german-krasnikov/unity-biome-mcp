using System.Collections.Generic;
using UnityEditor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Expands $sigil placeholders in batch DSL text and JSON args strings.
    /// Alias table is loaded lazily from PlaytestConfig assets and cached until
    /// any .asset file is re-imported (AliasConfigPostprocessor) or domain reload.
    /// </summary>
    internal static class AliasExpander
    {
        // Test seam — inject a pre-built table to bypass AssetDatabase in unit tests.
        internal static Dictionary<string, string> _tableOverride;

        private static Dictionary<string, string> _table;
        private static bool _hasLoaded;
        private static int _cachedConfigAliasCount = -1;

        [InitializeOnLoadMethod]
        static void ResetAliasCountCache() { _cachedConfigAliasCount = -1; }

        /// <summary>True when the table was previously loaded but is now invalidated (stale).</summary>
        internal static bool IsStale => _hasLoaded && _table == null && _tableOverride == null;

        /// <summary>Count of cached alias entries (0 if not yet loaded).</summary>
        internal static int CachedAliasCount => (_tableOverride ?? _table)?.Count ?? 0;

        /// <summary>Count aliases from PlaytestConfig assets. Result is cached until domain reload.</summary>
        internal static int CountConfigAliases()
        {
            if (_cachedConfigAliasCount >= 0) return _cachedConfigAliasCount;
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:PlaytestConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (cfg?.aliases != null) count += cfg.aliases.Count;
            }
            _cachedConfigAliasCount = count;
            return count;
        }

        /// <summary>Expand $sigils in a JSON args string. JSON-escapes replacement values.</summary>
        internal static string ExpandJson(string argsJson) => ExpandCore(argsJson, jsonEscape: true);

        /// <summary>Expand $sigils in plain DSL text (pre-JSON). No extra escaping needed.</summary>
        internal static string ExpandText(string text) => ExpandCore(text, jsonEscape: false);

        /// <summary>Force cache reload on next call (called by AliasConfigPostprocessor).</summary>
        internal static void Invalidate() { _table = null; _cachedConfigAliasCount = -1; }

        private static string ExpandCore(string text, bool jsonEscape)
        {
            if (text == null) return null;
            if (!text.Contains('$')) return text;  // fast path

            var table = _tableOverride ?? (_table ??= GetTable());
            if (table.Count == 0) return text;

            // G8: expand up to 3 levels to resolve nested aliases ($outer → $inner → value).
            // Stop early if no more sigils or text didn't change (cycle/unknown sigil).
            for (int pass = 0; pass < 3; pass++)
            {
                if (!text.Contains('$')) break;
                var expanded = PlaytestParser.SigilRegex.Replace(text, m =>
                {
                    var name = m.Groups[1].Value;
                    if (!table.TryGetValue(name, out var v)) return m.Value;  // unknown → intact
                    return jsonEscape ? JsonHelper.EscapeJson(v) : v;
                });
                if (expanded == text) break;  // no progress — unknown sigils remain
                text = expanded;
            }
            return text;
        }

        private static Dictionary<string, string> GetTable()
        {
            _hasLoaded = true;
            var table = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:PlaytestConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (cfg?.aliases == null) continue;
                foreach (var a in cfg.aliases)
                {
                    if (string.IsNullOrEmpty(a.alias)) continue;
                    string value = a.type switch
                    {
                        AliasType.ValConst   => a.constValue ?? "",
                        AliasType.VarRuntime => null,   // runtime-only, skip
                        _                    => BuildPipePath(a),
                    };
                    if (value != null) table[a.alias] = value;
                }
            }

            // Scan .defs files in Assets/PlaytestDefs/
            var defsDir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "PlaytestDefs");
            if (System.IO.Directory.Exists(defsDir))
            {
                foreach (var f in System.IO.Directory.GetFiles(defsDir, "*.defs"))
                {
                    var defsAliases = PlaytestAliasHelpers.ParseDefsToAliases(
                        System.IO.File.ReadAllText(f, System.Text.Encoding.UTF8));
                    foreach (var a in defsAliases)
                    {
                        if (string.IsNullOrEmpty(a.alias)) continue;
                        string value = a.type switch
                        {
                            AliasType.ValConst   => a.constValue ?? "",
                            AliasType.VarRuntime => null,
                            _                    => BuildPipePath(a),
                        };
                        if (value != null) table[a.alias] = value;
                    }
                }
            }

            return table;
        }

        private static string BuildPipePath(QueryAlias a)
        {
            var v = a.path ?? "";
            if (!string.IsNullOrEmpty(a.component)) v += "|" + a.component;
            if (!string.IsNullOrEmpty(a.field))     v += "|" + a.field;
            return v;
        }
    }

    internal class AliasConfigPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
                if (p.EndsWith(".asset") || p.EndsWith(".defs")) { AliasExpander.Invalidate(); return; }
            foreach (var p in deleted)
                if (p.EndsWith(".asset") || p.EndsWith(".defs")) { AliasExpander.Invalidate(); return; }
        }
    }
}
