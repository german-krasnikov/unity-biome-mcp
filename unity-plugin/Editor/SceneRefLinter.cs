// Read-only scene reference linter. Extracts path tokens from DSL lines
// and validates them against the live scene via SceneRefResolver.
using System.Collections.Generic;
using System.Linq;

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

            var lines = script.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed[0] == '#') continue;

                var tokens = trimmed.Split(new[] { ' ', '\t' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0) continue;
                if (_skipLineKeys.Contains(tokens[0])) continue;

                int lineNo = i + 1;
                foreach (var token in tokens)
                {
                    if (!IsPathToken(token)) continue;

                    // Embedded $alias inside a path (not at start) — not supported.
                    if (token[0] != '$' && token.IndexOf('$') > 0)
                    {
                        issues.Add(new LintIssue
                        {
                            Severity = "ERROR", Line = lineNo, Token = token,
                            Message = "embedded alias not supported — use $alias as a standalone token",
                        });
                        continue;
                    }

                    var r = SceneRefResolver.ResolveOne(token, System.Array.Empty<string>());
                    if (r.Status == "MISS")
                    {
                        bool isAlias = token[0] == '$';
                        issues.Add(new LintIssue
                        {
                            Severity = "ERROR", Line = lineNo, Token = token,
                            Message = isAlias
                                ? $"unresolved alias: {token}"
                                : $"object not found: {token}",
                        });
                    }
                    else if (r.Status == "AMB")
                    {
                        issues.Add(new LintIssue
                        {
                            Severity = "WARN", Line = lineNo, Token = token,
                            Message = $"ambiguous — {r.Reason}",
                        });
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
