using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotateToolbarButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private AnnotateToolbarButton _btn;

        [SetUp]
        public void Setup()
        {
            _btn = new AnnotateToolbarButton();
            ScreenshotService.CaptureFunc = null;
        }

        [TearDown]
        public void Cleanup()
        {
            ScreenshotService.CaptureFunc = null;
        }

        [Test]
        public void Key_IsAnnotate()
            => Assert.AreEqual("annotate", _btn.Key);

        [Test]
        public void Order_Is11()
            => Assert.AreEqual(11, _btn.Order);

        [Test]
        public void ButtonLabel_IsAnnotate()
            => Assert.AreEqual("Annotate", _btn.ButtonLabel);

        [Test]
        public void OnClick_WhenCaptureFails_DoesNotThrow()
        {
            ScreenshotService.CaptureFunc = (w, h, cam) => null;
            Assert.DoesNotThrow(() => _btn.OnClick(null));
        }

        [Test]
        public void MenuOnly_IsTrue()
            => Assert.IsTrue(_btn.MenuOnly, "Annotate button must live in hamburger menu only");
    }
}
