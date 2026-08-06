using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerTests : SceneTestBase
    {
        [TearDown]
        public void TearDown() => ConsoleCapture.Clear();

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
        [UnityMCP.Editor.Testing.SkipOnWindows("DateTime.Now low resolution on Windows causes timestamp filter to exclude injected entries")]
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
        [UnityMCP.Editor.Testing.SkipOnWindows("DateTime.Now low resolution on Windows causes timestamp filter to exclude injected entries")]
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

        // ── CheckStepConsoleErrors return value ───────────────────────────────

        [Test]
        [UnityMCP.Editor.Testing.SkipOnWindows("DateTime.Now low resolution on Windows causes timestamp filter to exclude injected entries")]
        public void CheckStepConsoleErrors_WithErrors_ReturnsTrue()
        {
            var before = DateTime.Now;
            ConsoleCapture.InjectForTest("Error!", LogType.Error);
            var step = new PlaytestStep { Type = StepType.Set };
            var results = new List<string>();

            bool hadErrors = PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.IsTrue(hadErrors);
        }

        [Test]
        public void CheckStepConsoleErrors_NoErrors_ReturnsFalse()
        {
            var before = DateTime.Now;
            var step = new PlaytestStep { Type = StepType.Set };
            var results = new List<string>();

            bool hadErrors = PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.IsFalse(hadErrors);
        }

        [Test]
        public void CheckStepConsoleErrors_AssertConsoleClean_ReturnsFalse()
        {
            var before = DateTime.Now;
            ConsoleCapture.InjectForTest("error", LogType.Error);
            var step = new PlaytestStep { Type = StepType.AssertConsoleClean };
            var results = new List<string>();

            bool hadErrors = PlaytestRunner.CheckStepConsoleErrors(step, 0, before, results);

            Assert.IsFalse(hadErrors, "AssertConsoleClean step should not count as console error");
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

        // ── Fix #10: ReadValue — GameObject property shorthands ───────────────

        [Test]
        public void ReadValue_activeSelf_ReturnsTrue_WhenActive()
        {
            var go = new GameObject("RVTest_P1");
            go.SetActive(true);
            try { Assert.AreEqual("true", PlaytestRunner.ReadValue("/RVTest_P1", "activeSelf", "")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_activeSelf_ReturnsFalse_WhenInactive()
        {
            var go = new GameObject("RVTest_P2");
            go.SetActive(false);
            try { Assert.AreEqual("false", PlaytestRunner.ReadValue("/RVTest_P2", "activeSelf", "")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_active_Alias_Works()
        {
            var go = new GameObject("RVTest_P3");
            go.SetActive(true);
            try { Assert.AreEqual("true", PlaytestRunner.ReadValue("/RVTest_P3", "active", "")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_GameObjectActiveSelf_Form2_Works()
        {
            var go = new GameObject("RVTest_P4");
            go.SetActive(false);
            try { Assert.AreEqual("false", PlaytestRunner.ReadValue("/RVTest_P4", "GameObject", "activeSelf")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_tag_ReturnsTag()
        {
            var go = new GameObject("RVTest_P5");
            try { Assert.AreEqual("Untagged", PlaytestRunner.ReadValue("/RVTest_P5", "tag", "")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_layer_ReturnsLayerInt()
        {
            var go = new GameObject("RVTest_P6");
            go.layer = 3;
            try { Assert.AreEqual("3", PlaytestRunner.ReadValue("/RVTest_P6", "layer", "")); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReadValue_activeInHierarchy_ReturnsFalse_WhenParentInactive()
        {
            var parent = new GameObject("RVTest_Parent7");
            var child = new GameObject("RVTest_Child7");
            child.transform.SetParent(parent.transform);
            parent.SetActive(false);
            try
            {
                Assert.AreEqual("false", PlaytestRunner.ReadValue("/RVTest_Parent7/RVTest_Child7", "activeInHierarchy", ""));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        // ── ResolveVirtualField (Wave 3 #11) ────────────────────────────────

        [Test]
        public void ResolveVirtualField_RigidbodySpeed_ReturnsParsableFloat()
        {
            var go = new GameObject("VF_RB");
            go.AddComponent<Rigidbody>();
            try
            {
                var result = PlaytestRunner.ReadValue("/VF_RB", "Rigidbody", "speed");
                Assert.DoesNotThrow(() => float.Parse(result, System.Globalization.CultureInfo.InvariantCulture),
                    $"speed must be a parseable float, got: {result}");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResolveVirtualField_Rigidbody2DSpeed_ReturnsParsableFloat()
        {
            var go = new GameObject("VF_RB2D");
            go.AddComponent<Rigidbody2D>();
            try
            {
                var result = PlaytestRunner.ReadValue("/VF_RB2D", "Rigidbody2D", "speed");
                Assert.DoesNotThrow(() => float.Parse(result, System.Globalization.CultureInfo.InvariantCulture),
                    $"speed must be a parseable float, got: {result}");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResolveVirtualField_AnimatorCurrentState_NoneWhenNotPlaying()
        {
            var go = new GameObject("VF_ANIM");
            go.AddComponent<Animator>(); // no controller — GetCurrentAnimatorClipInfo returns empty
            try
            {
                var result = PlaytestRunner.ReadValue("/VF_ANIM", "Animator", "currentState");
                Assert.AreEqual("none", result);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResolveVirtualField_UnknownField_FallsThroughToRealProperty()
        {
            var go = new GameObject("VF_FALLTHROUGH");
            go.AddComponent<Rigidbody>();
            try
            {
                // 'useGravity' is a real Rigidbody property — must not be swallowed by virtual resolver
                var result = PlaytestRunner.ReadValue("/VF_FALLTHROUGH", "Rigidbody", "useGravity");
                Assert.IsNotNull(result);
                StringAssert.IsMatch("(?i)true|false", result);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // TODO: WAIT_UNTIL _waitPollErrors counter needs a tick harness to test properly;
        // covered by integration tests. This smoke-test only verifies missing-path ERR reporting.
        [Test]
        public void ExecuteSyncStep_MissingPath_ReportsErr()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/NonExistent__P0Fix__|Missing|field",
                Op = "==", Value = "1",
                RawLine = "ASSERT /NonExistent__P0Fix__|Missing|field == 1"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
            Assert.AreEqual(1, failed);
            StringAssert.Contains("ERR", results[0]);
        }

        // ── #8 ASSERT_ONE_ACTIVE ─────────────────────────────────────────────────

        [Test]
        public void AssertOneActive_ExactlyOneActive_Passes()
        {
            var a = new GameObject("OAA_A"); a.SetActive(true);
            var b = new GameObject("OAA_B"); b.SetActive(false);
            var c = new GameObject("OAA_C"); c.SetActive(false);
            try
            {
                var step = new PlaytestStep
                {
                    Type = StepType.AssertOneActive,
                    Queries = new[] { "/OAA_A", "/OAA_B", "/OAA_C" },
                    RawLine = "ASSERT_ONE_ACTIVE /OAA_A /OAA_B /OAA_C"
                };
                var results = new List<string>();
                int passed = 0, failed = 0;
                PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
                Assert.AreEqual(1, passed);
                Assert.AreEqual(0, failed);
                StringAssert.Contains("PASS", results[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void AssertOneActive_TwoActive_Fails()
        {
            var a = new GameObject("OAB_A"); a.SetActive(true);
            var b = new GameObject("OAB_B"); b.SetActive(true);
            try
            {
                var step = new PlaytestStep
                {
                    Type = StepType.AssertOneActive,
                    Queries = new[] { "/OAB_A", "/OAB_B" },
                    RawLine = "ASSERT_ONE_ACTIVE /OAB_A /OAB_B"
                };
                var results = new List<string>();
                int passed = 0, failed = 0;
                PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
                Assert.AreEqual(0, passed);
                Assert.AreEqual(1, failed);
                StringAssert.Contains("FAIL", results[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void AssertOneActive_AllInactive_Fails()
        {
            var a = new GameObject("OAC_A"); a.SetActive(false);
            var b = new GameObject("OAC_B"); b.SetActive(false);
            try
            {
                var step = new PlaytestStep
                {
                    Type = StepType.AssertOneActive,
                    Queries = new[] { "/OAC_A", "/OAC_B" },
                    RawLine = "ASSERT_ONE_ACTIVE /OAC_A /OAC_B"
                };
                var results = new List<string>();
                int passed = 0, failed = 0;
                PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
                Assert.AreEqual(0, passed);
                Assert.AreEqual(1, failed);
                StringAssert.Contains("FAIL", results[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void Assert_WithExplicitTimeout_EntersWaitingPoll_NotDone()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/SomeObject|Health|hp",
                Op = ">",
                Value = "0",
                HasExplicitTimeout = true,
                Timeout = 3f,
                RawLine = "ASSERT /SomeObject|Health|hp > 0 TIMEOUT 3"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            bool done = PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
            Assert.IsFalse(done, "Polling ASSERT must not complete synchronously");
            Assert.AreEqual(0, results.Count, "No result before polling resolves");
        }

        // ── SET_ACTIVE DSL command ────────────────────────────────────────────────

        [Test]
        public void ParseSetActive_True_ProducesCorrectStep()
        {
            var result = PlaytestParser.Parse("SET_ACTIVE /Player true");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.SetActive, result[0].Type);
            Assert.AreEqual("/Player", result[0].Path);
            Assert.AreEqual("true", result[0].Value);
        }

        [Test]
        public void ParseSetActive_False_ProducesCorrectStep()
        {
            var result = PlaytestParser.Parse("SET_ACTIVE /Enemy false");
            Assert.AreEqual(StepType.SetActive, result[0].Type);
            Assert.AreEqual("false", result[0].Value);
        }

        // ── SETUP/TEARDOWN DSL blocks ─────────────────────────────────────────────

        [Test]
        public void Parse_NoSetupTeardown_SetupAndTeardownListsAreNull()
        {
            var result = PlaytestParser.Parse("ASSERT /X|C|f == 1");
            Assert.IsNull(result.SetupSteps, "SetupSteps should be null with no SETUP block");
            Assert.IsNull(result.TeardownSteps, "TeardownSteps should be null with no TEARDOWN block");
        }

        [Test]
        public void Parse_SetupBlock_StepsGoToSetupList()
        {
            var script = "SETUP\nSET_ACTIVE /TestEnv true\nLOG seeding";
            var result = PlaytestParser.Parse(script);
            Assert.IsNotNull(result.SetupSteps);
            Assert.AreEqual(2, result.SetupSteps.Count);
            Assert.AreEqual(StepType.SetActive, result.SetupSteps[0].Type);
            Assert.AreEqual(StepType.Log, result.SetupSteps[1].Type);
            Assert.AreEqual(0, result.Count, "Main Steps should be empty");
        }

        [Test]
        public void Parse_TeardownBlock_StepsGoToTeardownList()
        {
            var script = "TEARDOWN\nSET_ACTIVE /TestEnv false";
            var result = PlaytestParser.Parse(script);
            Assert.IsNotNull(result.TeardownSteps);
            Assert.AreEqual(1, result.TeardownSteps.Count);
            Assert.AreEqual(StepType.SetActive, result.TeardownSteps[0].Type);
            Assert.AreEqual(0, result.Count, "Main Steps should be empty");
        }

        [Test]
        public void Parse_SetupThenTeardown_MainStepsBeforeSetupKeyword()
        {
            var script = "ASSERT /Main|C|f == 1\nSETUP\nSET_ACTIVE /Env true\nTEARDOWN\nSET_ACTIVE /Env false";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(1, result.Count, "One main step before SETUP");
            Assert.AreEqual(StepType.Assert, result[0].Type);
            Assert.AreEqual(1, result.SetupSteps.Count);
            Assert.AreEqual(1, result.TeardownSteps.Count);
        }

        [Test]
        public void Parse_SetupAndTeardown_SectionsAreIndependent()
        {
            var script = "SETUP\nSET_ACTIVE /A true\nSET_ACTIVE /B true\nTEARDOWN\nSET_ACTIVE /A false";
            var result = PlaytestParser.Parse(script);
            Assert.AreEqual(2, result.SetupSteps.Count);
            Assert.AreEqual(1, result.TeardownSteps.Count);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Parse_SetupInDslKeywords_IsBlocked()
        {
            // SETUP and TEARDOWN must be in _DSL_KEYWORDS so they cannot be used as VAL values
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("SETUP"),
                "SETUP must be in _DSL_KEYWORDS");
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("TEARDOWN"),
                "TEARDOWN must be in _DSL_KEYWORDS");
        }

        // ── G1: fresh=true double-reload guard ────────────────────────────────

        [Test]
        public void FreshMode_LoadInProgressGuard_BlocksDoubleLoad()
        {
            RegisterCleanup(() => PlaytestRunner.SetFreshTestState(false, false, false));
            PlaytestRunner.SetFreshTestState(freshMode: true, reloadDone: false, loadInProgress: true);
            Assert.IsFalse(PlaytestRunner.ShouldStartFreshLoad,
                "Fresh load must not trigger while load is in progress");
        }

        [Test]
        public void FreshMode_ReloadDoneGuard_BlocksAfterComplete()
        {
            RegisterCleanup(() => PlaytestRunner.SetFreshTestState(false, false, false));
            PlaytestRunner.SetFreshTestState(freshMode: true, reloadDone: true, loadInProgress: false);
            Assert.IsFalse(PlaytestRunner.ShouldStartFreshLoad,
                "Fresh load must not trigger after reload is done");
        }

        [Test]
        public void FreshMode_InitialState_AllowsFirstLoad()
        {
            RegisterCleanup(() => PlaytestRunner.SetFreshTestState(false, false, false));
            PlaytestRunner.SetFreshTestState(freshMode: true, reloadDone: false, loadInProgress: false);
            Assert.IsTrue(PlaytestRunner.ShouldStartFreshLoad,
                "Fresh load should start when fresh mode is on and no load is in progress");
        }
    }
}
