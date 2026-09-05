using System;
using System.Collections.Generic;

namespace UnityMCP.Playtest.Core
{
    // Directive-handling logic for the DSL header (`# @needs`/`@tags`/`@expect`/`@suite-only`).
    // Kept out of PlaytestParser.cs per R-04 / csharp-unity.md file-size convention — that file
    // is already >1400 lines. B09 (compile-time Play-bound-verb rejection under `@needs editmode`)
    // and C06 extend this same partial; PlaytestParser.cs itself gains only the single call site
    // in Parse() (B04/B09).
    public static partial class PlaytestParser
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

        // C06 — EXPECT_FAIL directive: a pendingLabel-style single-slot flag. The keyword line
        // itself carries no step (Parse()'s "EXPECT_FAIL" case `continue`s); the flag is set here
        // and consumed by the very next parsed step's own PlaytestStep.ExpectFail field, mirroring
        // how DESC's pendingLabel is consumed at the same point in Parse(). Kept here per R-04 —
        // PlaytestParser.cs itself gains only the field and a one-line case delegation.

        /// <summary>Marks that the next parsed step is expected to fail.</summary>
        internal static void SetPendingExpectFail(ref bool pendingExpectFail) => pendingExpectFail = true;

        /// <summary>Consumes the pending flag into <paramref name="step"/> and resets the slot,
        /// exactly like pendingLabel's own consumption just above this call site in Parse().</summary>
        internal static void ConsumePendingExpectFail(PlaytestStep step, ref bool pendingExpectFail)
        {
            step.ExpectFail = pendingExpectFail;
            pendingExpectFail = false;
        }
    }
}
