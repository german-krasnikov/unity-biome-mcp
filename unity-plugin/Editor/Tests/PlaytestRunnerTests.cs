using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerTests
    {
        [TearDown]
        public void TearDown()
        {
            ConsoleCapture.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ── ResolveCharacterPath ──────────────────────────────────────────────

        [Test]
        public void ResolveCharacterPath_NullConfig_NoPlayerInScene_ReturnsDefaultPlayer()
        {
            // No config, no scene objects → last-resort "/Player"
            var result = PlaytestRunner.ResolveCharacterPath(null);
            Assert.AreEqual("/Player", result);
        }

        [Test]
        public void ResolveCharacterPath_ConfigPath_TakesPriorityOverScene()
        {
            // Config with characterPath="/Hero" overrides scene search
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.characterPath = "/Hero";
            var go = new GameObject("Player"); // scene has Player but config wins
            try
            {
                var result = PlaytestRunner.ResolveCharacterPath(config);
                Assert.AreEqual("/Hero", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResolveCharacterPath_NoConfig_PlayerInScene_ReturnsSlashPlayer()
        {
            var go = new GameObject("Player");
            try
            {
                var result = PlaytestRunner.ResolveCharacterPath(null);
                Assert.AreEqual("/Player", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveCharacterPath_NoConfig_CharacterInScene_ReturnsSlashCharacter()
        {
            var go = new GameObject("Character");
            try
            {
                var result = PlaytestRunner.ResolveCharacterPath(null);
                Assert.AreEqual("/Character", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveCharacterPath_EmptyConfigPath_FallsBackToSceneSearch()
        {
            var config = ScriptableObject.CreateInstance<PlaytestConfig>();
            config.characterPath = ""; // empty — should fall through to scene search
            var go = new GameObject("Player");
            try
            {
                var result = PlaytestRunner.ResolveCharacterPath(config);
                Assert.AreEqual("/Player", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // ── BuildReport ───────────────────────────────────────────────────────

        [Test]
        public void BuildReport_AllPassed_ReturnsOkCompact()
        {
            // No failures, no SNAPSHOT/ABORTED lines → compact "PLAYTEST: 3/3 (Xs) OK"
            var results = new List<string>
            {
                "[1] ASSERT /X|C|f == 1 — PASS (1)",
                "[2] WAIT 1s — done",
                "[3] ASSERT /Y|C|g == 2 — PASS (2)"
            };
            var report = PlaytestRunner.BuildReport(results, 3, 0, Time.realtimeSinceStartup - 0.1f);
            StringAssert.Contains("3/3", report);
            StringAssert.Contains("OK", report);
            // Compact form: should not contain line breaks (no detail lines)
            Assert.IsFalse(report.Contains('\n'), $"Expected compact report, got:\n{report}");
        }

        [Test]
        public void BuildReport_WithFail_IncludesFailLine()
        {
            var results = new List<string>
            {
                "[1] ASSERT /X|C|f == 1 — FAIL (99)"
            };
            var report = PlaytestRunner.BuildReport(results, 0, 1, Time.realtimeSinceStartup - 0.1f);
            StringAssert.Contains("FAIL", report);
            StringAssert.Contains("0/1", report);
        }

        [Test]
        public void BuildReport_WithAborted_IncludesAbortedLine()
        {
            var results = new List<string>
            {
                "[1] ABORTED: Play Mode stopped"
            };
            var report = PlaytestRunner.BuildReport(results, 0, 0, Time.realtimeSinceStartup - 0.1f);
            StringAssert.Contains("ABORTED", report);
        }

        [Test]
        public void BuildReport_WithSnapshot_IncludesSnapshotLine()
        {
            var results = new List<string>
            {
                "[1] SNAPSHOT\nhp=100"
            };
            // Snapshot forces expanded format even with no failures
            var report = PlaytestRunner.BuildReport(results, 1, 0, Time.realtimeSinceStartup - 0.1f);
            StringAssert.Contains("SNAPSHOT", report);
        }

        // ── StepConsoleCheck ──────────────────────────────────────────────────

        [SetUp]
        public void SetUp() => ConsoleCapture.Clear();


        [Test]
        public void StepConsoleCheck_ErrorDuringStep_CapturedInResult()
        {
            var before = DateTime.Now;
            ConsoleCapture.InjectForTest("NullReferenceException: Object ref", LogType.Exception);
            var step = new PlaytestStep { Type = StepType.Set };
            var results = new List<string>();

            PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.AreEqual(1, results.Count);
            StringAssert.Contains("CONSOLE_ERR", results[0]);
            StringAssert.Contains("NullReferenceException", results[0]);
            StringAssert.Contains("Set", results[0]);
        }

        [Test]
        public void StepConsoleCheck_NoError_NoExtraOutput()
        {
            var before = DateTime.Now;
            var step = new PlaytestStep { Type = StepType.Set };
            var results = new List<string>();

            PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void StepConsoleCheck_AssertConsoleClean_Skipped()
        {
            var before = DateTime.Now;
            ConsoleCapture.InjectForTest("some error", LogType.Error);
            var step = new PlaytestStep { Type = StepType.AssertConsoleClean };
            var results = new List<string>();

            PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.AreEqual(0, results.Count, "AssertConsoleClean step should skip auto-check");
        }

        [Test]
        public void StepConsoleCheck_MultipleErrors_CapsAtMax()
        {
            var before = DateTime.Now;
            for (int i = 0; i < 10; i++)
                ConsoleCapture.InjectForTest($"Error #{i}", LogType.Error);
            var step = new PlaytestStep { Type = StepType.Invoke };
            var results = new List<string>();

            PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.AreEqual(1, results.Count);
            // Max 3 errors — verify first 3 present, 4th absent
            StringAssert.Contains("Error #0", results[0]);
            StringAssert.Contains("Error #2", results[0]);
            Assert.IsFalse(results[0].Contains("Error #3"), "Should cap at StepConsoleErrorMax=3");
        }

        [Test]
        public void BuildReport_WithConsoleErr_ForcesExpandedReport()
        {
            var results = new List<string>
            {
                "[1] SET ok",
                "[1] CONSOLE_ERR during Set: NullRef"
            };
            var report = PlaytestRunner.BuildReport(results, 1, 0, Time.realtimeSinceStartup - 0.1f);
            StringAssert.Contains("CONSOLE_ERR", report);
            Assert.IsTrue(report.Contains('\n'), "CONSOLE_ERR should force expanded report");
        }

        // ── EvalCompound short-circuit ────────────────────────────────────────

        [Test]
        public void EvalCompound_And_PrimaryFalse_ShortCircuitsWithoutCallingReadFn()
        {
            bool called = false;
            bool result = PlaytestRunner.EvalCompound(
                false, new[] { "q" }, new[] { "==" }, new[] { "1" },
                isOr: false, q => { called = true; return "1"; });
            Assert.IsFalse(result);
            Assert.IsFalse(called, "readFn should not be called when AND primary=false");
        }

        [Test]
        public void EvalCompound_Or_PrimaryTrue_ShortCircuitsWithoutCallingReadFn()
        {
            bool called = false;
            bool result = PlaytestRunner.EvalCompound(
                true, new[] { "q" }, new[] { "==" }, new[] { "0" },
                isOr: true, q => { called = true; return "0"; });
            Assert.IsTrue(result);
            Assert.IsFalse(called, "readFn should not be called when OR primary=true");
        }

        [Test]
        public void EvalCompound_And_InnerFalse_ShortCircuitsRemainingCalls()
        {
            int callCount = 0;
            // queries[0] returns "0" → Compare("0","==","1") = false → should stop
            bool result = PlaytestRunner.EvalCompound(
                true, new[] { "q0", "q1" }, new[] { "==", "==" }, new[] { "1", "1" },
                isOr: false, q => { callCount++; return "0"; }); // both would fail, but only first should run
            Assert.IsFalse(result);
            Assert.AreEqual(1, callCount, "AND should stop after first false");
        }

        [Test]
        public void EvalCompound_Or_InnerTrue_ShortCircuitsRemainingCalls()
        {
            int callCount = 0;
            // queries[0] returns "1" → Compare("1","==","1") = true → should stop
            bool result = PlaytestRunner.EvalCompound(
                false, new[] { "q0", "q1" }, new[] { "==", "==" }, new[] { "1", "1" },
                isOr: true, q => { callCount++; return "1"; }); // both would pass, but only first should run
            Assert.IsTrue(result);
            Assert.AreEqual(1, callCount, "OR should stop after first true");
        }

        // ── TraceFlow ────────────────────────────────────────────────────────

        [Test]
        public void TraceFlow_ReportsNotImplemented()
        {
            var step = new PlaytestStep { Type = StepType.TraceFlow };
            var results = new List<string>();
            int passed = 0, failed = 0;

            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.AreEqual(0, passed, "TraceFlow should not increment passed");
            Assert.AreEqual(1, failed, "TraceFlow should increment failed");
            Assert.AreEqual(1, results.Count);
            StringAssert.Contains("not yet implemented", results[0]);
        }

        // ── EvalCapturedDelta (pure math, no Unity objects) ───────────────────

        [Test]
        public void EvalCapturedDelta_Increased_ReturnsFalse_WhenEqual()
        {
            float us = -1f;
            Assert.IsFalse(PlaytestRunner.EvalCapturedDelta("INCREASED", null, null, 5f, 5f, ref us, 0f, 0f));
        }

        [Test]
        public void EvalCapturedDelta_Increased_ReturnsTrue_WhenHigher()
        {
            float us = -1f;
            Assert.IsTrue(PlaytestRunner.EvalCapturedDelta("INCREASED", null, null, 5f, 6f, ref us, 0f, 0f));
        }

        [Test]
        public void EvalCapturedDelta_Decreased_ReturnsTrue_WhenLower()
        {
            float us = -1f;
            Assert.IsTrue(PlaytestRunner.EvalCapturedDelta("DECREASED", null, null, 10f, 8f, ref us, 0f, 0f));
        }

        [Test]
        public void EvalCapturedDelta_IncreasedBy_GreaterEqual_ReturnsTrueWhenThresholdMet()
        {
            float us = -1f;
            // baseline=10, current=12 → delta=2, Compare("2.0000",">=","1") → true
            Assert.IsTrue(PlaytestRunner.EvalCapturedDelta("INCREASED_BY", ">=", "1", 10f, 12f, ref us, 0f, 0f));
        }

        [Test]
        public void EvalCapturedDelta_Unchanged_ReturnsFalse_BeforeDuration()
        {
            float us = -1f; // not yet tracking
            // value unchanged (5==5), overDuration=2, now=0 → sets unchangedSince=0, but 0-0<2
            bool result = PlaytestRunner.EvalCapturedDelta("UNCHANGED", null, null, 5f, 5f, ref us, 0f, 2f);
            Assert.IsFalse(result);
            Assert.AreEqual(0f, us, 0.001f, "unchangedSince should be set to now");
        }

        [Test]
        public void EvalCapturedDelta_Unchanged_ReturnsTrue_AfterDuration()
        {
            float us = 0f; // tracking since time 0
            // now=3, overDuration=2 → 3-0=3 >= 2 → true
            bool result = PlaytestRunner.EvalCapturedDelta("UNCHANGED", null, null, 5f, 5f, ref us, 3f, 2f);
            Assert.IsTrue(result);
        }

        [Test]
        public void EvalCapturedDelta_Unchanged_ResetsWhenValueChanges()
        {
            float us = 1f; // was tracking
            // current != baseline → reset and return false
            bool result = PlaytestRunner.EvalCapturedDelta("UNCHANGED", null, null, 5f, 6f, ref us, 5f, 2f);
            Assert.IsFalse(result);
            Assert.AreEqual(-1f, us, 0.001f, "unchangedSince should reset to -1");
        }

        [Test]
        public void EvalCapturedDelta_DecreasedBy_ReturnsTrueWhenThresholdMet()
        {
            float us = -1f;
            Assert.IsTrue(PlaytestRunner.EvalCapturedDelta("DECREASED_BY", ">=", "1", 10f, 8f, ref us, 0f, 0f));
        }

        [Test]
        public void EvalCapturedDelta_Unchanged_NoDuration_ReturnsTrueImmediately()
        {
            float us = -1f;
            Assert.IsTrue(PlaytestRunner.EvalCapturedDelta("UNCHANGED", null, null, 5f, 5f, ref us, 0f, 0f));
        }
    }
}
