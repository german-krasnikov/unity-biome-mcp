// Read-only scene reference linter. Extracts path tokens from DSL lines
// and validates them against the live scene via SceneRefResolver.
using System.Collections.Generic;
using System.Linq;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class SceneRefLinter
    {
        internal struct LintIssue
        {
            internal string Severity;  // "ERROR", "WARN"
            internal int    Line;
            internal string Token;
            internal string Message;
        }

        // Lines starting with these are meta/definition — no path tokens to check.
        private static readonly HashSet<string> _skipLineKeys =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "VAL", "VAR", "INCLUDE", "MACRO", "END_MACRO",
                "SECTION", "DESC", "CALL", "ABORT_ON_FAIL",
            };

        // DSL verbs: not path refs.
        private static readonly HashSet<string> _dslVerbs =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "INVOKE", "MOVE", "MOVE_PATH", "SWEEP_PATH", "TELEPORT",
                "ASSERT", "ASSERT_CONSOLE_CLEAN", "ASSERT_BATCH", "ASSERT_NEAR",
                "ASSERT_CAPTURED", "ASSERT_CONSERVED", "ASSERT_CTA",
                "WAIT", "WAIT_UNTIL", "SNAPSHOT", "SET", "LOG", "TIMESCALE",
                "CAPTURE", "INVARIANT", "SIMULATE", "MONITOR", "TRACE_FLOW",
                "CLICK", "TAP", "WAIT_CAPTURED", "COMPLETE_PURCHASE", "INVOKE_REPEAT",
                "==", "!=", "<", ">", "<=", ">=", "AND", "OR",
            };

        internal static List<LintIssue> LintScript(string script)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(script)) return issues;

            // Parse to collect VAL definitions (including from INCLUDEs) so
            // $alias tokens that are defined don't produce false-positive MISS.
            Dictionary<string, string> valDefs = null;
            try
            {
                var parsed = PlaytestParser.Parse(script);
                valDefs = parsed.ValDefs;
            }
            catch { /* parse failure = lint without alias knowledge */ }

            // INCLUDE lines (and other meta keywords) are in _skipLineKeys and are skipped entirely,
            // so filenames are never linted as scene paths.
            // G14: comma-separated path tokens (e.g. "/A|T,/B|T") are split before path checking below.
            var lines = script.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed[0] == '#') continue;

                var tokens = PlaytestParser.SplitTokens(trimmed);
                if (tokens.Length == 0) continue;
                if (_skipLineKeys.Contains(tokens[0])) continue;

                int lineNo = i + 1;
                foreach (var rawToken in tokens)
                {
                    // G14: split comma-separated path tokens (e.g. ASSERT_BATCH /A|Comp,/B|Comp)
                    var subTokens = rawToken.Contains(',')
                        ? rawToken.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                        : new[] { rawToken };

                    foreach (var token in subTokens)
                    {
                        var t = token.Trim();
                        if (!IsPathToken(t)) continue;

                        // Embedded $alias inside a path (not at start) — not supported.
                        if (t[0] != '$' && t.IndexOf('$') > 0)
                        {
                            issues.Add(new LintIssue
                            {
                                Severity = "ERROR", Line = lineNo, Token = t,
                                Message = "embedded alias not supported — use $alias as a standalone token",
                            });
                            continue;
                        }

                        // Skip $alias tokens that are defined via VAL (no false-positive MISS)
                        if (t[0] == '$' && valDefs != null
                            && valDefs.ContainsKey(t.TrimStart('$').Split('|')[0]))
                            continue;

                        var r = SceneRefResolver.ResolveOne(t, System.Array.Empty<string>());
                        if (r.Status == "MISS")
                        {
                            bool isAlias = t[0] == '$';
                            issues.Add(new LintIssue
                            {
                                Severity = "ERROR", Line = lineNo, Token = t,
                                Message = isAlias
                                    ? $"unresolved alias: {t}"
                                    : $"object not found: {t}",
                            });
                        }
                        else if (r.Status == "AMB")
                        {
                            issues.Add(new LintIssue
                            {
                                Severity = "WARN", Line = lineNo, Token = t,
                                Message = $"ambiguous — {r.Reason}",
                            });
                        }
                    }
                }
            }

            return issues;
        }

        private static bool IsPathToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (_dslVerbs.Contains(token)) return false;
            return token[0] == '$' || token[0] == '/' || token.Contains('|');
        }

        internal static string FormatReport(string source, List<LintIssue> issues)
        {
            if (issues.Count == 0) return $"OK  {source}  no issues";
            return string.Join("\n", issues.Select(i =>
                $"{i.Severity,-5}  {source}{(i.Line > 0 ? ":" + i.Line : "")}  {i.Token}  {i.Message}"));
        }
    }
}
