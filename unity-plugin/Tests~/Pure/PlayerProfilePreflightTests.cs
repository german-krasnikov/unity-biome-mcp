using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace UnityMCP.Playtest.Core.PureTests
{
    // PR-01C / F1 — PlayerProfilePreflight is the single pre-execution gate deciding
    // whether PlayerPlaytestRunner may run a fully-parsed ParseResult at all. Pure
    // function (ParseResult in, List<PreflightViolation> out) — the whole suite lives
    // in the zero-Unity Pure lane, same as PlaytestCorpusParseTests.cs / CompareParityTests.cs.
    // See Plans/PR-01C.md for the grounded rule table these tests exercise.
    [TestFixture]
    public class PlayerProfilePreflightTests
    {
        [Test]
        public void Validate_FatalParseErrors_ReturnsOneViolationPerError()
        {
            // TIMESCALE is a Play-bound verb (B09) — under `# @needs editmode` the
            // parser itself already records a fatal ParseResult.Errors entry (finding #2).
            var parsed = PlaytestParser.Parse("# @needs editmode\nTIMESCALE 0.5\n");
            Assert.IsNotNull(parsed.Errors, "sanity: parser must have recorded a fatal error for this script");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason.StartsWith("parse error:", StringComparison.Ordinal)),
                "expected a violation whose Reason starts with 'parse error:'");
        }

        [Test]
        public void Validate_NeedsEditmodeHeaderAlone_Rejected()
        {
            // No Play-bound step here, so parsed.Errors stays null — proves this rule
            // is genuinely distinct from the fatal-parse-errors rule above, not a duplicate.
            var parsed = PlaytestParser.Parse("# @needs editmode\nLOG hi\n");
            Assert.IsNull(parsed.Errors, "sanity: no Play-bound verb, parser should not fatal-error");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.AreEqual(1, violations.Count, "expected exactly one violation");
            Assert.IsTrue(violations[0].Reason.Contains("@needs editmode"));
        }

        [Test]
        public void Validate_SetupBlock_Rejected()
        {
            var parsed = PlaytestParser.Parse(
                "SETUP\nLOG setup-marker\nSETUP_END\nLOG body-marker\n" +
                "TEARDOWN\nLOG teardown-marker\nTEARDOWN_END\n");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason == "SETUP block is not supported in Player"));
        }

        [Test]
        public void Validate_TeardownBlock_Rejected()
        {
            var parsed = PlaytestParser.Parse(
                "SETUP\nLOG setup-marker\nSETUP_END\nLOG body-marker\n" +
                "TEARDOWN\nLOG teardown-marker\nTEARDOWN_END\n");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason == "TEARDOWN block is not supported in Player"));
        }

        [Test]
        public void Validate_MainSectionStepType_UnaffectedBySetupTeardownRejection()
        {
            // Proves the rejection is about the SETUP/TEARDOWN *section*, not the shared
            // Log step type: the same "LOG body-marker" line, parsed WITHOUT any
            // SETUP/TEARDOWN wrapper, must be structurally fine on its own.
            var isolated = PlaytestParser.Parse("LOG body-marker\n");

            var violations = PlayerProfilePreflight.Validate(isolated);

            Assert.AreEqual(0, violations.Count, "a bare LOG step must never be rejected on its own");
        }

        [Test]
        public void Validate_ExpectFailStep_Rejected()
        {
            var parsed = PlaytestParser.Parse("EXPECT_FAIL\nLOG this-step-succeeds\n");

            var violations = PlayerProfilePreflight.Validate(parsed);

            var violation = violations.SingleOrDefault(v => v.Reason.Contains("EXPECT_FAIL"));
            Assert.IsNotNull(violation.Reason, "expected a violation mentioning EXPECT_FAIL");
            Assert.AreEqual(parsed.Steps[0].RawLine, violation.RawLine,
                "the violation must be attached to the LOG step's RawLine");
        }

        [TestCase("AND")]
        [TestCase("OR")]
        public void Validate_CompoundWaitUntilAndOr_Rejected(string joiner)
        {
            var parsed = PlaytestParser.Parse(
                $"WAIT_UNTIL /A|Comp|flag == True {joiner} /B|Comp|flag == False TIMEOUT 1\n");
            Assert.AreEqual(1, parsed.Steps[0].Queries.Length,
                "sanity: parser must carry the second compound clause in Queries");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason ==
                "compound AND/OR WAIT_UNTIL condition is not supported in Player"));
        }

        [Test]
        public void Validate_AssertWithExplicitTimeout_Rejected()
        {
            var parsed = PlaytestParser.Parse("ASSERT /A|Comp|flag == True TIMEOUT 2\n");
            Assert.IsTrue(parsed.Steps[0].HasExplicitTimeout, "sanity: parser must set HasExplicitTimeout");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason ==
                "ASSERT ... TIMEOUT retry semantics are not supported in Player"));
        }

        [Test]
        public void Validate_PlainWaitUntilWithTimeout_NotRejected()
        {
            // Regression guard for finding #9: plain WAIT_UNTIL ... TIMEOUT n (no AND/OR)
            // already works correctly in Player today — must NOT be rejected.
            var parsed = PlaytestParser.Parse("WAIT_UNTIL /A|Comp|flag == True TIMEOUT 2\n");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.AreEqual(0, violations.Count);
        }

        [Test]
        public void Validate_EmptyMainSection_Rejected()
        {
            var parsed = PlaytestParser.Parse("# just a comment\n");
            Assert.AreEqual(0, parsed.Steps.Count, "sanity: zero parsed steps");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason.Contains("empty main section")));
        }

        [Test]
        public void Validate_UnsupportedStepType_MessageTextUnchanged()
        {
            var parsed = PlaytestParser.Parse("MOVE /Enemy TO 1,2,3\n");
            Assert.AreEqual(StepType.Move, parsed.Steps[0].Type, "sanity: parses to StepType.Move");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason == "unsupported step type in Player: Move"),
                "wording must exactly match today's inline-loop text — operator tooling greps for it");
        }

        // Regression anchor (finding #11): every fixture the built Player already ships
        // must keep passing preflight byte-for-byte. Explicit filenames (not a directory
        // glob) so a newly-added, deliberately-rejected fixture (e.g.
        // player_ci_preflight_reject.playtest) can never accidentally join this list.
        [TestCase("player_ci_bounds.playtest")]
        [TestCase("player_ci_expected_failure.playtest")]
        [TestCase("player_ci_graphics_smoke.playtest")]
        [TestCase("player_ci_multi_move.playtest")]
        [TestCase("player_ci_reset.playtest")]
        [TestCase("player_ci_smoke.playtest")]
        public void Validate_AllSixShippedFixtures_ReturnsNoViolations(string fileName)
        {
            var raw = File.ReadAllText(Path.Combine(FixtureDir, fileName), Encoding.UTF8);
            var parsed = PlaytestParser.Parse(raw);
            Assert.IsNull(parsed.Errors, $"{fileName}: unexpected parse errors");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.AreEqual(0, violations.Count,
                $"{fileName}: must pass Player preflight with zero violations (regression)");
        }

        [Test]
        public void Validate_PreflightRejectFixture_FlagsCompoundWaitUntilViolation()
        {
            // Confirms the new L5 fixture (player_ci_preflight_reject.playtest) is
            // authored correctly: its compound WAIT_UNTIL line really does carry a
            // second clause (Queries.Length > 0) and really does get rejected — the
            // dedicated CI step only proves the built-Player side (zero body-step
            // execution); this proves the DSL/rule-table side without a live Player.
            var raw = File.ReadAllText(Path.Combine(FixtureDir, "player_ci_preflight_reject.playtest"), Encoding.UTF8);
            var parsed = PlaytestParser.Parse(raw);
            Assert.IsNull(parsed.Errors, "sanity: fixture must parse without fatal errors");
            var waitUntil = parsed.Steps.Single(s => s.Type == StepType.WaitUntil);
            Assert.IsTrue(waitUntil.Queries is { Length: > 0 },
                "sanity: fixture's WAIT_UNTIL must carry a compound AND clause");

            var violations = PlayerProfilePreflight.Validate(parsed);

            Assert.IsTrue(violations.Any(v => v.Reason ==
                "compound AND/OR WAIT_UNTIL condition is not supported in Player"));
        }

        private const string FixtureRelDir = "unity-test-project/Assets/StreamingAssets/Playtests";
        private static string _repoRoot;

        // Bounded parent walk, identical pattern to PlaytestCorpusParseTests.RepoRoot() —
        // dotnet test's working directory is this assembly's own bin/ output, not the repo root.
        private static string RepoRoot()
        {
            if (_repoRoot != null) return _repoRoot;
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            for (var depth = 0; depth < 12 && dir != null; depth++, dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "unity-plugin")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "unity-test-project")))
                {
                    _repoRoot = dir.FullName;
                    return _repoRoot;
                }
            }
            throw new InvalidOperationException(
                "Could not locate repo root (unity-plugin/ + unity-test-project/ sentinels) walking up from " +
                TestContext.CurrentContext.TestDirectory);
        }

        private static string FixtureDir =>
            Path.Combine(RepoRoot(), FixtureRelDir.Replace('/', Path.DirectorySeparatorChar));
    }
}
