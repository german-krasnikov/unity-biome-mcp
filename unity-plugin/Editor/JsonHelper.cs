using System.Text;

namespace UnityMCP.Editor
{
    public static partial class JsonHelper
    {
        /// <summary>UTF-8 without BOM. Use for all File.WriteAllText calls — the static
        /// <c>Encoding.UTF8</c> emits a BOM that breaks Node JSON.parse and Unity importer.</summary>
        public static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        private static int FindKeyIndex(string json, string needle)
        {
            bool inString = false;
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '{' || c == '[') { depth++; continue; }
                if (c == '}' || c == ']') { depth--; continue; }
                if (c == '"')
                {
                    if (depth <= 1 && i + needle.Length <= json.Length &&
                        string.CompareOrdinal(json, i, needle, 0, needle.Length) == 0)
                    {
                        int j = i + needle.Length;
                        while (j < json.Length && json[j] == ' ') j++;
                        if (j < json.Length && json[j] == ':')
                            return i;
                    }
                    inString = true;
                }
            }
            return -1;
        }

        public static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return null;

            var colon = json.IndexOf(':', idx + needle.Length);
            if (colon == -1) return null;

            var i = colon + 1;
            while (i < json.Length && json[i] == ' ') i++;
            if (i >= json.Length) return null;

            if (i + 4 <= json.Length && json.Substring(i, 4) == "null")
                return null;

            if (json[i] == '"')
            {
                i++;
                var end = i;
                while (end < json.Length)
                {
                    if (json[end] == '"')
                    {
                        int backslashes = 0;
                        int b = end - 1;
                        while (b >= i && json[b] == '\\') { backslashes++; b--; }
                        if (backslashes % 2 == 0) break;
                    }
                    end++;
                }
                if (end >= json.Length) return null;
                return UnescapeJsonString(json.Substring(i, end - i));
            }

            var endIdx = i;
            while (endIdx < json.Length && json[endIdx] != ',' && json[endIdx] != '}')
                endIdx++;
            return json.Substring(i, endIdx - i).Trim();
        }

        public static string ExtractObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "{}";
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return "{}";
            var start = json.IndexOf('{', idx + needle.Length);
            if (start == -1) return "{}";
            var end = ScanBalanced(json, start, '{', '}');
            return end == -1 ? "{}" : json.Substring(start, end - start);
        }

        public static string ExtractArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "[]";
            var needle = $"\"{key}\"";
            var idx = FindKeyIndex(json, needle);
            if (idx == -1) return "[]";
            var start = json.IndexOf('[', idx + needle.Length);
            if (start == -1) return "[]";
            var end = ScanBalanced(json, start, '[', ']');
            return end == -1 ? "[]" : json.Substring(start, end - start);
        }

        /// <summary>
        /// Scan for a balanced open/close pair starting at <paramref name="start"/>.
        /// Returns the exclusive-end index (close char index + 1), or -1 if not found.
        /// Handles nesting and quoted strings with escape sequences.
        /// </summary>
        private static int ScanBalanced(string s, int start, char open, char close)
        {
            int depth = 0; bool inStr = false, esc = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (esc)   { esc = false; continue; }
                if (inStr) { if (c == '\\') esc = true; else if (c == '"') inStr = false; continue; }
                if (c == '"')   { inStr = true; continue; }
                if (c == open)  { depth++; continue; }
                if (c == close) { depth--; if (depth == 0) return i + 1; }
            }
            return -1;
        }

        public static string UnescapeJsonString(string s)
        {
            if (s == null || s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    switch (s[i + 1])
                    {
                        case '"':  sb.Append('"');  i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n':  sb.Append('\n'); i++; break;
                        case 'r':  sb.Append('\r'); i++; break;
                        case 't':  sb.Append('\t'); i++; break;
                        case 'b':  sb.Append('\b'); i++; break;
                        case 'f':  sb.Append('\f'); i++; break;
                        case 'u':
                            if (i + 5 < s.Length)
                            {
                                sb.Append((char)System.Convert.ToInt32(s.Substring(i + 2, 4), 16));
                                i += 5;
                            }
                            else sb.Append(s[i]);
                            break;
                        default:   sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extract an integer value from JSON (works for both numbers and quoted strings).
        /// Returns <paramref name="defaultValue"/> if key not found or not parseable.
        /// </summary>
        public static int ExtractInt(string json, string key, int defaultValue = 0)
        {
            var raw = ExtractString(json, key);
            if (string.IsNullOrEmpty(raw)) return defaultValue;
            return int.TryParse(raw.Trim('"'), out var v) ? v : defaultValue;
        }

        /// <summary>
        /// Extract a numeric float value from JSON (works for both numbers and quoted strings).
        /// Returns 0f if key not found or not parseable.
        /// </summary>
        public static float ExtractFloat(string json, string key)
        {
            var raw = ExtractString(json, key);
            if (string.IsNullOrEmpty(raw)) return 0f;
            if (float.TryParse(raw.Trim('"'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
            return 0f;
        }

        internal static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            var sb = new StringBuilder(str.Length + 8);
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    default:
                        if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Build a JSON string array: ["a","b","c"].
        /// Items are JSON-escaped. Returns "[]" for null/empty input.
        /// </summary>
        public static string BuildJsonStringArray(string[] items)
        {
            if (items == null || items.Length == 0) return "[]";
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(EscapeJson(items[i]));
                sb.Append('"');
            }
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Extract the first balanced JSON object from an array string like [{"a":1},{"b":2}].</summary>
        internal static string ExtractFirstArrayObject(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson)) return null;
            var start = arrayJson.IndexOf('{');
            if (start == -1) return null;
            var end = ScanBalanced(arrayJson, start, '{', '}');
            return end == -1 ? null : arrayJson.Substring(start, end - start);
        }

        /// <summary>
        /// Extract the next balanced JSON object from arrayJson starting at or after <paramref name="pos"/>.
        /// Advances pos past the closing '}'. Returns null when no more objects found.
        /// </summary>
        internal static string ExtractNextArrayObject(string arrayJson, ref int pos)
        {
            if (string.IsNullOrEmpty(arrayJson)) return null;
            var start = arrayJson.IndexOf('{', pos);
            if (start == -1) { pos = arrayJson.Length; return null; }
            var end = ScanBalanced(arrayJson, start, '{', '}');
            if (end == -1)   { pos = arrayJson.Length; return null; }
            pos = end;
            return arrayJson.Substring(start, end - start);
        }
    }
}
