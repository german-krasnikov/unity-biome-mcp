// TDD: PlaytestState — BuildReport and StableWindowPollStable — EditMode safe.
// FrameSet tests (Task 3) already covered in PlaytestFrameCaptureTests.cs.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── BuildReport ─────────────────────────────────────────────────────────

        [Test]
        public void BuildReport_NoViolationsAndNoConservedViolations_ReturnsNull()
        {
            var state = new PlaytestState();
            Assert.IsNull(state.BuildReport());
        }

        [Test]
        public void BuildReport_WithViolation_ReturnsNonNullContainingViolationText()
        {
            var state = new PlaytestState();
            state.Violations.Add("[frame 1] INVARIANT VIOLATED: x == 1 (actual=2)");
            var report = state.BuildReport();
            Assert.IsNotNull(report);
            StringAssert.Contains("INVARIANT VIOLATED", report);
        }

        [Test]
        public void BuildReport_WithConservedViolation_ReturnsNonNullContainingConservedText()
        {
            var state = new PlaytestState();
            state.ConservedViolations.Add("ASSERT_CONSERVED VIOLATED: SUM changed 10 → 15");
            var report = state.BuildReport();
            Assert.IsNotNull(report);
            StringAssert.Contains("ASSERT_CONSERVED VIOLATED", report);
        }

        [Test]
        public void BuildReport_BothViolationTypes_ContainsBoth()
        {
            var state = new PlaytestState();
            state.Violations.Add("inv-violation-text");
            state.ConservedViolations.Add("conserved-violation-text");
            var report = state.BuildReport();
            Assert.IsNotNull(report);
            StringAssert.Contains("inv-violation-text", report);
            StringAssert.Contains("conserved-violation-text", report);
        }

        // ── StableWindowPollStable ──────────────────────────────────────────────

        [Test]
        public void PollStable_TwoSamplesWithinDelta_ReturnsTrue()
        {
            var state = new PlaytestState();
            state.StartStableWindow("/path");
            // First call: 1 sample only — range=float.MaxValue → not stable yet
            state.PollStable(0f, 0.5f, 2f, _ => "5.0");
            // Second call: 2 samples both 5.0 → range=0 ≤ delta=0.5 → stable
            var stable = state.PollStable(0.1f, 0.5f, 2f, _ => "5.0");
            Assert.IsTrue(stable);
        }

        [Test]
        public void PollStable_TwoSamplesOutsideDelta_ReturnsFalse()
        {
            var state = new PlaytestState();
            state.StartStableWindow("/path");
            state.PollStable(0f, 0.1f, 2f, _ => "1.0");
            // range = 2.0 - 1.0 = 1.0 > delta = 0.1 → unstable
            var stable = state.PollStable(0.1f, 0.1f, 2f, _ => "2.0");
            Assert.IsFalse(stable);
        }

        [Test]
        public void PollStable_NonParseableReadFnResult_DoesNotThrow()
        {
            var state = new PlaytestState();
            state.StartStableWindow("/path");
            Assert.DoesNotThrow(() =>
                state.PollStable(0f, 1f, 2f, _ => "not-a-number"));
        }

        [Test]
        public void PollStable_ForwardsQueryFromStartStableWindow_ToReadFn()
        {
            var state = new PlaytestState();
            state.StartStableWindow("/my/query");
            string calledWith = null;
            state.PollStable(0f, 1f, 2f, q => { calledWith = q; return "1.0"; });
            Assert.AreEqual("/my/query", calledWith);
        }
    }
}
