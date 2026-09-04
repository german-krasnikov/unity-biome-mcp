using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    // Directive-handling logic for the DSL header (`# @needs`/`@tags`/`@expect`/`@suite-only`).
    // Kept out of PlaytestParser.cs per R-04 / csharp-unity.md file-size convention — that file
    // is already >1400 lines. B09 (compile-time Play-bound-verb rejection under `@needs editmode`)
    // and C06 extend this same partial; PlaytestParser.cs itself gains only the single call site
    // in Parse() (B04/B09).
    internal static partial class PlaytestParser
    {
        /// <summary>Scans the raw pre-INCLUDE script text for `# @directive` header lines.
        /// Single point of contact between the parser and <see cref="PlaytestHeaderScanner"/>.</summary>
        internal static PlaytestHeader ScanHeader(string script) => PlaytestHeaderScanner.Scan(script);

        // B09 / INV-022: these verbs require Play Mode to actually do anything (Move drives a
        // live rigidbody/transform tween, Simulate ticks a runtime simulator, CaptureFrames
        // samples successive rendered frames, TimeScale changes Time.timeScale) — none of that
        // exists under `# @needs editmode`, so they are rejected as a compile error rather than
        // silently no-op-ing or throwing at runtime. MOVE_PATH/SWEEP_PATH desugar to StepType.Move
        // at parse time (see PlaytestParser.cs "MOVE_PATH"/"SWEEP_PATH" cases), so checking the
        // expanded step list catches both without a separate case.
        private static readonly StepType[] PlayBoundStepTypes =
        {
            StepType.Move, StepType.Simulate, StepType.CaptureFrames, StepType.TimeScale
        };

        /// <summary>Post-pass over the fully-built step lists: null when <paramref name="header"/>
        /// does not declare `@needs editmode`, or when no Play-bound verb was found. Otherwise one
        /// error per offending step, in <see cref="ParseResult.Errors"/> convention (fatal —
        /// <see cref="PlaytestRunner.Run"/> aborts before executing any steps).</summary>
        internal static List<string> RejectPlayBoundVerbsUnderEditmode(
            PlaytestHeader header, List<PlaytestStep> steps, List<PlaytestStep> setupSteps, List<PlaytestStep> teardownSteps)
        {
            if (header == null || !header.NeedsEditmode) return null;

            List<string> errors = null;
            foreach (var section in new[] { steps, setupSteps, teardownSteps })
            {
                if (section == null) continue;
                foreach (var step in section)
                {
                    if (Array.IndexOf(PlayBoundStepTypes, step.Type) < 0) continue;
                    errors = errors ?? new List<string>();
                    errors.Add($"'{step.Type}' requires Play Mode and cannot run under `# @needs editmode` (line {step.SourceLine + 1})");
                }
            }
            return errors;
        }
    }
}
