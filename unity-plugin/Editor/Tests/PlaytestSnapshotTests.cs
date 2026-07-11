// TDD: P1.3 Data Snapshot on Failure — pure-logic tests, EditMode safe.
// Covers: BuildFailureSnapshot format, ExecuteSyncStep snapshot injection.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestSnapshotTests
    {
        [SetUp]
        public void SetUp() => ConsoleCapture.Clear();

        // ── BuildFailureSnapshot: no sigils → bare "snapshot:" header ────────────

        [Test]
        public void BuildFailureSnapshot_NoSigils_ReturnsBareSnapshotHeader()
        {
            var step = new PlaytestStep { RawLine = "LOG no sigils here" };
            var result = PlaytestRunner.BuildFailureSnapshot(step, null);
            Assert.AreEqual("snapshot:", result);
        }

        // ── BuildFailureSnapshot: null RawLine → bare "snapshot:" header ─────────

        [Test]
        public void BuildFailureSnapshot_NullRawLine_ReturnsBareSnapshotHeader()
        {
            var step = new PlaytestStep { RawLine = null };
            var result = PlaytestRunner.BuildFailureSnapshot(step, null);
            Assert.AreEqual("snapshot:", result);
        }

        // ── BuildFailureSnapshot: unresolvable sigil skipped silently ────────────

        [Test]
        public void BuildFailureSnapshot_UnresolvableSigil_SkippedSilently()
        {
            // $nonexistent_abc cannot resolve (config=null → ResolveQuery throws or returns bad path)
            var step = new PlaytestStep { RawLine = "ASSERT $nonexistent_abc == True" };
            var result = PlaytestRunner.BuildFailureSnapshot(step, null);
            // Should not throw; sigil entry absent from output
            StringAssert.StartsWith("snapshot:", result);
            StringAssert.DoesNotContain("$nonexistent_abc", result);
        }

        // ── ExecuteSyncStep Assert ERR + snapshotOnFailure=true → "snapshot:" ───

        [Test]
        public void ExecuteSyncStep_AssertErr_SnapshotEnabled_ContainsSnapshotBlock()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/SnapshotTestNonExistent__|SomeComp|field",
                Op = "==",
                Value = "True",
                RawLine = "ASSERT /SnapshotTestNonExistent__|SomeComp|field == True"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0,
                snapshotOnFailure: true);
            Assert.AreEqual(1, results.Count);
            StringAssert.Contains("snapshot:", results[0]);
        }

        // ── ExecuteSyncStep Assert ERR + snapshotOnFailure=false → no snapshot ──

        [Test]
        public void ExecuteSyncStep_AssertErr_SnapshotDisabled_NoSnapshotBlock()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Assert,
                Query = "/SnapshotTestNonExistent__|SomeComp|field",
                Op = "==",
                Value = "True",
                RawLine = "ASSERT /SnapshotTestNonExistent__|SomeComp|field == True"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);
            Assert.AreEqual(1, results.Count);
            StringAssert.DoesNotContain("snapshot:", results[0]);
        }

        // ── ExecuteSyncStep PASS → no snapshot even when enabled ─────────────────

        [Test]
        public void ExecuteSyncStep_LogStep_SnapshotEnabled_NoSnapshotBlock()
        {
            var step = new PlaytestStep
            {
                Type = StepType.Log,
                Message = "hello",
                RawLine = "LOG hello"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0,
                snapshotOnFailure: true);
            Assert.AreEqual(1, results.Count);
            StringAssert.DoesNotContain("snapshot:", results[0]);
        }

        // ── ASSERT_NEAR FAIL + snapshot ─────────────────────────────────────────

        [Test]
        public void ExecuteSyncStep_AssertNearFail_SnapshotEnabled_ContainsSnapshot()
        {
            var goA = new GameObject("SnapNearA");
            var goB = new GameObject("SnapNearB");
            goA.transform.position = Vector3.zero;
            goB.transform.position = new Vector3(100, 0, 0);
            try
            {
                var step = new PlaytestStep
                {
                    Type = StepType.AssertNear,
                    Path = "SnapNearA",
                    Value = "SnapNearB",
                    Delay = 1f,
                    RawLine = "ASSERT_NEAR SnapNearA SnapNearB 1"
                };
                var results = new List<string>();
                int passed = 0, failed = 0;
                PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0,
                    snapshotOnFailure: true);
                Assert.AreEqual(1, failed);
                StringAssert.Contains("snapshot:", results[0]);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        // ── ASSERT_BATCH partial fail + snapshot ────────────────────────────────

        [Test]
        public void ExecuteSyncStep_AssertBatchFail_SnapshotEnabled_ContainsSnapshot()
        {
            var step = new PlaytestStep
            {
                Type = StepType.AssertBatch,
                Queries = new[] { "/SnapBatchNonExistent__|C|f" },
                BatchOps = new[] { "==" },
                BatchValues = new[] { "True" },
                RawLine = "ASSERT_BATCH $nonexistent"
            };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0,
                snapshotOnFailure: true);
            Assert.AreEqual(1, failed);
            StringAssert.Contains("snapshot:", results[0]);
        }

        // ── ASSERT_CAPTURED FAIL + snapshot ─────────────────────────────────────

        [Test]
        public void ExecuteSyncStep_AssertCapturedFail_SnapshotEnabled_ContainsSnapshot()
        {
            var go = new GameObject("SnapCapTest");
            var tb = go.AddComponent<SnapCaptureHelper>();
            tb.Val = 10f;
            try
            {
                var state = new PlaytestState();
                var captureStep = new PlaytestStep
                {
                    Type = StepType.Capture,
                    Message = "snapval",
                    Query = "/SnapCapTest|SnapCaptureHelper|Val",
                    RawLine = "CAPTURE snapval /SnapCapTest|SnapCaptureHelper|Val"
                };
                var results = new List<string>();
                int passed = 0, failed = 0;
                PlaytestRunner.ExecuteSyncStep(captureStep, null, results, ref passed, ref failed, 0, state);

                var assertStep = new PlaytestStep
                {
                    Type = StepType.AssertCaptured,
                    Message = "snapval",
                    Op = "INCREASED",
                    Query = "/SnapCapTest|SnapCaptureHelper|Val",
                    RawLine = "ASSERT_CAPTURED $snapval INCREASED"
                };
                results.Clear();
                passed = 0; failed = 0;
                PlaytestRunner.ExecuteSyncStep(assertStep, null, results, ref passed, ref failed, 0, state,
                    snapshotOnFailure: true);
                Assert.AreEqual(1, failed);
                StringAssert.Contains("snapshot:", results[0]);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    public class SnapCaptureHelper : MonoBehaviour
    {
        public float Val;
    }
}
