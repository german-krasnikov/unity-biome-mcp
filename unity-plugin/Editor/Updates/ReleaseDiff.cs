using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static class ReleaseDiff
    {
        internal class DiffSection
        {
            public string       Header;
            public List<string> Bullets;
        }

        static readonly Regex HeaderRx = new Regex(@"^\*\*(.+?):\*\*\s*$", RegexOptions.Multiline);
        static readonly Regex BulletRx = new Regex(@"^[-*]\s+(.+)$", RegexOptions.Multiline);

        internal static List<DiffSection> Compute(List<ChangelogReader.Entry> entries, string fromVersion)
        {
            var result = new List<DiffSection>();
            foreach (var entry in entries)
            {
                if (!IsNewer(entry.Version, fromVersion)) continue;
                result.AddRange(ParseSections(entry.Content));
            }
            return result;
        }

        static List<DiffSection> ParseSections(string content)
        {
            var sections = new List<DiffSection>();
            var current  = new DiffSection { Header = "", Bullets = new List<string>() };

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                var hm   = HeaderRx.Match(line);
                if (hm.Success)
                {
                    if (current.Bullets.Count > 0) sections.Add(current);
                    current = new DiffSection { Header = hm.Groups[1].Value.Trim(), Bullets = new List<string>() };
                    continue;
                }
                var bm = BulletRx.Match(line);
                if (bm.Success) current.Bullets.Add(bm.Groups[1].Value.Trim());
            }
            if (current.Bullets.Count > 0) sections.Add(current);
            return sections;
        }

        // UI-1/Q16: the one DECISION isNewer (dsl/decisions/versionorder.logic), shared with UpdateChecker and
        // ChangelogReader and with server/src/unity_mcp/_update_check.py.
        static bool IsNewer(string candidate, string fromVersion) => Decisions.VersionFacts.IsNewer(candidate, fromVersion);
    }
}
