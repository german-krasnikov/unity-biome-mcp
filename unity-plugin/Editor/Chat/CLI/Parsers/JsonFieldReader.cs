// Shared JSON field reader for the Parsers assembly.
// Pure C# — no UnityEngine deps (noEngineReferences: true).
// Handles string and raw (number/bool/array/object) fields.
using System.Text;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal static class JsonFieldReader
    {
        // Returns the decoded string value for "key": "...", or null if absent/not a string.
        internal static string ReadString(string json, string key)
        {
            if (json == null) return null;
            var needle = "\"" + key + "\":";
            int idx = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx++];
                if (c == '\\' && idx < json.Length)
                {
                    char esc = json[idx++];
                    switch (esc)
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'u':
                            if (idx + 4 <= json.Length &&
                                int.TryParse(json.Substring(idx, 4),
                                    System.Globalization.NumberStyles.HexNumber,
                                    null, out int code))
                            {
                                sb.Append((char)code);
                                idx += 4;
                            }
                            break;
                        default:   sb.Append(esc);  break;
                    }
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // Returns decoded string for string values; raw JSON token for array/object/primitive.
        internal static string ReadRaw(string json, string key)
        {
            var needle = "\"" + key + "\":";
            int idx = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += needle.Length;
            while (idx < json.Length && json[idx] == ' ') idx++;
            if (idx >= json.Length) return null;

            char first = json[idx];
            if (first == '"') return ReadString(json, key);

            if (first == '[' || first == '{')
            {
                char open  = first;
                char close = (open == '[') ? ']' : '}';
                int depth = 0, start = idx;
                while (idx < json.Length)
                {
                    char c = json[idx];
                    if (c == open)  depth++;
                    else if (c == close) { depth--; if (depth == 0) { idx++; break; } }
                    else if (c == '"')
                    {
                        idx++;
                        while (idx < json.Length)
                        {
                            char sc = json[idx++];
                            if (sc == '\\') idx++;
                            else if (sc == '"') break;
                        }
                        continue;
                    }
                    idx++;
                }
                return json.Substring(start, idx - start);
            }

            // Primitive: number, bool, null
            {
                int start = idx;
                while (idx < json.Length && json[idx] != ',' && json[idx] != '}') idx++;
                return json.Substring(start, idx - start).Trim();
            }
        }
    }
}
