using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Roslyn
{
    internal static class UnityPreflightHints
    {
        internal static string Analyze(string filePath, string newContent)
        {
            var sb = new StringBuilder();
            CheckSerializedDictionary(newContent, sb);
            CheckSerializedNonSerializableType(newContent, sb);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                CheckRenamedWithoutFormerlySerializedAs(filePath, newContent, sb);
            return sb.ToString().TrimEnd();
        }

        private static void CheckSerializedDictionary(string content, StringBuilder sb)
        {
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("[SerializeField]") && !lines[i].Contains("[SerializeField,"))
                    continue;
                for (int j = i; j < Math.Min(i + 4, lines.Length); j++)
                {
                    if (lines[j].Contains("Dictionary<"))
                    {
                        sb.AppendLine($"WARN: serialized_dictionary (line {j + 1}) — Unity cannot serialize Dictionary<,>; use List<SerializablePair> or a custom wrapper");
                        break;
                    }
                }
            }
        }

        private static readonly Regex _nonSerializableRe = new Regex(
            @"\[SerializeField[^\]]*\]\s*(?:private|protected|public|internal)?\s*(I[A-Z]\w+|abstract\s+\w+)\s+\w+",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static void CheckSerializedNonSerializableType(string content, StringBuilder sb)
        {
            foreach (Match m in _nonSerializableRe.Matches(content))
            {
                var lineNum = content.Take(m.Index).Count(c => c == '\n') + 1;
                sb.AppendLine($"WARN: non_serializable_type (line {lineNum}) — [SerializeField] on interface/abstract type '{m.Groups[1].Value}'");
            }
        }

        private static readonly Regex _fieldRe = new Regex(
            @"\[SerializeField[^\]]*\][^;]*?\s+(\w+)\s*[;=]",
            RegexOptions.Compiled);

        private static void CheckRenamedWithoutFormerlySerializedAs(string existingFilePath, string newContent, StringBuilder sb)
        {
            var existingContent = File.ReadAllText(existingFilePath);
            var oldFields = new HashSet<string>(_fieldRe.Matches(existingContent).Cast<Match>().Select(m => m.Groups[1].Value));
            var newFields = new HashSet<string>(_fieldRe.Matches(newContent).Cast<Match>().Select(m => m.Groups[1].Value));

            foreach (var oldName in oldFields.Except(newFields))
            {
                if (!newContent.Contains($"FormerlySerializedAs(\"{oldName}\")"))
                    sb.AppendLine($"WARN: missing_formerly_serialized_as — field '{oldName}' removed without [FormerlySerializedAs]; serialized data will be lost");
            }
        }
    }
}
