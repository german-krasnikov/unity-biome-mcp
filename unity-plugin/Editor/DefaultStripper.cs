using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    // Port of server/src/unity_mcp/compressor.py strip_defaults().
    // Removes lines whose value is a known Unity default to save tokens.
    // Always preserves blank, headers, separators, err: lines.
    internal static class DefaultStripper
    {
        private static readonly HashSet<string> Defaults = new HashSet<string>
        {
            "0", "0.0", "false", "null", "None", "\"\"",
            "(0, 0, 0)", "(0.0, 0.0, 0.0)",
            "(0, 0, 0, 1)", "(0.0, 0.0, 0.0, 1.0)",
            "(1, 1, 1)", "(1.0, 1.0, 1.0)",
            "[]", "#00000000", "Untagged", "Default",
            "(0, 0)", "(0, 0, 0, 0)", "#FFFFFFFF",
        };

        // Field-specific defaults: "1"/"1.0" only stripped for known fields to avoid
        // false positives (e.g. user health = 1 should not be stripped).
        private static readonly Dictionary<string, HashSet<string>> FieldDefaults =
            new Dictionary<string, HashSet<string>>
        {
            { "m_mass",     new HashSet<string> { "1", "1.0" } },
            { "m_layer",    new HashSet<string> { "0" } },
            { "m_isstatic", new HashSet<string> { "false", "False" } },
        };

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
