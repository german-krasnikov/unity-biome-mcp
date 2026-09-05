using System.Collections.Generic;

namespace UnityMCP.Playtest.Core
{
    /// <summary>One reason PlayerPlaytestRunner may not execute a parsed script.
    /// RawLine is null for script-level violations (header/setup/teardown/errors/empty
    /// main section) — those aren't attached to any single step.</summary>
    public readonly struct PreflightViolation
    {
        public readonly string RawLine;
        public readonly string Reason;

        public PreflightViolation(string rawLine, string reason)
        {
            RawLine = rawLine;
            Reason = reason;
        }
    }

    /// <summary>
    /// Pure, engine-free gate (PR-01C/F1): decides once, before any Player step runs,
    /// whether PlayerPlaytestRunner may execute a fully-parsed ParseResult at all.
    /// Single owner of the Player-executable step-type whitelist — PlayerPlaytestRunner
    /// no longer keeps its own copy. See Plans/PR-01C.md for the grounded rule table.
    /// </summary>
    public static class PlayerProfilePreflight
    {
        // The exact 9 StepTypes the fixtures under Assets/StreamingAssets/Playtests/
        // use (D11). Do NOT widen without a corresponding Execute() case in
        // PlayerPlaytestRunner — Move/Section/Setup/Monitor etc. are deliberately out
        // of scope (no fixture needs them; unbounded scope creep). Single owner —
        // PlayerPlaytestRunner no longer keeps its own copy of this set.
        public static readonly HashSet<StepType> SupportedStepTypes = new()
        {
            StepType.Assert, StepType.AssertConsoleClean, StepType.Invoke, StepType.Log,
            StepType.Set, StepType.Snapshot, StepType.TimeScale, StepType.WaitUntil, StepType.Wait,
        };

        public static List<PreflightViolation> Validate(ParseResult parsed)
        {
            var violations = new List<PreflightViolation>();

            if (parsed.Errors != null)
                foreach (var error in parsed.Errors)
                    violations.Add(new PreflightViolation(null, "parse error: " + error));

            if (parsed.Header != null && parsed.Header.NeedsEditmode)
                violations.Add(new PreflightViolation(null,
                    "script declares '# @needs editmode' — Player cannot execute EditMode-only scripts"));

            if (parsed.SetupSteps is { Count: > 0 })
                violations.Add(new PreflightViolation(null, "SETUP block is not supported in Player"));

            if (parsed.TeardownSteps is { Count: > 0 })
                violations.Add(new PreflightViolation(null, "TEARDOWN block is not supported in Player"));

            if (parsed.Steps.Count == 0)
                violations.Add(new PreflightViolation(null,
                    "script has no executable steps in Player (empty main section)"));

            foreach (var step in parsed.Steps)
                ValidateStep(step, violations);

            return violations;
        }

        private static void ValidateStep(PlaytestStep step, List<PreflightViolation> violations)
        {
            if (!SupportedStepTypes.Contains(step.Type))
            {
                violations.Add(new PreflightViolation(step.RawLine, "unsupported step type in Player: " + step.Type));
                return; // type-specific modifier checks below are meaningless for an unrunnable type
            }

            if (step.ExpectFail)
                violations.Add(new PreflightViolation(step.RawLine, "EXPECT_FAIL is not supported in Player"));

            if (step.Type == StepType.WaitUntil && step.Queries is { Length: > 0 })
                violations.Add(new PreflightViolation(step.RawLine,
                    "compound AND/OR WAIT_UNTIL condition is not supported in Player"));

            if (step.Type == StepType.Assert && step.HasExplicitTimeout)
                violations.Add(new PreflightViolation(step.RawLine,
                    "ASSERT ... TIMEOUT retry semantics are not supported in Player"));
        }
    }
}
