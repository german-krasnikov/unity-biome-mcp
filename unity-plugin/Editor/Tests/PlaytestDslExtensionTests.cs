// TDD Red tests for Phase 1 DSL extensions.
// These compile only after all 5 changes are applied (StepType.Section/Desc, Label field, etc.)
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestDslExtensionTests : SceneTestBase
    {
        // ── 1.1 ReadField with method args ───────────────────────────────────────

        private class ReadFieldTestBehaviour : MonoBehaviour
        {
            public string GetName() => "test_name";
            public bool HasItem(string itemName) => itemName == "sword";
        }

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("DslExt_Test");
            _go.AddComponent<ReadFieldTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ReadField_MethodNoArgs_StillWorks()
        {
            // Regression: GetName() with no args must still invoke via new IndexOf('(') path
            var comp = _go.GetComponent<ReadFieldTestBehaviour>();
            var result = RuntimeHelper.ReadFieldInternal(comp, "GetName()");
            Assert.AreEqual("test_name", result);
        }

        [Test]
        public void ReadField_MethodWithStringArg_Dispatches()
        {
            // HasItem(sword) should invoke HasItem("sword") and return "True"
            var comp = _go.GetComponent<ReadFieldTestBehaviour>();
            var result = RuntimeHelper.ReadFieldInternal(comp, "HasItem(sword)");
            Assert.AreEqual("True", result);
        }

        // ── 1.2 MOVE_PATH parser ─────────────────────────────────────────────────

        [Test]
        public void Parse_MovePath_ThreeWaypoints_EmitsThreeMoveSteps()
        {
            var steps = PlaytestParser.Parse("MOVE_PATH 1,0,0 > 5,0,0 > 10,0,3");
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].Type);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), steps[0].Position);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), steps[1].Position);
            Assert.AreEqual(new Vector3(10f, 0f, 3f), steps[2].Position);
        }

        [Test]
        public void Parse_MovePath_WithTimeout_SetsOnEachStep()
        {
            var steps = PlaytestParser.Parse("MOVE_PATH 1,0,0 > 5,0,0 TIMEOUT 8");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(8f, steps[0].Timeout, 0.001f);
            Assert.AreEqual(8f, steps[1].Timeout, 0.001f);
        }

        // ── 1.3 SECTION + DESC ───────────────────────────────────────────────────

        [Test]
        public void Parse_Section_CreatesStep()
        {
            var steps = PlaytestParser.Parse("SECTION \"Movement Phase\"");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Section, steps[0].Type);
            Assert.AreEqual("Movement Phase", steps[0].Message);
        }

        [Test]
        public void Parse_Desc_SetsLabelOnNextStep()
        {
            var steps = PlaytestParser.Parse("DESC \"check health\"\nASSERT /X|C|f == 1");
            Assert.AreEqual(1, steps.Count, "DESC must not emit a step itself");
            Assert.AreEqual(StepType.Assert, steps[0].Type);
            Assert.AreEqual("check health", steps[0].Label);
        }

        // ── 1.4 ASSERT AS ────────────────────────────────────────────────────────

        [Test]
        public void Parse_Assert_WithAs_SetsMessage()
        {
            var steps = PlaytestParser.Parse("ASSERT /X|C|f == 1 AS \"health is full\"");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("health is full", steps[0].Message);
        }

        // ── P0.5 SWEEP_PATH ──────────────────────────────────────────────────────

        [Test]
        public void Parse_SweepPath_ThreeWaypoints_EmitsSixMoveWaitPlusWaitUntil()
        {
            var steps = PlaytestParser.Parse(
                "SWEEP_PATH /Julia DWELL 0.4\n  1,0,0 > 2,0,0 > 3,0,0\nUNTIL /q|C|f >= 2 TIMEOUT 5");
            Assert.AreEqual(7, steps.Count);
            Assert.AreEqual(StepType.Move,      steps[0].Type);
            Assert.AreEqual("/Julia",            steps[0].Path);
            Assert.AreEqual(new Vector3(1,0,0), steps[0].Position);
            Assert.AreEqual(StepType.Wait,      steps[1].Type);
            Assert.AreEqual(0.4f,               steps[1].Delay, 0.001f);
            Assert.AreEqual(StepType.Move,      steps[2].Type);
            Assert.AreEqual(StepType.Wait,      steps[3].Type);
            Assert.AreEqual(0.4f,               steps[3].Delay, 0.001f);
            Assert.AreEqual(StepType.Move,      steps[4].Type);
            Assert.AreEqual(StepType.Wait,      steps[5].Type);
            Assert.AreEqual(StepType.WaitUntil, steps[6].Type);
            Assert.AreEqual("/q|C|f",           steps[6].Query);
            Assert.AreEqual(5f,                 steps[6].Timeout, 0.001f);
        }

        [Test]
        public void Parse_SweepPath_NoDwell_EmitsMovesAndWaitUntilOnly()
        {
            var steps = PlaytestParser.Parse(
                "SWEEP_PATH /Julia DWELL 0\n  1,0,0 > 2,0,0\nUNTIL /q|C|f == 1 TIMEOUT 3");
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(StepType.Move,      steps[0].Type);
            Assert.AreEqual(StepType.Move,      steps[1].Type);
            Assert.AreEqual(StepType.WaitUntil, steps[2].Type);
            Assert.AreEqual(3f,                 steps[2].Timeout, 0.001f);
        }

        [Test]
        public void Parse_SweepPath_NoUntil_EmitsMoveWaitPairsOnly()
        {
            var steps = PlaytestParser.Parse(
                "SWEEP_PATH /Julia DWELL 0.2\n  1,0,0 > 2,0,0");
            Assert.AreEqual(4, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].Type);
            Assert.AreEqual(StepType.Wait, steps[1].Type);
            Assert.AreEqual(0.2f,          steps[1].Delay, 0.001f);
            Assert.AreEqual(StepType.Move, steps[2].Type);
            Assert.AreEqual(StepType.Wait, steps[3].Type);
            Assert.IsFalse(steps.Exists(s => s.Type == StepType.WaitUntil));
        }

        [Test]
        public void Parse_SweepPath_MissingDwell_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("SWEEP_PATH /Julia 0.4"));
            StringAssert.Contains("DWELL", ex.Message);
        }

        [Test]
        public void Parse_SweepPath_DslCommandAfterWaypoints_NotSwallowed()
        {
            var steps = PlaytestParser.Parse(
                "SWEEP_PATH /Julia DWELL 0.2\n  1,0,0 > 2,0,0\nASSERT /X|C|f == 1");
            Assert.IsTrue(steps.Exists(s => s.Type == StepType.Assert), "ASSERT must not be swallowed by SWEEP_PATH");
        }

        // ── P0.4 WAIT_CAPTURED parser ─────────────────────────────────────────

        [Test]
        public void Parse_WaitCaptured_Increased_ParsesLabel()
        {
            var steps = PlaytestParser.Parse("WAIT_CAPTURED myLabel INCREASED TIMEOUT 12");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitCaptured, steps[0].Type);
            Assert.AreEqual("myLabel", steps[0].Message);
            Assert.AreEqual("INCREASED", steps[0].Op);
            Assert.AreEqual(12f, steps[0].Timeout, 0.001f);
        }

        [Test]
        public void Parse_WaitCaptured_IncreasedBy_ParsesSubOpAndValue()
        {
            var steps = PlaytestParser.Parse("WAIT_CAPTURED cashBefore INCREASED_BY >= 1 TIMEOUT 6");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitCaptured, steps[0].Type);
            Assert.AreEqual("INCREASED_BY", steps[0].Op);
            Assert.AreEqual(">=", steps[0].Args);
            Assert.AreEqual("1", steps[0].Value);
            Assert.AreEqual(6f, steps[0].Timeout, 0.001f);
        }

        [Test]
        public void Parse_WaitCaptured_Unchanged_ParsesOverDuration()
        {
            var steps = PlaytestParser.Parse("WAIT_CAPTURED eggs UNCHANGED OVER 1");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitCaptured, steps[0].Type);
            Assert.AreEqual("UNCHANGED", steps[0].Op);
            Assert.AreEqual(1f, steps[0].Delay, 0.001f);
        }

        [Test]
        public void Parse_WaitCaptured_Decreased_ParsesCorrectly()
        {
            var steps = PlaytestParser.Parse("WAIT_CAPTURED cash DECREASED TIMEOUT 4");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.WaitCaptured, steps[0].Type);
            Assert.AreEqual("cash", steps[0].Message);
            Assert.AreEqual("DECREASED", steps[0].Op);
            Assert.AreEqual(4f, steps[0].Timeout, 0.001f);
        }

        // ── P1.4 Bool-Only Assert Sugar ───────────────────────────────────────────

        [Test]
        public void Parse_Assert_BoolSugar_SingleTrue_EmitsAssertEqualsTrue()
        {
            var steps = PlaytestParser.Parse("ASSERT $worker_cart_active");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Assert, steps[0].Type);
            Assert.AreEqual("$worker_cart_active", steps[0].Query);
            Assert.AreEqual("==", steps[0].Op);
            Assert.AreEqual("True", steps[0].Value);
        }

        [Test]
        public void Parse_Assert_BoolSugar_SingleFalse_EmitsAssertEqualsFalse()
        {
            var steps = PlaytestParser.Parse("ASSERT !$tractor_active");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Assert, steps[0].Type);
            Assert.AreEqual("$tractor_active", steps[0].Query);
            Assert.AreEqual("==", steps[0].Op);
            Assert.AreEqual("False", steps[0].Value);
        }

        [Test]
        public void Parse_Assert_BoolSugar_Group_EmitsAssertBatch_AllTrue()
        {
            var steps = PlaytestParser.Parse("ASSERT ($a,$b,$c)");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.AssertBatch, steps[0].Type);
            Assert.AreEqual(3, steps[0].Queries.Length);
            Assert.AreEqual("$a", steps[0].Queries[0]);
            Assert.AreEqual("$b", steps[0].Queries[1]);
            Assert.AreEqual("$c", steps[0].Queries[2]);
            foreach (var v in steps[0].BatchValues) Assert.AreEqual("True", v);
            foreach (var op in steps[0].BatchOps) Assert.AreEqual("==", op);
        }

        [Test]
        public void Parse_Assert_BoolSugar_GroupNegated_EmitsAssertBatch_AllFalse()
        {
            var steps = PlaytestParser.Parse("ASSERT !($a,$b)");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.AssertBatch, steps[0].Type);
            Assert.AreEqual(2, steps[0].Queries.Length);
            foreach (var v in steps[0].BatchValues) Assert.AreEqual("False", v);
        }

        [Test]
        public void Parse_Assert_Standard_StillWorks()
        {
            var steps = PlaytestParser.Parse("ASSERT $count >= 4");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Assert, steps[0].Type);
            Assert.AreEqual(">=", steps[0].Op);
            Assert.AreEqual("4", steps[0].Value);
        }

        [Test]
        public void Parse_Assert_MalformedSingle_Throws()
        {
            Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("ASSERT someNonSigilToken"));
        }

        // ── P1.5 Generic Action Helpers ───────────────────────────────────────────

        [Test]
        public void Parse_CompletePurchase_EmitsInvokeAndWaitUntilCompound()
        {
            var steps = PlaytestParser.Parse(
                "COMPLETE_PURCHASE $buy_silo_pipe EXPECT\n  $a,$b,$c\nTIMEOUT 2");
            Assert.AreEqual(2, steps.Count);
            // step 0: INVOKE
            Assert.AreEqual(StepType.Invoke, steps[0].Type);
            Assert.AreEqual("$buy_silo_pipe", steps[0].Path);
            Assert.AreEqual("PlacementPurchase", steps[0].Component);
            Assert.AreEqual("CompletePurchase", steps[0].Method);
            // step 1: compound WAIT_UNTIL
            Assert.AreEqual(StepType.WaitUntil, steps[1].Type);
            Assert.AreEqual("$a", steps[1].Query);
            Assert.AreEqual("==", steps[1].Op);
            Assert.AreEqual("True", steps[1].Value);
            Assert.AreEqual(2f, steps[1].Timeout, 0.001f);
            Assert.IsNotNull(steps[1].Queries);
            Assert.AreEqual(2, steps[1].Queries.Length);
            Assert.IsFalse(steps[1].IsOr);
        }

        [Test]
        public void Parse_CompletePurchase_SingleExpect_EmitsInvokeAndSimpleWaitUntil()
        {
            var steps = PlaytestParser.Parse(
                "COMPLETE_PURCHASE $buy_coop EXPECT\n  $buy_coop_completed\nTIMEOUT 2");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.Invoke, steps[0].Type);
            Assert.AreEqual(StepType.WaitUntil, steps[1].Type);
            Assert.AreEqual("$buy_coop_completed", steps[1].Query);
            Assert.IsNull(steps[1].Queries, "No compound for single EXPECT");
        }

        [Test]
        public void Parse_InvokeRepeat_SixTimes_EmitsSixInvokes()
        {
            var steps = PlaytestParser.Parse(
                "INVOKE_REPEAT 6 $coop ClearingZoneCounter OnBoardDestroyed");
            Assert.AreEqual(6, steps.Count);
            foreach (var s in steps)
            {
                Assert.AreEqual(StepType.Invoke, s.Type);
                Assert.AreEqual("$coop", s.Path);
                Assert.AreEqual("ClearingZoneCounter", s.Component);
                Assert.AreEqual("OnBoardDestroyed", s.Method);
            }
        }

        [Test]
        public void Parse_InvokeRepeat_WithExpect_EmitsInvokesAndWaitUntil()
        {
            var steps = PlaytestParser.Parse(
                "INVOKE_REPEAT 3 $coop ClearingZoneCounter OnBoardDestroyed\nEXPECT $coop_remaining == 0 TIMEOUT 2");
            Assert.AreEqual(4, steps.Count);
            Assert.AreEqual(StepType.Invoke, steps[0].Type);
            Assert.AreEqual(StepType.Invoke, steps[1].Type);
            Assert.AreEqual(StepType.Invoke, steps[2].Type);
            Assert.AreEqual(StepType.WaitUntil, steps[3].Type);
            Assert.AreEqual("$coop_remaining", steps[3].Query);
            Assert.AreEqual("==", steps[3].Op);
            Assert.AreEqual("0", steps[3].Value);
            Assert.AreEqual(2f, steps[3].Timeout, 0.001f);
        }

        [Test]
        public void Parse_InvokeRepeat_ZeroCount_EmitsNoInvokes()
        {
            var steps = PlaytestParser.Parse(
                "INVOKE_REPEAT 0 $coop ClearingZoneCounter OnBoardDestroyed");
            Assert.AreEqual(0, steps.Count);
        }
    }
}
