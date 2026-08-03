using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardStepAnimTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void TransitionOut_AddsOutClass()
        {
            var el = new VisualElement();
            WizardStepAnim.TransitionOut(el);
            Assert.IsTrue(el.ClassListContains("wiz-transition-out"));
        }

        [Test]
        public void TransitionIn_AddsStartClass()
        {
            var el = new VisualElement();
            WizardStepAnim.TransitionIn(el);
            Assert.IsTrue(el.ClassListContains("wiz-transition-in-start"));
        }

        [Test]
        public void BuildProgressBar_HasProgressBarClass()
        {
            var bar = WizardStepAnim.BuildProgressBar();
            Assert.IsTrue(bar.ClassListContains("wiz-progress-bar"));
        }

        [Test]
        public void BuildProgressBar_HasFillChild()
        {
            var bar = WizardStepAnim.BuildProgressBar();
            Assert.AreEqual(1, bar.childCount);
            Assert.IsTrue(bar.ElementAt(0).ClassListContains("wiz-progress-fill"));
        }

        [Test]
        public void SetProgress_UpdatesFillWidth()
        {
            var bar = WizardStepAnim.BuildProgressBar();
            WizardStepAnim.SetProgress(bar, 0.5f);
            var fill = bar.Q(className: "wiz-progress-fill");
            Assert.AreEqual(50f, fill.style.width.value.value, 0.001f);
        }
    }
}
