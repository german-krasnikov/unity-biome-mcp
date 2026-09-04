// Static linter for .playtest DSL scripts. Read-only — no Play Mode required.
// Three passes: raw line scan → parse → semantic checks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class PlaytestLinter
    {
        internal static readonly HashSet<StepType> _evidenceTypes = new HashSet<StepType> {
            StepType.Assert, StepType.WaitUntil, StepType.AssertConsoleClean,
            StepType.AssertBatch, StepType.AssertCaptured, StepType.Capture,
            StepType.AssertOneActive, StepType.AssertChanged,
            StepType.AssertFramesDiffer, StepType.AssertFramesStatic
        };

        internal struct LintIssue
        {
            public string Severity;   // "ERROR", "WARN", "INFO"
            public string File;
            public int    Line;       // 0 = file-level
            public string Message;
        }

        internal static string LintFile(string projectRelativePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
            if (!fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && fullPath != projectRoot)
                return "ERROR  (project root guard)  path outside project: " + projectRelativePath;
            if (!File.Exists(fullPath))
                return "ERROR  " + projectRelativePath + "  file not found";
            return LintScript(File.ReadAllText(fullPath, System.Text.Encoding.UTF8), projectRelativePath);
        }

        internal static string LintScript(string script, string fileLabel = "<inline>")
        {
            var issues = new List<LintIssue>();
            var rawLines = script.Split('\n');

            // Inject same alias context PlaytestRunner uses so $sigils resolve correctly.
            var guids = AssetDatabase.FindAssets("t:PlaytestConfig");
            PlaytestConfig config = null;
            if (guids.Length > 0)
                config = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var cfgBlock = config?.aliases?.Count > 0
                ? PlaytestAliasHelpers.FormatVALBlock(config.aliases) + "\n"
                : "";
            var tagLines = string.Join("\n",
                UnityEditorInternal.InternalEditorUtility.tags
                    .Select(t => $"VAL ${t.Replace(" ", "_")} {t}"));
            var fullScript = tagLines + "\n" + cfgBlock + script;

            // Pass 1: raw line scans (syntactic, no parse needed)
            bool inComment = false;
            for (int i = 0; i < rawLines.Length; i++)
            {
                var trimmed = rawLines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                if (trimmed.Equals("COMMENT", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("COMMENT ", StringComparison.OrdinalIgnoreCase))
                { inComment = true; continue; }
                if (trimmed.Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase))
                { inComment = false; continue; }
                if (inComment) continue;
                var lineNo = i + 1;

                if (trimmed.StartsWith("ALIAS ", StringComparison.OrdinalIgnoreCase))
                    issues.Add(Issue("ERROR", fileLabel, lineNo, "deprecated ALIAS keyword — use VAL instead"));

                if (trimmed.StartsWith("TRACE_FLOW ", StringComparison.OrdinalIgnoreCase))
                    issues.Add(Issue("WARN", fileLabel, lineNo,
                        "TRACE_FLOW is parsed but not yet implemented; step is silently skipped"));

                if (trimmed.StartsWith("WAIT_UNTIL ", StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains(" AND ") && trimmed.Contains(" OR "))
                    issues.Add(Issue("WARN", fileLabel, lineNo,
                        "WAIT_UNTIL mixes AND and OR — behaviour is AND-only (last one wins)"));

                if (trimmed.StartsWith("ASSERT_ONE_ACTIVE ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                        issues.Add(Issue("ERROR", fileLabel, lineNo,
                            "ASSERT_ONE_ACTIVE requires at least 2 paths"));
                }
            }

            // Pass 1b: TIMESCALE risk — raw scan so # @timescale-ok comments are visible
            CheckTimescaleRisk(rawLines, issues, fileLabel);

            // Pass 2: try parse (catches unknown macros, wrong arg counts)
            ParseResult parsed = null;
            try
            {
                parsed = PlaytestParser.Parse(fullScript);
            }
            catch (ArgumentException ex)
            {
                issues.Add(Issue("ERROR", fileLabel, 0, "parse error: " + ex.Message));
            }
            catch (Exception ex)
            {
                issues.Add(Issue("ERROR", fileLabel, 0, "unexpected parse error: " + ex.Message));
            }

            // Pass 3: semantic checks on parsed steps
            if (parsed != null)
            {
                if (parsed.Warnings != null)
                    foreach (var w in parsed.Warnings)
                        issues.Add(Issue("WARN", fileLabel, 0, w));

                bool hasEvidence = parsed.Steps.Any(s => _evidenceTypes.Contains(s.Type));
                if (!hasEvidence)
                    issues.Add(Issue("ERROR", fileLabel, 0,
                        "no evidence commands (ASSERT/WAIT_UNTIL/ASSERT_CONSOLE_CLEAN/ASSERT_BATCH/ASSERT_CAPTURED/ASSERT_CHANGED/ASSERT_ONE_ACTIVE/ASSERT_FRAMES_DIFFER/ASSERT_FRAMES_STATIC)"));

                if (hasEvidence && !HasCleanup(parsed))
                    issues.Add(Issue("ERROR", fileLabel, 0,
                        "no ASSERT_CONSOLE_CLEAN at end; add ASSERT_CONSOLE_CLEAN or 'CALL finish_clean'"));

                // C11: an MCP step whose next main-section step isn't an evidence
                // step is likely a call whose result nobody checked. Main steps
                // only (parsed.Steps never contains TEARDOWN — those live in
                // parsed.TeardownSteps — so a cleanup MCP there is never scanned).
                for (int mi = 0; mi < parsed.Steps.Count; mi++)
                {
                    if (parsed.Steps[mi].Type != StepType.Mcp) continue;
                    var following = mi + 1 < parsed.Steps.Count ? parsed.Steps[mi + 1] : null;
                    if (following == null || !_evidenceTypes.Contains(following.Type))
                        issues.Add(Issue("WARN", fileLabel, 0,
                            $"MCP step '{parsed.Steps[mi].Method}' has no following evidence step " +
                            "(ASSERT/WAIT_UNTIL/ASSERT_CONSOLE_CLEAN/ASSERT_BATCH/ASSERT_CAPTURED/" +
                            "CAPTURE/ASSERT_ONE_ACTIVE/ASSERT_CHANGED/ASSERT_FRAMES_DIFFER/ASSERT_FRAMES_STATIC)"));
                }
            }

            return FormatReport(fileLabel, issues);
        }

        static void CheckTimescaleRisk(string[] rawLines, List<LintIssue> issues, string fileLabel)
        {
            float timescale = 1f;
            bool inComment = false;
            bool nextSuppressed = false;

            for (int i = 0; i < rawLines.Length; i++)
            {
                var trimmed = rawLines[i].Trim();
                if (string.IsNullOrEmpty(trimmed)) { nextSuppressed = false; continue; }

                if (trimmed.Equals("COMMENT", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("COMMENT ", StringComparison.OrdinalIgnoreCase))
                { inComment = true; continue; }
                if (trimmed.Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase))
                { inComment = false; continue; }
                if (inComment) continue;

                if (trimmed.StartsWith("#"))
                {
                    if (trimmed.Contains("@timescale-ok")) nextSuppressed = true;
                    continue;
                }

                var lineNo = i + 1;

                if (trimmed.StartsWith("TIMESCALE ", StringComparison.OrdinalIgnoreCase))
                {
                    var valStr = trimmed.Substring("TIMESCALE ".Length).Trim();
                    if (float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float ts))
                        timescale = ts;
                    nextSuppressed = false;
                    continue;
                }

                if (timescale > 1f && !nextSuppressed &&
                    (trimmed.StartsWith("WAIT_UNTIL ", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith("CAPTURE_MIN ", StringComparison.OrdinalIgnoreCase)
                     || trimmed.StartsWith("CAPTURE_MAX ", StringComparison.OrdinalIgnoreCase)))
                {
                    var cmd = trimmed.Split(' ')[0].ToUpperInvariant();
                    issues.Add(Issue("WARN", fileLabel, lineNo,
                        $"TIMESCALE_WARN: '{cmd}' at line {lineNo} runs at TIMESCALE {timescale}x" +
                        " — poll-based runner may miss sub-tick conditions." +
                        " Add '# @timescale-ok' to suppress."));
                }

                nextSuppressed = false;
            }
        }

        static bool HasCleanup(ParseResult parsed)
        {
            var last = parsed.Steps.LastOrDefault(s =>
                s.Type != StepType.Section && s.Type != StepType.Desc);
            return last != null &&
                   (last.Type == StepType.AssertConsoleClean
                    || (last.Type == StepType.TimeScale
                        && parsed.Steps.Any(s => s.Type == StepType.AssertConsoleClean)));
        }

        static LintIssue Issue(string severity, string file, int line, string message) =>
            new LintIssue { Severity = severity, File = file, Line = line, Message = message };

        static string FormatReport(string fileLabel, List<LintIssue> issues)
        {
            if (issues.Count == 0) return $"OK  {fileLabel}  no issues";
            return string.Join("\n", issues.Select(i =>
                $"{i.Severity,-5}  {i.File}{(i.Line > 0 ? ":" + i.Line : "")}  {i.Message}"));
        }
    }
}
