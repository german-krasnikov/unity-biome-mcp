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
    }
}
