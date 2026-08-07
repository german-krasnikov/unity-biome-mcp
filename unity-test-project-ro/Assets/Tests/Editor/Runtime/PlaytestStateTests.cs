using NUnit.Framework;
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Runtime
{
    [TestFixture]
    public class PlaytestStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ─── CAPTURE ───

        [Test]
        public void Capture_StoresValue()
        {
            var state = new PlaytestState();
            state.Capture("money", "/P|C|money", "100", 100f);
            Assert.That(state.GetCapturedValue("money"), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Capture_OverwritesSameLabel()
        {
            var state = new PlaytestState();
            state.Capture("money", "/P|C|money", "100", 100f);
            state.Capture("money", "/P|C|money", "250", 250f);
            Assert.That(state.GetCapturedValue("money"), Is.EqualTo(250f).Within(0.001f));
        }

        [Test]
        public void GetCapturedValue_UnknownLabel_Throws()
        {
            var state = new PlaytestState();
            Assert.Throws<KeyNotFoundException>(() => state.GetCapturedValue("ghost"));
        }

        // ─── ASSERT_CAPTURED ───

        [Test]
        public void AssertCaptured_Increased_WhenHigher()
        {
            var state = new PlaytestState();
            state.Capture("money", "q", "100", 100f);
            // current=150 > captured=100 → INCREASED passes
            Assert.That(state.EvaluateCaptured("money", "INCREASED", null, null, 150f), Is.True);
        }

        [Test]
        public void AssertCaptured_Decreased_WhenLower()
        {
            var state = new PlaytestState();
            state.Capture("money", "q", "100", 100f);
            Assert.That(state.EvaluateCaptured("money", "DECREASED", null, null, 50f), Is.True);
        }

        [Test]
        public void AssertCaptured_Unchanged_WhenSame()
        {
            var state = new PlaytestState();
            state.Capture("money", "q", "100", 100f);
            Assert.That(state.EvaluateCaptured("money", "UNCHANGED", null, null, 100f), Is.True);
        }

        [Test]
        public void AssertCaptured_IncreasedBy_WithOp_Pass()
        {
            var state = new PlaytestState();
            state.Capture("money", "q", "100", 100f);
            // delta = 160-100 = 60, ">=", "50" → true
            Assert.That(state.EvaluateCaptured("money", "INCREASED_BY", ">=", "50", 160f), Is.True);
        }

        [Test]
        public void AssertCaptured_IncreasedBy_WithOp_Fail()
        {
            var state = new PlaytestState();
            state.Capture("money", "q", "100", 100f);
            // delta = 110-100 = 10, ">=", "50" → false
            Assert.That(state.EvaluateCaptured("money", "INCREASED_BY", ">=", "50", 110f), Is.False);
        }

        // ─── INVARIANT ───

        [Test]
        public void Invariant_NoProblem_NoViolations()
        {
            var state = new PlaytestState();
            state.RegisterInvariant("/P|C|money", ">=", "0", "/P|C|money >= 0");
            // Check with good value 100 → no violation
            state.CheckInvariants(null, 1, v => "100");
            Assert.That(state.Violations, Is.Empty);
        }

        [Test]
        public void Invariant_Violation_Recorded()
        {
            var state = new PlaytestState();
            state.RegisterInvariant("/P|C|money", ">=", "0", "/P|C|money >= 0");
            // Check with bad value -5 → violation
            state.CheckInvariants(null, 1, v => "-5");
            Assert.That(state.Violations, Has.Count.EqualTo(1));
            Assert.That(state.Violations[0], Does.Contain("/P|C|money >= 0"));
        }

        // ─── ASSERT_CONSERVED ───

        [Test]
        public void Conserved_SumConstant_NoViolation()
        {
            var state = new PlaytestState();
            // initial read: qa=6, qb=4 → initialSum=10
            state.StartConserved(new[] { "qa", "qb" }, 0f, null, v => v == "qa" ? "6" : "4");
            // check with same values → sum still 10 → no violation
            state.CheckConserved(null, v => v == "qa" ? "6" : "4");
            Assert.That(state.ConservedViolations, Is.Empty);
        }

        [Test]
        public void Conserved_SumChanged_Violation()
        {
            var state = new PlaytestState();
            // initial read: qa=6, qb=4 → initialSum=10
            state.StartConserved(new[] { "qa", "qb" }, 0f, null, v => v == "qa" ? "6" : "4");
            // check with different sum: 3+4=7 → violation
            state.CheckConserved(null, v => v == "qa" ? "3" : "4");
            Assert.That(state.ConservedViolations, Has.Count.EqualTo(1));
        }
    }
}
