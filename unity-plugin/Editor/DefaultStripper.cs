using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    // Port of server/src/unity_mcp/compressor.py strip_defaults().
    // Removes lines whose value is a known Unity default to save tokens.
    // Always preserves blank, headers, separators, err: lines.
    // Default data sourced from dsl/data/stripdefaults*.core-data via StripDefaultRows
    // and FieldSpecificDefaultRows.
    internal static class DefaultStripper
    {
        private static readonly HashSet<string> Defaults = BuildDefaults();
        // Field-specific defaults: "1"/"1.0" only stripped for known fields to avoid
        // false positives (e.g. user health = 1 should not be stripped).
        private static readonly Dictionary<string, HashSet<string>> FieldDefaults = BuildFieldDefaults();

        private static HashSet<string> BuildDefaults()
        {
            var set = new HashSet<string>(StripDefaultRows.All.Length);
            foreach (var row in StripDefaultRows.All)
                set.Add(row.Value);
            return set;
        }

        private static Dictionary<string, HashSet<string>> BuildFieldDefaults()
        {
            var d = new Dictionary<string, HashSet<string>>();
            foreach (var row in FieldSpecificDefaultRows.All)
            {
                if (!d.TryGetValue(row.FieldKey, out var set))
                    d[row.FieldKey] = set = new HashSet<string>();
                set.Add(row.Value);
            }
            return d;
        }

        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var lines = text.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                var s = line.Trim();
                if (IsStructural(s) || !ShouldStrip(s))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
            }
            return sb.ToString();
        }

        private static bool IsStructural(string s) =>
            s.Length == 0 || s.StartsWith("[") || s.StartsWith("---") || s.StartsWith("err:");

        private static bool ShouldStrip(string s)
        {
            int sep = s.IndexOf(": ");
            if (sep < 0) return false;
            var key   = s.Substring(0, sep).Trim().ToLowerInvariant();
            var value = s.Substring(sep + 2).Trim();
            if (Defaults.Contains(value)) return true;
            return FieldDefaults.TryGetValue(key, out var fd) && fd.Contains(value);
        }
    }
}
