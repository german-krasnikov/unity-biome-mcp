// Idempotently ensures the project-root .gitignore lists all generated per-project MCP
// config paths, under one marker comment block. Never duplicates, never touches
// unrelated lines.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Wizard
{
    internal static class GitignorePatcher
    {
        internal const string MarkerLine = "# unity-biome-mcp (auto-generated project configs)";

        // Pure — string in, string out. No file I/O. Idempotent: calling twice with the
        // same input+entries returns identical output both times.
        internal static string EnsureEntries(string existingText, IEnumerable<string> relativePaths)
        {
            existingText = existingText ?? "";
            var existingLines = existingText.Replace("\r\n", "\n").Split('\n');
            var normalizedExisting = new HashSet<string>();
            foreach (var line in existingLines)
                normalizedExisting.Add(Normalize(line));

            var missing = new List<string>();
            foreach (var path in relativePaths)
                if (normalizedExisting.Add(Normalize(path)))
                    missing.Add(path);

            if (missing.Count == 0) return existingText;

            var sb = new StringBuilder(existingText);
            if (sb.Length > 0 && !existingText.EndsWith("\n")) sb.Append("\n");

            if (!existingText.Contains(MarkerLine))
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(MarkerLine).Append("\n");
            }

            foreach (var path in missing)
                sb.Append(path).Append("\n");

            return sb.ToString();
        }

        // Thin I/O wrapper: read <root>/.gitignore (or "" if missing) → EnsureEntries →
        // write back ONLY if changed, to avoid mtime/git-diff noise on every Editor session.
        internal static void Apply(string projectRoot, IEnumerable<string> relativePaths)
        {
            var path = Path.Combine(projectRoot, ".gitignore");
            try
            {
                var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
                var updated = EnsureEntries(existing, relativePaths);
                if (updated == existing) return;
                File.WriteAllText(path, updated, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                // Read-only FS / permission denied — never throw out of delayCall.
                Debug.LogWarning($"unity-biome-mcp: could not write .gitignore: {ex.Message}");
            }
        }

        private static string Normalize(string line) => line.Trim().TrimEnd('/');
    }
}
