// Pure static helpers for Alias Composer UI — no Unity API dependencies.
// Testable without live Editor; all IO goes through ExportToDefs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class PlaytestAliasHelpers
    {
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
            AssetDatabase.Refresh();
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
    }
}
