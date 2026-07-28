using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ArcadeAnimTests
    {
        [Test]
        public void AnimateClass_AddsHiddenClass_Immediately()
        {
            var el = new VisualElement();
            ArcadeAnim.AnimateClass(el, "my-hidden", "my-visible");
            Assert.IsTrue(el.ClassListContains("my-hidden"));
        }

        [Test]
        public void StaggerFadeIn_AddsHiddenClass_ToAllElements()
        {
            var els = new List<VisualElement> { new VisualElement(), new VisualElement(), new VisualElement() };
            ArcadeAnim.StaggerFadeIn(els);
            foreach (var el in els)
                Assert.IsTrue(el.ClassListContains("arcade-fade-hidden"));
        }

        [Test]
        public void CountUp_SetsInitialText()
        {
            var label = new Label();
            ArcadeAnim.CountUp(label, 0, 100);
            Assert.AreEqual("0", label.text);
        }

        [Test]
        public void GlowPulse_AddsGlowClass()
        {
            var el = new VisualElement();
            ArcadeAnim.GlowPulse(el, "up");
            Assert.IsTrue(el.ClassListContains("arcade-glow"));
        }

        [Test]
        public void FlashClass_AddsClassToElement()
        {
            var el = new VisualElement();
            ArcadeAnim.FlashClass(el, "test-cls", 500);
            Assert.IsTrue(el.ClassListContains("test-cls"));
        }

        [Test]
        public void SmoothLoop_InitializesOnce_WhileDetached()
        {
            var el = new VisualElement();
            int ticks = 0;

            ArcadeAnim.SmoothLoop(el, _ => ticks++);

            Assert.AreEqual(1, ticks);
            Assert.IsNull(el.panel);
        }

        [Test]
        public void ControlledSmoothLoop_OnlyInitializesWhenActivated()
        {
            var el = new VisualElement();
            int ticks = 0;

            var motion = ArcadeAnim.ControlledSmoothLoop(el, _ => ticks++);

            Assert.AreEqual(0, ticks);
            Assert.IsFalse(motion.IsActive);

            motion.SetActive(true);
            Assert.AreEqual(1, ticks);
            Assert.IsTrue(motion.IsActive);

            motion.SetActive(false);
            Assert.AreEqual(1, ticks);
            Assert.IsFalse(motion.IsActive);
        }
    }
}
