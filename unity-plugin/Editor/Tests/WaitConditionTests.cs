// WaitConditionTests.cs — TDD for method dispatch, AND/OR DSL, abort-on-fail parse, compound eval.
// All tests: EditMode, no Play Mode required.
using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    // ── Method dispatch via () suffix ────────────────────────────────────────────

    [TestFixture]
    public class RuntimeHelperMethodSuffixTests : SceneTestBase
    {
        private class MethodTestComp : MonoBehaviour
        {
            public int health = 99;
            public MethodSubObject stats = new MethodSubObject();
            public int GetScore() => 42;
            public MethodSubObject GetStats() => stats;
        }

        private class MethodSubObject
        {
            public int score = 77;
            public string GetValue() => "stats_value";
        }

        private GameObject _go;
        private MethodTestComp _comp;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RHMethodSuffix_Test");
            _comp = _go.AddComponent<MethodTestComp>();
        }

        [Test]
        public void ReadField_MethodSuffix_InvokesZeroArgMethod()
        {
            var result = RuntimeHelper.ReadFieldInternal(_comp, "GetScore()");
            Assert.AreEqual("42", result);
        }

        [Test]
        public void ReadField_MethodSuffix_IsIdempotent()
        {
            var r1 = RuntimeHelper.ReadFieldInternal(_comp, "GetScore()");
            var r2 = RuntimeHelper.ReadFieldInternal(_comp, "GetScore()");
            Assert.AreEqual(r1, r2);
        }

        [Test]
        public void ReadField_MethodSuffix_MethodNotFound_Throws()
        {
            Assert.Throws<ArgumentException>(() => RuntimeHelper.ReadFieldInternal(_comp, "Missing()"));
        }

        [Test]
        public void ReadField_DotPath_MethodAtEnd()
        {
            var result = RuntimeHelper.ReadFieldInternal(_comp, "stats.GetValue()");
            Assert.AreEqual("stats_value", result);
        }

        [Test]
        public void ReadField_RegularField_Unaffected()
        {
            var result = RuntimeHelper.ReadFieldInternal(_comp, "health");
            Assert.AreEqual("99", result);
        }

        [Test]
        public void ReadField_DotPath_MethodInMiddle()
        {
            var result = RuntimeHelper.ReadFieldInternal(_comp, "GetStats().score");
            Assert.AreEqual("77", result);
        }
    }

    // ── AND/OR parser ────────────────────────────────────────────────────────────

    [TestFixture]
    public class PlaytestParserAndOrTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void WaitUntil_NoConditions_QueriesNull()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q == v");
            Assert.IsNull(steps[0].Queries);
        }

        [Test]
        public void WaitUntil_AND_OneExtra_Parsed()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q1 == v1 AND q2 >= v2");
            var s = steps[0];
            Assert.AreEqual(1, s.Queries.Length);
            Assert.IsFalse(s.IsOr);
            Assert.AreEqual(">=", s.BatchOps[0]);
            Assert.AreEqual("q2", s.Queries[0]);
            Assert.AreEqual("v2", s.BatchValues[0]);
        }

        [Test]
        public void WaitUntil_OR_OneExtra_Parsed()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q1 == v1 OR q2 == v2");
            var s = steps[0];
            Assert.IsTrue(s.IsOr);
            Assert.AreEqual(1, s.Queries.Length);
        }

        [Test]
        public void WaitUntil_AND_TwoExtras()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q1 == v1 AND q2 == v2 AND q3 == v3");
            Assert.AreEqual(2, steps[0].Queries.Length);
        }

        [Test]
        public void WaitUntil_MixAndOr_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("WAIT_UNTIL q1 == v1 AND q2 == v2 OR q3 == v3"));
        }

        [Test]
        public void WaitUntil_AND_WithTimeout_Parsed()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q1 == v1 AND q2 == v2 TIMEOUT 10");
            var s = steps[0];
            Assert.AreEqual(10f, s.Timeout, 0.001f);
            Assert.AreEqual(1, s.Queries.Length);
        }

        [Test]
        public void WaitUntil_OR_WithTimeoutFirst()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL q1 == v1 TIMEOUT 5 OR q2 == v2");
            var s = steps[0];
            Assert.AreEqual(5f, s.Timeout, 0.001f);
            Assert.IsTrue(s.IsOr);
            Assert.AreEqual(1, s.Queries.Length);
        }
    }

    // ── Abort-on-fail parse ──────────────────────────────────────────────────────

    [TestFixture]
    public class PlaytestParserAbortTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Parse_WaitUntilWithAbortToken_SetsAbortOnFail()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|H|v > 0 TIMEOUT 5 ABORT");
            Assert.IsTrue(steps[0].AbortOnFail);
        }

        [Test]
        public void Parse_WaitUntilWithoutAbortToken_AbortOnFailFalse()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|H|v > 0 TIMEOUT 5");
            Assert.IsFalse(steps[0].AbortOnFail);
        }

        [Test]
        public void HasGlobalAbort_DirectivePresent_ReturnsTrue()
        {
            Assert.IsTrue(PlaytestParser.Parse("ABORT_ON_FAIL\nWAIT_UNTIL /P|H|v > 0").HasGlobalAbort);
        }

        [Test]
        public void HasGlobalAbort_NoDirective_ReturnsFalse()
        {
            Assert.IsFalse(PlaytestParser.Parse("WAIT_UNTIL /P|H|v > 0").HasGlobalAbort);
        }

        [Test]
        public void Parse_AbortOnFailDirective_NotEmittedAsStep()
        {
            var steps = PlaytestParser.Parse("ABORT_ON_FAIL\nWAIT_UNTIL /P|H|v > 0 TIMEOUT 5");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitUntil, steps[0].Type);
        }

        [Test]
        public void Parse_WaitUntilTimeoutThenAbort_BothParsed()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|H|v > 0 TIMEOUT 7 ABORT");
            Assert.AreEqual(7f, steps[0].Timeout, 0.001f);
            Assert.IsTrue(steps[0].AbortOnFail);
        }
    }

    // ── EvalCompound pure unit tests ─────────────────────────────────────────────

    [TestFixture]
    public class PlaytestRunnerCompoundWaitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static Func<string, string> ConstFn(string v) => _ => v;

        [Test]
        public void AND_AllTrue_ReturnsTrue()
        {
            bool result = PlaytestRunner.EvalCompound(true, new[] { "q" }, new[] { "==" }, new[] { "5" }, false, ConstFn("5"));
            Assert.IsTrue(result);
        }

        [Test]
        public void AND_ExtraFalse_ReturnsFalse()
        {
            bool result = PlaytestRunner.EvalCompound(true, new[] { "q" }, new[] { "==" }, new[] { "5" }, false, ConstFn("9"));
            Assert.IsFalse(result);
        }

        [Test]
        public void AND_PrimaryFalse_ReturnsFalse()
        {
            bool result = PlaytestRunner.EvalCompound(false, new[] { "q" }, new[] { "==" }, new[] { "5" }, false, ConstFn("5"));
            Assert.IsFalse(result);
        }

        [Test]
        public void OR_AllFalse_ReturnsFalse()
        {
            bool result = PlaytestRunner.EvalCompound(false, new[] { "q" }, new[] { "==" }, new[] { "5" }, true, ConstFn("9"));
            Assert.IsFalse(result);
        }

        [Test]
        public void OR_PrimaryFalse_ExtraTrue_ReturnsTrue()
        {
            bool result = PlaytestRunner.EvalCompound(false, new[] { "q" }, new[] { "==" }, new[] { "5" }, true, ConstFn("5"));
            Assert.IsTrue(result);
        }

        [Test]
        public void OR_PrimaryTrue_ExtraFalse_ReturnsTrue()
        {
            bool result = PlaytestRunner.EvalCompound(true, new[] { "q" }, new[] { "==" }, new[] { "5" }, true, ConstFn("9"));
            Assert.IsTrue(result);
        }

        [Test]
        public void NoExtras_PrimaryFalse_ReturnsFalse()
        {
            bool result = PlaytestRunner.EvalCompound(false, null, null, null, false, ConstFn(""));
            Assert.IsFalse(result);
        }

        [Test]
        public void NoExtras_PrimaryTrue_ReturnsTrue()
        {
            bool result = PlaytestRunner.EvalCompound(true, null, null, null, false, ConstFn(""));
            Assert.IsTrue(result);
        }
    }

    // ── P-263: compound helper TIMEOUT must set HasExplicitTimeout ───────────────

    [TestFixture]
    public class CompoundHelperExplicitTimeoutTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void SweepPath_UntilWithTimeout_SetsHasExplicitTimeout()
        {
            var script = "SWEEP_PATH /Player DWELL 0.1\n1,0,0 > 2,0,0\nUNTIL /P|H|v == 10 TIMEOUT 2";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.AreEqual(2f, waitStep.Timeout, 0.001f);
            Assert.IsTrue(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be true when TIMEOUT present");
        }

        [Test]
        public void SweepPath_UntilWithoutTimeout_HasExplicitTimeoutFalse()
        {
            var script = "SWEEP_PATH /Player DWELL 0.1\n1,0,0 > 2,0,0\nUNTIL /P|H|v == 10";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.IsFalse(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be false when TIMEOUT absent");
        }

        [Test]
        public void InvokeRepeat_ExpectWithTimeout_SetsHasExplicitTimeout()
        {
            var script = "INVOKE_REPEAT 2 /Player Shooter Fire\nEXPECT /P|H|v == 1 TIMEOUT 3";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.AreEqual(3f, waitStep.Timeout, 0.001f);
            Assert.IsTrue(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be true when TIMEOUT present");
        }

        [Test]
        public void InvokeRepeat_ExpectWithoutTimeout_HasExplicitTimeoutFalse()
        {
            var script = "INVOKE_REPEAT 1 /Player Shooter Fire\nEXPECT /P|H|v == 1";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.IsFalse(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be false when TIMEOUT absent");
        }

        [Test]
        public void CompletePurchase_WithTimeout_SetsHasExplicitTimeout()
        {
            var script = "COMPLETE_PURCHASE /Shop\nEXPECT /Item|Status|purchased\nTIMEOUT 7";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.AreEqual(7f, waitStep.Timeout, 0.001f);
            Assert.IsTrue(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be true when TIMEOUT present");
        }

        [Test]
        public void CompletePurchase_WithoutTimeout_HasExplicitTimeoutFalse()
        {
            var script = "COMPLETE_PURCHASE /Shop\nEXPECT /Item|Status|purchased";
            var steps = PlaytestParser.Parse(script);
            var waitStep = steps.Find(s => s.Type == StepType.WaitUntil);
            Assert.IsNotNull(waitStep, "WaitUntil step must be emitted");
            Assert.IsFalse(waitStep.HasExplicitTimeout, "HasExplicitTimeout must be false when TIMEOUT absent");
        }
    }
}
