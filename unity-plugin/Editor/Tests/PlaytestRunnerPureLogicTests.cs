// TDD: PlaytestRunner pure-logic tests not yet covered by PlaytestRunnerTests.cs.
// Area 2, Task 3 (FormatProvenance, EvalCapturedDelta edge) + Task 4 (SetTimeScale).
// No Play Mode, no scene objects — purely deterministic logic.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerPureLogicTests : SceneTestBase
    {
        // ── Task 3: FormatProvenance ──────────────────────────────────────────

        [Test]
        public void FormatProvenance_NullAll_ReturnsEmptyString()
        {
            var step = new PlaytestStep(); // all provenance fields null

            var result = PlaytestRunner.FormatProvenance(step);

            Assert.AreEqual("", result);
        }

        [Test]
        public void FormatProvenance_WithSourceFile_IncludesFileAndLine()
        {
            var step = new PlaytestStep { SourceFile = "Assets/Tests/MyTest.playtest", SourceLine = 4 };

            var result = PlaytestRunner.FormatProvenance(step);

            StringAssert.Contains("source:", result);
            StringAssert.Contains("MyTest.playtest", result);
            StringAssert.Contains("5", result); // SourceLine 4 → displayed as 5 (1-based)
        }

        [Test]
        public void FormatProvenance_WithMacroStack_IncludesMacroChain()
        {
            var step = new PlaytestStep { MacroStack = new[] { "MacroA", "MacroB" } };

            var result = PlaytestRunner.FormatProvenance(step);

            StringAssert.Contains("macro:", result);
            StringAssert.Contains("MacroA", result);
            StringAssert.Contains("MacroB", result);
        }

        [Test]
        public void FormatProvenance_WithSectionContext_IncludesSection()
        {
            var step = new PlaytestStep { SectionContext = "SETUP" };

            var result = PlaytestRunner.FormatProvenance(step);

            StringAssert.Contains("section:", result);
            StringAssert.Contains("SETUP", result);
        }

        // ── Task 3: EvalCapturedDelta edge cases ──────────────────────────────

        [Test]
        public void EvalCapturedDelta_IncreasedBy_NoThresholdNoOp_ReturnsTrueWhenPositiveDelta()
        {
            float us = -1f;
            // INCREASED_BY with no threshold/subOp: delta > 0 → true
            var result = PlaytestRunner.EvalCapturedDelta("INCREASED_BY", "", "", 5f, 7f, ref us, 0f, 0f);
            Assert.IsTrue(result, "INCREASED_BY with no threshold should return true when delta > 0");
        }

        [Test]
        public void EvalCapturedDelta_UnknownMode_ThrowsArgumentException()
        {
            // ref params can't be captured in lambdas; use try/catch instead
            float us = -1f;
            bool threw = false;
            try { PlaytestRunner.EvalCapturedDelta("TELEPORTED", null, null, 0f, 0f, ref us, 0f, 0f); }
            catch (ArgumentException) { threw = true; }
            Assert.IsTrue(threw, "Unknown WAIT_CAPTURED mode must throw ArgumentException");
        }

        [Test]
        public void ShouldStopPlayModeOnPollTimeout_BothFalse_ReturnsFalse()
        {
            Assert.IsFalse(PlaytestRunner.ShouldStopPlayModeOnPollTimeout(false, false));
        }

        // ── Task 4: SetTimeScale via BuildReport side-effect ──────────────────

        [TearDown]
        public void TearDownTimeScale()
        {
            // Restore timescale in case a test failed mid-way
            Time.timeScale = 1f;
            PlaytestRunner.CompleteRunCleanupForTests();
        }

        [Test]
        public void BuildReport_AlwaysResetsTimeScaleTo1()
        {
            // Arrange: set timescale to non-default value
            Time.timeScale = 0.25f;
            var results = new List<string> { "[1] ASSERT x == 1 — PASS (1)" };

            // Act: BuildReport always calls SetTimeScale(1f) at start
            PlaytestRunner.BuildReport(results, 1, 0, Time.realtimeSinceStartup - 0.1f);

            // Assert: timescale reset to 1
            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void SetTimeScale_ViaReflection_SetsUnityTimeScale()
        {
            // Access the private SetTimeScale method via reflection
            var mi = typeof(PlaytestRunner).GetMethod(
                "SetTimeScale",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, "SetTimeScale must exist as private static method");

            try
            {
                mi.Invoke(null, new object[] { 0.5f });
                Assert.AreEqual(0.5f, Time.timeScale, 0.001f);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [Test]
        public void SetTimeScale_ViaReflection_ZeroIsAllowed()
        {
            var mi = typeof(PlaytestRunner).GetMethod(
                "SetTimeScale",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi);

            try
            {
                mi.Invoke(null, new object[] { 0f });
                Assert.AreEqual(0f, Time.timeScale, 0.001f);
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        [Test]
        public void SetTimeScale_ViaReflection_NullCachedConfig_FallsBackToTimeTimeScale()
        {
            // Ensure _cachedConfig is null (default state) so the method falls through
            // to the Time.timeScale fallback.
            var mi = typeof(PlaytestRunner).GetMethod(
                "SetTimeScale",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi);

            // _cachedConfig is null (CompleteRunCleanup sets it to null)
            PlaytestRunner.CompleteRunCleanupForTests();

            try
            {
                mi.Invoke(null, new object[] { 0.75f });
                Assert.AreEqual(0.75f, Time.timeScale, 0.001f,
                    "With null config, SetTimeScale must fall back to Time.timeScale");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }
    }
}
