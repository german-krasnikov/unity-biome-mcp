// TDD — ScreenshotToolbarButton: contract fields, OnClick fires seam.
using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ScreenshotToolbarButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ScreenshotToolbarButton _btn;

        [SetUp]
        public void Setup()
        {
            _btn = new ScreenshotToolbarButton();
            ScreenshotToolbarButton.OnScreenshotCaptured = null;
            ScreenshotService.CaptureFunc = null;
        }

        [TearDown]
        public void Teardown()
        {
            ScreenshotToolbarButton.OnScreenshotCaptured = null;
            ScreenshotService.CaptureFunc = null;
        }

        static byte[] MakeValidPng()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.blue);
            tex.Apply();
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        [Test]
        public void Key_IsScreenshot()
            => Assert.AreEqual("screenshot", _btn.Key);

        [Test]
        public void Order_Is10()
            => Assert.AreEqual(10, _btn.Order);

        [Test]
        public void ButtonLabel_IsSnap()
            => Assert.AreEqual("Snap", _btn.ButtonLabel);

        [Test]
        public void OnClick_WhenCaptureSucceeds_FiresSeamWithPath()
        {
            var png = MakeValidPng();
            ScreenshotService.CaptureFunc = (w, h, cam) => png;

            string received = null;
            ScreenshotToolbarButton.OnScreenshotCaptured = p => received = p;

            _btn.OnClick(null);

            Assert.IsNotNull(received);
            Assert.That(received, Does.EndWith(".png"));
        }

        [Test]
        public void OnClick_WhenCaptureFails_DoesNotFireSeam()
        {
            ScreenshotService.CaptureFunc = (w, h, cam) => null;

            bool fired = false;
            ScreenshotToolbarButton.OnScreenshotCaptured = _ => fired = true;

            _btn.OnClick(null);

            Assert.IsFalse(fired);
        }

        [Test]
        public void MenuOnly_IsTrue()
            => Assert.IsTrue(_btn.MenuOnly, "Snap button must live in hamburger menu only");
    }
}
