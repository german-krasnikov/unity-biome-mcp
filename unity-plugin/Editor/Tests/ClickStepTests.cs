using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClickStepTests : SceneTestBase
    {
        readonly List<GameObject> _cleanup = new List<GameObject>();

        GameObject Create(string name)
        {
            var go = new GameObject(name);
            _cleanup.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _cleanup)
                if (go != null) Object.DestroyImmediate(go);
            _cleanup.Clear();
        }

        // ── Parser ───────────────────────────────────────────────────────────

        [Test]
        public void Parse_ClickCommand_ReturnsClickStep()
        {
            var steps = PlaytestParser.Parse("CLICK /Canvas/Button");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Click, steps[0].Type);
            Assert.AreEqual("/Canvas/Button", steps[0].Path);
        }

        [Test]
        public void Parse_ClickWithWait_ParsesDelay()
        {
            var steps = PlaytestParser.Parse("CLICK /Canvas/Button WAIT 0.5");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Click, steps[0].Type);
            Assert.AreEqual(0.5f, steps[0].Delay, 0.001f);
        }

        [Test]
        public void Parse_TapCommand_ParsesAsClick()
        {
            var steps = PlaytestParser.Parse("TAP /Canvas/Button");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Click, steps[0].Type);
            Assert.AreEqual("/Canvas/Button", steps[0].Path);
        }

        // ── Execution ────────────────────────────────────────────────────────

        [Test]
        public void Execute_Click_Button_InvokesOnClick()
        {
            var go = Create("ClickTarget");
            var btn = go.AddComponent<Button>();
            bool invoked = false;
            btn.onClick.AddListener(() => invoked = true);

            var step = new PlaytestStep { Type = StepType.Click, Path = "/ClickTarget" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.IsTrue(invoked, "Button.onClick should have been invoked");
            Assert.AreEqual(1, passed);
            Assert.AreEqual(0, failed);
            StringAssert.Contains("CLICK button:", results[0]);
        }

        [Test]
        public void Execute_Click_NoButton_FallbackPointerClick()
        {
            var go = Create("HandlerTarget");
            var handler = go.AddComponent<TestPointerClickHandler>();

            var step = new PlaytestStep { Type = StepType.Click, Path = "/HandlerTarget" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.IsTrue(handler.Clicked, "IPointerClickHandler should have been invoked");
            Assert.AreEqual(1, passed);
            Assert.AreEqual(0, failed);
            StringAssert.Contains("CLICK handler:", results[0]);
        }

        [Test]
        public void Execute_Click_NotFound_ReturnsError()
        {
            var step = new PlaytestStep { Type = StepType.Click, Path = "/NonExistentObject" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.AreEqual(0, passed);
            Assert.AreEqual(1, failed);
            StringAssert.Contains("ERR", results[0]);
            StringAssert.Contains("not found", results[0]);
        }

        [Test]
        public void Execute_Click_Inactive_ReturnsError()
        {
            var go = Create("InactiveBtn");
            go.AddComponent<Button>();
            go.SetActive(false);

            var step = new PlaytestStep { Type = StepType.Click, Path = "/InactiveBtn" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.AreEqual(0, passed);
            Assert.AreEqual(1, failed);
            StringAssert.Contains("ERR", results[0]);
            StringAssert.Contains("inactive", results[0]);
        }

        [Test]
        public void Execute_Click_NoHandler_CountsAsFailed()
        {
            var go = Create("PlainObject");

            var step = new PlaytestStep { Type = StepType.Click, Path = "/PlainObject" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            PlaytestRunner.ExecuteSyncStep(step, null, results, ref passed, ref failed, 0);

            Assert.AreEqual(0, passed);
            Assert.AreEqual(1, failed, "no Button/IPointerClickHandler on the target must count as a failed step");
            StringAssert.Contains("FAIL", results[0]);
        }

        // ── Test helper ──────────────────────────────────────────────────────

        class TestPointerClickHandler : MonoBehaviour, IPointerClickHandler
        {
            public bool Clicked;
            public void OnPointerClick(PointerEventData eventData) => Clicked = true;
        }
    }
}
