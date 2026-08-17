using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Visual
{
    /// <summary>
    /// Verifies that ScreenshotCapture.OverlayCanvasWarning() returns a warning
    /// string when a ScreenSpaceOverlay canvas is present in Edit Mode.
    /// </summary>
    [TestFixture]
    public class ScreenshotOverlayWarningTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void OverlayCanvasWarning_NoCanvas_ReturnsNull()
        {
            // Fresh scene with no canvas — should return null
            var result = ScreenshotCapture.OverlayCanvasWarning();
            // Null OR no canvas present: either is acceptable in clean scene
            // The important thing is it doesn't throw.
            // In a clean test scene there's no canvas, so result should be null.
            Assert.IsNull(result, "No overlay canvas should produce null warning");
        }

        [Test]
        public void OverlayCanvasWarning_WithOverlayCanvas_ReturnsWarningString()
        {
            // Create a ScreenSpaceOverlay canvas
            var go = TrackOwnedObject(new GameObject("TestOverlayCanvas"));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var result = ScreenshotCapture.OverlayCanvasWarning();

            Assert.IsNotNull(result, "Should return a warning when ScreenSpaceOverlay canvas is present");
            StringAssert.Contains("warn:", result);
            StringAssert.Contains("ScreenSpaceOverlay", result);
        }

        [Test]
        public void OverlayCanvasWarning_WithWorldSpaceCanvas_ReturnsNull()
        {
            // WorldSpace canvas should NOT trigger the warning
            var go = TrackOwnedObject(new GameObject("TestWorldCanvas"));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // WorldSpace canvas requires an event camera; set a camera to avoid Unity warnings
            // but the test is about the warning return value

            var result = ScreenshotCapture.OverlayCanvasWarning();
            Assert.IsNull(result, "WorldSpace canvas should not trigger warning");
        }

        [Test]
        public void OverlayCanvasWarning_WithScreenSpaceCamera_ReturnsNull()
        {
            // ScreenSpaceCamera requires a worldCamera; without one Unity falls back to
            // ScreenSpaceOverlay rendering but the renderMode property still reflects our intent.
            var camGo = TrackOwnedObject(new GameObject("TestCam"));
            var camera = camGo.AddComponent<Camera>();

            var go = TrackOwnedObject(new GameObject("TestCameraCanvas"));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;

            var result = ScreenshotCapture.OverlayCanvasWarning();
            Assert.IsNull(result, "ScreenSpaceCamera canvas should not trigger warning");
        }
    }
}
