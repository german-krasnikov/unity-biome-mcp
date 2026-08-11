// Pure frontmatter parser — zero UnityEngine deps. NUnit-testable.
using System;

namespace UnityMCP.Editor.Chat
{
    internal static class AgentFrontmatterParser
    {
        /// <summary>Extract the `description:` value from frontmatter. Returns <paramref name="fallback"/> when not found.</summary>
        internal static string ParseDescription(string fileText, string fallback = "") =>
            ParseKey(fileText, "description:", fallback);

        /// <summary>Extract the `model:` value from frontmatter. Returns <paramref name="fallback"/> when not found.</summary>
        internal static string ParseModel(string fileText, string fallback = "") =>
            ParseKey(fileText, "model:", fallback);

        // Shared extractor for any frontmatter key: "key:" → value (unquoted, trimmed).
        private static string ParseKey(string fileText, string key, string fallback)
        {
            if (string.IsNullOrEmpty(fileText)) return fallback;
            var text  = fileText.Replace("\r\n", "\n").TrimStart('\n', '\r', ' ');
            var lines = text.Split('\n');
            if (lines.Length < 2 || lines[0].Trim() != "---") return fallback;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line == "---") break;
                if (!line.StartsWith(key, StringComparison.Ordinal)) continue;
                var value = line.Substring(key.Length).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2).Trim();
                else if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                    value = value.Substring(1, value.Length - 2).Trim();
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            return fallback;
        }

        /// <summary>
        /// Extract the `name:` value from the first YAML frontmatter block.
        /// Falls back to <paramref name="fileStem"/> when not found.
        /// </summary>
        internal static string ParseName(string fileText, string fileStem) =>
            ParseKey(fileText, "name:", fileStem);
    }
}
