using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Parses run_tests wire args for the categories/assemblies/tests selection.
    /// Supports both a JSON array (Python sends command_args["categories"] = [...])
    /// and a comma-separated string (CLI ergonomics for a hand-typed invocation).
    /// Missing or empty always yields Array.Empty -- never null.
    /// </summary>
    internal static class TestRunSelectionArgs
    {
        internal static string[] ParseList(string argsJson, string key)
        {
            if (string.IsNullOrEmpty(argsJson)) return Array.Empty<string>();
            var needle = "\"" + key + "\"";
            var keyIdx = argsJson.IndexOf(needle, StringComparison.Ordinal);
            if (keyIdx < 0) return Array.Empty<string>();
            var colon = argsJson.IndexOf(':', keyIdx + needle.Length);
            if (colon < 0) return Array.Empty<string>();
            var i = colon + 1;
            while (i < argsJson.Length && argsJson[i] == ' ') i++;
            if (i >= argsJson.Length) return Array.Empty<string>();

            if (argsJson[i] == '[')
                return ParseJsonStringArray(argsJson, i);

            if (argsJson[i] == '"')
                return SplitCommaSeparated(JsonHelper.ExtractString(argsJson, key) ?? "");

            return Array.Empty<string>();
        }

        // Bounded scan of exactly this array's ["a","b"] text -- never reads
        // past its own closing bracket, so a later key's value can't leak in.
        private static string[] ParseJsonStringArray(string json, int start)
        {
            var result = new List<string>();
            var depth = 0;
            for (var i = start; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '[') { depth++; continue; }
                if (c == ']') { depth--; if (depth == 0) break; continue; }
                if (c != '"') continue;
                var end = json.IndexOf('"', i + 1);
                if (end < 0) break;
                result.Add(JsonHelper.UnescapeJsonString(json.Substring(i + 1, end - i - 1)));
                i = end;
            }
            return result.ToArray();
        }

        private static string[] SplitCommaSeparated(string value) =>
            value.Split(',')
                 .Select(s => s.Trim())
                 .Where(s => s.Length > 0)
                 .ToArray();
    }
}
