using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Playtest.Core
{
    /// <summary>Parsed `# @directive` header metadata from a .playtest script.
    /// Pure data — zero UnityEditor/UnityEngine dependency (Wave D Core extraction seam).</summary>
    public sealed class PlaytestHeader
    {
        public bool NeedsEditmode;
        public bool NeedsPlaymode;
        public List<string> Tags = new List<string>();
        public int? ExpectSteps;
        public int? ExpectFailed;
        public bool SuiteOnly;
    }

    /// <summary>Scans the raw pre-INCLUDE lines of a .playtest script for `# @directive` header
    /// lines (`@needs`, `@tags`, `@expect`, `@suite-only`). Zero `UnityEditor`/`UnityEngine`
    /// dependency — pure string processing, forward-compatible with the Wave D Core extraction
    /// (extraction itself is not done here). Directives live in the main file only; INCLUDE-d
    /// content is never scanned (documented MVP constraint). Precedent for `# @directive`:
    /// `PlaytestLinter.cs:152` (`@timescale-ok`).</summary>
    internal static class PlaytestHeaderScanner
    {
        private static readonly Regex DirectiveLine = new Regex(@"^#\s*@(\w+)\s*(.*)$", RegexOptions.Compiled);
        private static readonly char[] Space = { ' ' };

        // Known directive keys. A hyphenated author-facing name (e.g. "@suite-only") is captured
        // by DirectiveLine only up to the hyphen — "\w+" does not include '-' — so its recognized
        // key is the pre-hyphen prefix ("suite"); the remainder ("-only") is an unused suffix.
        // This is also why an unrelated hyphenated directive like the shipped "@timescale-ok"
        // resolves to the key "timescale", which is not in this known set (falls to default: ignored).
        private const string DirectiveNeeds  = "needs";
        private const string DirectiveTags   = "tags";
        private const string DirectiveExpect = "expect";
        private const string DirectiveSuite   = "suite";
        private const string NeedsEditmodeToken = "editmode";
        private const string NeedsPlaymodeToken = "playmode";
        private const string ExpectStepsKey  = "steps";
        private const string ExpectFailedKey = "failed";

        internal static PlaytestHeader Scan(string script)
        {
            var header = new PlaytestHeader();
            if (string.IsNullOrEmpty(script)) return header;

            var tags = new HashSet<string>();
            // By design: scans all lines, not just the leading comment block — @directives
            // may appear after code lines.
            foreach (var rawLine in script.Split('\n'))
            {
                var match = DirectiveLine.Match(rawLine.Trim());
                if (!match.Success) continue;

                var key = match.Groups[1].Value.ToLowerInvariant();
                var rest = match.Groups[2].Value.Trim();
                ApplyDirective(header, tags, key, rest);
            }
            // Dedup + sort for determinism — HashSet iteration order is not guaranteed.
            var sortedTags = new List<string>(tags);
            sortedTags.Sort(StringComparer.Ordinal);
            header.Tags = sortedTags;
            return header;
        }

        private static void ApplyDirective(PlaytestHeader header, HashSet<string> tags, string key, string rest)
        {
            switch (key)
            {
                case DirectiveNeeds:
                    foreach (var token in rest.Split(Space, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (token.Equals(NeedsEditmodeToken, StringComparison.OrdinalIgnoreCase))
                            header.NeedsEditmode = true;
                        else if (token.Equals(NeedsPlaymodeToken, StringComparison.OrdinalIgnoreCase))
                            header.NeedsPlaymode = true;
                    }
                    break;

                case DirectiveTags:
                    foreach (var t in rest.Split(Space, StringSplitOptions.RemoveEmptyEntries))
                        tags.Add(t);
                    break;

                case DirectiveExpect:
                    foreach (var pair in rest.Split(Space, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var eq = pair.IndexOf('=');
                        if (eq < 0) continue;
                        var pkey = pair.Substring(0, eq);
                        var pval = pair.Substring(eq + 1);
                        if (pkey.Equals(ExpectStepsKey, StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(pval, out var steps))
                            header.ExpectSteps = steps;
                        else if (pkey.Equals(ExpectFailedKey, StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(pval, out var failed))
                            header.ExpectFailed = failed;
                    }
                    break;

                case DirectiveSuite:
                    header.SuiteOnly = true;
                    break;

                default:
                    // Unknown directive (e.g. "@timescale-ok" truncates to key "timescale") —
                    // silently ignored, never an error, never a flag mutation. R-18: forward-compat.
                    break;
            }
        }
    }
}
