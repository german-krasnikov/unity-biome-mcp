// Pure static helpers for Alias Composer UI — no Unity API dependencies.
// Testable without live Editor; all IO goes through ExportToDefs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class PlaytestAliasHelpers
    {
        // Testability seam — tests can inject a call spy.
        internal static Action<string, ImportAssetOptions> _importAsset =
            AssetDatabase.ImportAsset;

        private static readonly Regex NonAlphaUnder = new Regex(@"[^a-z0-9_]", RegexOptions.Compiled);

        // Dispatch on type — VAL or VAR keyword, path or constValue content.
        internal static string FormatLine(QueryAlias a) => a.type switch
        {
            AliasType.ValConst   => $"VAL ${a.alias} {a.constValue}",
            AliasType.VarRuntime => $"VAR ${a.alias} @{a.path}|{a.component}|{a.field}",
            _                    => $"VAL ${a.alias} {BuildPath(a)}",
        };

        // Backward-compat wrapper — all callers keep working without change.
        internal static string FormatVALLine(QueryAlias a) => FormatLine(a);

        static string BuildPath(QueryAlias a)
        {
            var v = a.path;
            if (!string.IsNullOrEmpty(a.component)) v += "|" + a.component;
            if (!string.IsNullOrEmpty(a.field))     v += "|" + a.field;
            return v;
        }

        // Multi-line VAL block — empty list → ""
        internal static string FormatVALBlock(IReadOnlyList<QueryAlias> aliases)
        {
            if (aliases == null || aliases.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < aliases.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FormatVALLine(aliases[i]));
            }
            return sb.ToString();
        }

        // Writes Assets/PlaytestDefs/<filename>.defs; returns absolute path.
        internal static string ExportToDefs(IReadOnlyList<QueryAlias> aliases, string filename = "aliases")
        {
            const string folder = "Assets/PlaytestDefs";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "PlaytestDefs");

            var relative = $"{folder}/{filename}.defs";
            var absolute = Path.GetFullPath(relative);
            File.WriteAllText(absolute, FormatVALBlock(aliases));
            _importAsset(relative, ImportAssetOptions.Default);
            return absolute;
        }

        // Estimated net token savings: gross savings minus block overhead.
        // Block overhead: header(4) + footer(1) + per-alias definition cost.
        // Assumes minimum 3 uses per alias. Returns 0 when aliases don't pay off.
        internal static int TokenSavingsEstimate(IReadOnlyList<QueryAlias> aliases)
        {
            if (aliases == null || aliases.Count == 0) return 0;
            int blockOverhead = 5;
            foreach (var a in aliases)
            {
                int defLen = a.type == AliasType.ValConst
                    ? (a.alias?.Length ?? 0) + (a.constValue?.Length ?? 0) + 3
                    : (a.alias?.Length ?? 0) + (a.path?.Length ?? 0)
                      + (a.component?.Length ?? 0) + (a.field?.Length ?? 0) + 3;
                blockOverhead += defLen / 4 + 1;
            }
            int gross = 0;
            foreach (var a in aliases)
            {
                int fullLen = a.type == AliasType.ValConst
                    ? (a.constValue?.Length ?? 0)
                    : (a.path?.Length ?? 0) + (a.component?.Length ?? 0)
                      + (a.field?.Length ?? 0) + 2;
                int shortLen = (a.alias?.Length ?? 0) + 1;
                gross += Math.Max(0, fullLen - shortLen) * 3 / 4;
            }
            return Math.Max(0, gross - blockOverhead);
        }

        // Lowercase, spaces→underscore, strip non-alphanum-non-underscore.
        internal static string SuggestName(string goName)
        {
            if (string.IsNullOrEmpty(goName)) return "";
            var lower = goName.ToLowerInvariant().Replace(' ', '_');
            return NonAlphaUnder.Replace(lower, "");
        }

        // Parse .defs file text into QueryAlias list.
        // Skips blank lines, # comments, MACRO…END_MACRO blocks, INCLUDE lines.
        // VAL $name /path|comp|field → ValPath; VAL $name literal → ValConst;
        // VAR $name @path|comp|field → VarRuntime.
        // Throws ArgumentException on malformed VAL/VAR line (missing value token).
        // Last definition wins for duplicate names.
        internal static List<QueryAlias> ParseDefsToAliases(string defsText)
        {
            if (string.IsNullOrWhiteSpace(defsText)) return new List<QueryAlias>();
            var dict  = new Dictionary<string, QueryAlias>(StringComparer.Ordinal);
            var order = new List<string>();
            bool inMacro = false;

            foreach (var rawLine in defsText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                if (line.StartsWith("MACRO"))    { inMacro = true;  continue; }
                if (line == "END_MACRO")         { inMacro = false; continue; }
                if (inMacro)                     continue;
                if (line.StartsWith("INCLUDE ")) continue;

                bool isVal = line.StartsWith("VAL ");
                bool isVar = line.StartsWith("VAR ");
                if (!isVal && !isVar) continue;  // skip unknown DSL keywords

                var rest     = line.Substring(4).Trim();  // after "VAL " / "VAR "
                var spaceIdx = rest.IndexOf(' ');
                if (spaceIdx < 0)
                    throw new ArgumentException($"malformed line: {line}");

                var name  = rest.Substring(0, spaceIdx).TrimStart('$');
                var value = rest.Substring(spaceIdx + 1).Trim();

                QueryAlias alias;
                if (isVar)
                {
                    var atPath = value.StartsWith("@") ? value.Substring(1) : value;
                    var parts  = atPath.Split('|');
                    alias = new QueryAlias
                    {
                        alias     = name,
                        type      = AliasType.VarRuntime,
                        path      = parts[0],
                        component = parts.Length > 1 ? parts[1] : "",
                        field     = parts.Length > 2 ? parts[2] : "",
                    };
                }
                else if (value.StartsWith("/") || value.Contains("|"))
                {
                    var parts = value.Split('|');
                    alias = new QueryAlias
                    {
                        alias     = name,
                        type      = AliasType.ValPath,
                        path      = parts[0],
                        component = parts.Length > 1 ? parts[1] : "",
                        field     = parts.Length > 2 ? parts[2] : "",
                    };
                }
                else
                {
                    alias = new QueryAlias
                        { alias = name, type = AliasType.ValConst, constValue = value };
                }

                if (!dict.ContainsKey(name)) order.Add(name);
                dict[name] = alias;
            }

            return order.Select(n => dict[n]).ToList();
        }

        // Compare two alias lists. Returns "ok: N aliases in sync" when identical,
        // or a diff report with missing/extra/changed sections.
        // Safe against duplicate aliases in either list (last definition wins per name).
        internal static string ValidateAliases(
            IReadOnlyList<QueryAlias> fromDefs, IReadOnlyList<QueryAlias> fromAsset)
        {
            var defMap   = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var a in fromDefs)   defMap[a.alias]   = FormatLine(a);
            var assetMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var a in fromAsset)  assetMap[a.alias] = FormatLine(a);

            var missing = new List<string>();
            var changed = new List<(string name, string dLine, string aLine)>();
            foreach (var kvp in defMap)
            {
                if (!assetMap.TryGetValue(kvp.Key, out var aLine)) missing.Add(kvp.Key);
                else if (aLine != kvp.Value)                        changed.Add((kvp.Key, kvp.Value, aLine));
            }
            var extra = assetMap.Keys.Where(k => !defMap.ContainsKey(k)).ToList();

            if (missing.Count == 0 && extra.Count == 0 && changed.Count == 0)
                return $"ok: {defMap.Count} aliases in sync";

            int matched = defMap.Count - missing.Count - changed.Count;
            var sb = new StringBuilder();
            sb.AppendLine($"ok: {matched} matched");
            if (missing.Count > 0) { sb.AppendLine($"missing: {missing.Count}"); foreach (var n in missing) sb.AppendLine($"  ${n}"); }
            if (extra.Count > 0)   { sb.AppendLine($"extra: {extra.Count}");   foreach (var n in extra)   sb.AppendLine($"  ${n}"); }
            if (changed.Count > 0)
            {
                sb.AppendLine($"changed: {changed.Count}");
                foreach (var (n, d, a) in changed)
                {
                    sb.AppendLine($"  ${n}");
                    sb.AppendLine($"    defs:  {d}");
                    sb.AppendLine($"    asset: {a}");
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
