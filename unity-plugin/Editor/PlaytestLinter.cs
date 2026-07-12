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
        static readonly HashSet<StepType> _evidenceTypes = new HashSet<StepType> {
            StepType.Assert, StepType.WaitUntil, StepType.AssertConsoleClean,
            StepType.AssertBatch, StepType.AssertCaptured, StepType.Capture
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
            for (int i = 0; i < rawLines.Length; i++)
            {
                var trimmed = rawLines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                var lineNo = i + 1;

                if (trimmed.StartsWith("ALIAS ", StringComparison.OrdinalIgnoreCase))
                    issues.Add(Issue("WARN", fileLabel, lineNo, "deprecated ALIAS keyword — use VAL instead"));

                if (trimmed.StartsWith("TRACE_FLOW ", StringComparison.OrdinalIgnoreCase))
                    issues.Add(Issue("WARN", fileLabel, lineNo,
                        "TRACE_FLOW is parsed but not yet implemented; step is silently skipped"));

                if (trimmed.StartsWith("WAIT_UNTIL ", StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains(" AND ") && trimmed.Contains(" OR "))
                    issues.Add(Issue("WARN", fileLabel, lineNo,
                        "WAIT_UNTIL mixes AND and OR — behaviour is AND-only (last one wins)"));
            }

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
                        "no evidence commands (ASSERT/WAIT_UNTIL/ASSERT_CONSOLE_CLEAN/ASSERT_BATCH/ASSERT_CAPTURED)"));

                if (hasEvidence && !HasCleanup(parsed))
                    issues.Add(Issue("WARN", fileLabel, 0,
                        "no ASSERT_CONSOLE_CLEAN at end; consider adding 'CALL finish_clean'"));
            }

            return FormatReport(fileLabel, issues);
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
