using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardAnimUtilsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void FadeIn_AddsHiddenClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.FadeIn(el);
            Assert.IsTrue(el.ClassListContains("wiz-fade-hidden"));
        }

        [Test]
        public void FadeIn_WithDelay_AddsHiddenClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.FadeIn(el, 200);
            Assert.IsTrue(el.ClassListContains("wiz-fade-hidden"));
        }

        [Test]
        public void SlideInRight_AddsHiddenClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.SlideInRight(el);
            Assert.IsTrue(el.ClassListContains("wiz-slide-hidden"));
        }

        [Test]
        public void SlideInRight_WithDelay_AddsHiddenClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.SlideInRight(el, 100);
            Assert.IsTrue(el.ClassListContains("wiz-slide-hidden"));
        }

        [Test]
        public void FlashClass_AddsClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.FlashClass(el, "test-class", 100);
            Assert.IsTrue(el.ClassListContains("test-class"));
        }

        [Test]
        public void FlashClass_DifferentClass_AddsCorrectClass()
        {
            var el = new VisualElement();
            WizardAnimUtils.FlashClass(el, "wiz-btn-copied", 300);
            Assert.IsTrue(el.ClassListContains("wiz-btn-copied"));
        }

        [Test]
        public void PulseOnce_DoesNotThrow()
        {
            var el = new VisualElement();
            Assert.DoesNotThrow(() => WizardAnimUtils.PulseOnce(el));
        }

        [Test]
        public void ShakeX_DoesNotThrow()
        {
            var el = new VisualElement();
            Assert.DoesNotThrow(() => WizardAnimUtils.ShakeX(el));
        }
    }
}
