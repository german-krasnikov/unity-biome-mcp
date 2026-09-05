// TDD: D11 — Player fixture parse-parity oracle. A local Player build is
// impractical (the real receipt-content CI gate lives in
// .github/workflows/unity-player-playtest.yml's "Validate ... Receipts"
// steps, run on push/PR). This EditMode test proves the shared Core parser
// produces a stable, fully-Player-supported step sequence for the 6 shipped
// Player fixtures under Assets/StreamingAssets/Playtests/ — a regression in
// PlaytestParser.Parse() (step count, step types, or the pinned
// expected-failure Assert line) surfaces here instead of only in nightly
// Player CI. Supported-type set mirrors PlayerPlaytestRunner's D11 pre-scan
// gate exactly (Assert, AssertConsoleClean, Invoke, Log, Set, Snapshot,
// TimeScale, WaitUntil, Wait) — the 9 types the fixtures actually use.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlayerCiFixtureParseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string FixtureDir = "Assets/StreamingAssets/Playtests";

        private static readonly HashSet<StepType> SupportedInPlayer = new HashSet<StepType>
        {
            StepType.Assert, StepType.AssertConsoleClean, StepType.Invoke, StepType.Log,
            StepType.Set, StepType.Snapshot, StepType.TimeScale, StepType.WaitUntil, StepType.Wait,
        };

        private static string ReadFixture(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", FixtureDir, fileName));
            return File.ReadAllText(fullPath);
        }

        private static void AssertAllStepsSupportedInPlayer(ParseResult result, string fileName)
        {
            foreach (var step in result.Steps)
            {
                Assert.IsTrue(SupportedInPlayer.Contains(step.Type),
                    $"{fileName}: step type {step.Type} is not in the Player-supported set ('{step.RawLine}')");
            }
        }

        [Test]
        public void Parse_PlayerCiSmoke_FourteenSupportedSteps()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_smoke.playtest"));
            Assert.AreEqual(14, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_smoke.playtest");
        }

        [Test]
        public void Parse_PlayerCiBounds_TwelveSupportedSteps()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_bounds.playtest"));
            Assert.AreEqual(12, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_bounds.playtest");
        }

        [Test]
        public void Parse_PlayerCiExpectedFailure_HasThePinnedFailingAssertLine()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_expected_failure.playtest"));
            Assert.AreEqual(5, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_expected_failure.playtest");

            // unity-player-playtest.yml's "Validate Player PlayTest Expected Failure
            // Receipts" step matches this exact raw line in the receipt — the Player
            // must reproduce this literal Assert step untouched.
            var assertStep = result[2];
            Assert.AreEqual(StepType.Assert, assertStep.Type);
            Assert.AreEqual("ASSERT /GridPlayer|GridPlayer|PosZ == 999", assertStep.RawLine);
            Assert.AreEqual("/GridPlayer|GridPlayer|PosZ", assertStep.Query);
            Assert.AreEqual("==", assertStep.Op);
            Assert.AreEqual("999", assertStep.Value);
        }

        [Test]
        public void Parse_PlayerCiMultiMove_FifteenSupportedSteps()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_multi_move.playtest"));
            Assert.AreEqual(15, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_multi_move.playtest");
        }

        [Test]
        public void Parse_PlayerCiReset_SixteenSupportedSteps()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_reset.playtest"));
            Assert.AreEqual(16, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_reset.playtest");
        }

        [Test]
        public void Parse_PlayerCiGraphicsSmoke_FourSupportedSteps()
        {
            var result = PlaytestParser.Parse(ReadFixture("player_ci_graphics_smoke.playtest"));
            Assert.AreEqual(4, result.Count);
            AssertAllStepsSupportedInPlayer(result, "player_ci_graphics_smoke.playtest");
        }
    }
}
