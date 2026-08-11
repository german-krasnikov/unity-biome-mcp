// Parser for Edit/Write/str_replace_editor tool argsJson.
// Keys verified empirically: file_path (primary), path (fallback),
// old_string, new_string, content, edits[].
// Pure C# — no UnityEngine deps.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal readonly struct CodeEditEdit
    {
        public readonly string OldString;
        public readonly string NewString;
        public CodeEditEdit(string old, string nw) { OldString = old; NewString = nw; }
    }

    internal struct CodeEditArgs
    {
        public string FilePath;
        public string OldString;
        public string NewString;
        public string Content;
        public CodeEditEdit[] Edits;
        public bool IsValid;
    }

    internal static class CodeEditArgsParser
    {
        internal static CodeEditArgs Parse(string argsJson)
        {
            if (string.IsNullOrEmpty(argsJson))
                return new CodeEditArgs { IsValid = false };

            var trimmed = argsJson.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{')
                return new CodeEditArgs { IsValid = false };

            var path = ReadStringField(argsJson, "file_path")
                    ?? ReadStringField(argsJson, "path");
            if (path == null)
                return new CodeEditArgs { IsValid = false };

            return new CodeEditArgs
            {
                FilePath  = path,
                OldString = ReadStringField(argsJson, "old_string"),
                NewString = ReadStringField(argsJson, "new_string"),
                Content   = ReadStringField(argsJson, "content"),
                Edits     = ReadEditsArray(argsJson),
                IsValid   = true,
            };
        }

        internal static string DetectLang(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "";
            int dot = filePath.LastIndexOf('.');
            if (dot < 0) return "";
            var ext = filePath.Substring(dot + 1).ToLowerInvariant();
            switch (ext)
            {
                case "cs":             return "csharp";
                case "py":             return "python";
                case "js": case "jsx": return "javascript";
                case "ts": case "tsx": return "typescript";
                case "shader":         return "hlsl";
                case "json":           return "json";
                case "xml":            return "xml";
                case "md":             return "markdown";
                case "sh":             return "bash";
                default:               return "";
            }
        }

        // Delegates to shared JsonFieldReader; kept internal for any external callers in tests.
        internal static string ReadStringField(string json, string key) =>
            JsonFieldReader.ReadString(json, key);

        // Extracts [{old_string, new_string}, ...] from "edits":[...].
        private static CodeEditEdit[] ReadEditsArray(string json)
        {
            const string needle = "\"edits\":[";
            int start = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (start < 0) return null;
            start += needle.Length - 1; // position of '['

            // Extract the balanced array text
            int depth = 0, end = start;
            while (end < json.Length)
            {
                if (json[end] == '[') depth++;
                else if (json[end] == ']') { depth--; if (depth == 0) break; }
                end++;
            }
            string arrayText = json.Substring(start, end - start + 1);

            // Parse objects with depth-tracking to handle braces inside string values.
            var result = new List<CodeEditEdit>();
            int pos = 0;
            while (pos < arrayText.Length)
            {
                int objStart2 = arrayText.IndexOf('{', pos);
                if (objStart2 < 0) break;
                int objDepth = 0, cur = objStart2;
                bool inStr = false;
                while (cur < arrayText.Length)
                {
                    char c = arrayText[cur];
                    if (inStr) { if (c == '\\') cur++; else if (c == '"') inStr = false; }
                    else if (c == '"') inStr = true;
                    else if (c == '{') objDepth++;
                    else if (c == '}') { objDepth--; if (objDepth == 0) { cur++; break; } }
                    cur++;
                }
                string obj = arrayText.Substring(objStart2, cur - objStart2);
                var old = ReadStringField(obj, "old_string");
                var nw  = ReadStringField(obj, "new_string");
                result.Add(new CodeEditEdit(old, nw));
                pos = cur;
            }
            return result.Count > 0 ? result.ToArray() : null;
        }
    }
}
