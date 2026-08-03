// TDD — ScreenshotService: injectable CaptureFunc seam, null/throw paths.
using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ScreenshotServiceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void Setup()    => ScreenshotService.CaptureFunc = null;
        [TearDown] public void Teardown() => ScreenshotService.CaptureFunc = null;

        static byte[] MakeValidPng()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.red);
            tex.Apply();
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        [Test]
        public void Capture_WithValidPng_ReturnsPathEndingWithDotPng()
        {
            var png = MakeValidPng();
            ScreenshotService.CaptureFunc = (w, h, cam) => png;

            var result = ScreenshotService.Capture();

            Assert.IsNotNull(result);
            Assert.That(result, Does.EndWith(".png"));
        }

        [Test]
        public void Capture_WhenCaptureFuncReturnsNull_ReturnsNull()
        {
            ScreenshotService.CaptureFunc = (w, h, cam) => null;
            Assert.IsNull(ScreenshotService.Capture());
        }

        [Test]
        public void Capture_WhenCaptureFuncReturnsEmpty_ReturnsNull()
        {
            ScreenshotService.CaptureFunc = (w, h, cam) => Array.Empty<byte>();
            Assert.IsNull(ScreenshotService.Capture());
        }

        [Test]
        public void Capture_WhenCaptureFuncThrows_ReturnsNull()
        {
            ScreenshotService.CaptureFunc = (w, h, cam) => throw new Exception("boom");
            Assert.IsNull(ScreenshotService.Capture());
        }

        [Test]
        public void Capture_PassesCameraNameBasedOnPlayMode()
        {
            // In EditMode (not playing) camera should be "scene_view"
            string capturedCamera = null;
            ScreenshotService.CaptureFunc = (w, h, cam) => { capturedCamera = cam; return MakeValidPng(); };

            ScreenshotService.Capture();

            Assert.AreEqual("scene_view", capturedCamera);
        }
    }
}
