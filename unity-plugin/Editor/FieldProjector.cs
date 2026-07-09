using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    // Port of server/src/unity_mcp/compressor.py project_fields().
    // Keeps only lines whose key matches a requested field (exact or dotted-prefix,
    // case-insensitive). Always preserves blank, headers, separators, err: lines.
    internal static class FieldProjector
    {
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "position",      "m_localposition" }, { "localposition", "m_localposition" },
            { "rotation",      "m_localrotation" }, { "localrotation", "m_localrotation" },
            { "scale",         "m_localscale"    }, { "localscale",    "m_localscale"    },
            { "mass",          "m_mass"          }, { "enabled",       "m_enabled"       },
            { "active",        "m_isactive"      }, { "name",          "m_name"          },
            { "tag",           "m_tagstring"     }, { "layer",         "m_layer"         },
        };

        public static string Project(string text, string fields)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fields)) return text;
            var wanted = BuildWanted(fields);
            if (wanted.Count == 0) return text;

            var lines = text.Split('\n');
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                var s = line.Trim();
                if (IsStructural(s) || IsMatch(s, wanted))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
            }
            return sb.ToString();
        }

        private static List<string> BuildWanted(string fields)
        {
            var wanted = new List<string>();
            foreach (var f in fields.Split(','))
            {
                var raw = f.Trim().ToLowerInvariant();
                if (raw.Length == 0) continue;
                wanted.Add(raw);
                if (Aliases.TryGetValue(raw, out var alias) && alias != raw)
                    wanted.Add(alias);
            }
            return wanted;
        }

        private static bool IsStructural(string s) =>
            s.Length == 0 || s.StartsWith("[") || s.StartsWith("---") || s.StartsWith("err:");

        private static bool IsMatch(string s, List<string> wanted)
        {
            int sep = s.IndexOf(": ");
            var key = (sep >= 0 ? s.Substring(0, sep).Trim() : s).ToLowerInvariant();
            foreach (var w in wanted)
                if (key == w || key.StartsWith(w + ".")) return true;
            return false;
        }
    }
}
