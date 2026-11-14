using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class ChangelogReader
    {
        public struct Entry
        {
            public string Version;
            public string Date;
            public string Content;
            public bool   IsNewer;
        }

        // ## [v0.38.0] — 2026-06-19 <!-- optional comment -->
        static readonly Regex HeaderRx = new Regex(
            @"^## \[v?(\d+\.\d+\.\d+(?:\.\d+)?|\[?Unreleased\]?)\](?:[^\n]*)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static string LocatePath()
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChangelogReader).Assembly);
                if (info != null)
                {
                    var p = Path.Combine(info.resolvedPath, "CHANGELOG.md");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }

            // Walk up from project Assets folder
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
            {
                var p = Path.Combine(dir.FullName, "CHANGELOG.md");
                if (File.Exists(p)) return p;
            }
            return null;
        }

        public static List<Entry> Parse(string content, string currentVersion)
        {
            var entries = new List<Entry>();
            if (string.IsNullOrEmpty(content)) return entries;

            var matches = HeaderRx.Matches(content);
            for (int i = 0; i < matches.Count; i++)
            {
                var m       = matches[i];
                var raw     = m.Groups[1].Value;
                var isUnrel = raw.IndexOf("Unreleased", StringComparison.OrdinalIgnoreCase) >= 0;
                var version = isUnrel ? "Unreleased" : raw.TrimStart('v');

                // Extract date from header line (look for YYYY-MM-DD)
                var date    = "";
                var dateM   = Regex.Match(m.Value, @"\d{4}-\d{2}-\d{2}");
                if (dateM.Success) date = dateM.Value;

                // Body = text between this header and next header (or end)
                int bodyStart = m.Index + m.Length;
                int bodyEnd   = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
                var body      = content.Substring(bodyStart, bodyEnd - bodyStart).Trim();

                bool isNewer = !isUnrel && IsNewerThan(version, currentVersion);

                entries.Add(new Entry { Version = version, Date = date, Content = body, IsNewer = isNewer });
            }
            return entries;
        }

        // UI-1/Q16: the one DECISION isNewer (dsl/decisions/versionorder.logic), shared with UpdateChecker and
        // ReleaseDiff and with server/src/unity_mcp/_update_check.py.
        static bool IsNewerThan(string candidate, string current) => Decisions.VersionFacts.IsNewer(candidate, current);
    }
}
