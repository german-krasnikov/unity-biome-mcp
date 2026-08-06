using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static class SceneDiffHelper
    {
        private static string _lastSnapshot;

        // P-021: strip volatile #instanceID tokens before line comparison.
        internal static string NormalizeSnapshot(string text)
            => Regex.Replace(text, @"\s+#\d+", "");

        public static string Diff()
        {
            var current = NormalizeSnapshot(HierarchySerializer.Serialize(depth: 99));

            if (_lastSnapshot == null)
            {
                _lastSnapshot = current;
                return "SNAPSHOT SAVED (first call — no diff yet)";
            }

            var prev = NormalizeSnapshot(_lastSnapshot);
            _lastSnapshot = current;

            var prevLines = new HashSet<string>(prev.Split('\n'));
            var currLines = current.Split('\n');
            var currSet = new HashSet<string>(currLines);

            var sb = new StringBuilder();
            int added = 0, removed = 0;

            foreach (var line in currLines)
                if (line.Length > 0 && !prevLines.Contains(line)) { sb.AppendLine("+ " + line); added++; }

            foreach (var line in prev.Split('\n'))
                if (line.Length > 0 && !currSet.Contains(line)) { sb.AppendLine("- " + line); removed++; }

            if (added == 0 && removed == 0) return "NO CHANGES";
            return $"DIFF: +{added} -{removed}\n{sb.ToString().TrimEnd()}";
        }

        // Reset for tests
        internal static void Reset() => _lastSnapshot = null;
    }
}
