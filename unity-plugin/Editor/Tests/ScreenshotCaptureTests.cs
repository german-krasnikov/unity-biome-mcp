using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScreenshotCaptureTests
    {
        private GameObject _cameraGo;

        [SetUp]
        public void SetUp()
        {
            // Ensure at least one Camera in the scene for tests that need it
            _cameraGo = new GameObject("SCTest_Camera");
            var cam = _cameraGo.AddComponent<Camera>();
            cam.tag = "MainCamera"; // makes it Camera.main
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            // NoCameraInScene test destroys all cameras — replace dirty scene
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ── FindCamera fallback to Camera.main ───────────────────────────────

        [Test]
        public void Capture_WithMainCamera_ReturnsNonEmptyBase64()
        {
            // Capture at tiny size for speed; should succeed with Camera.main present
            var result = ScreenshotCapture.Capture(16, 16, null);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
            // Base64 string has no spaces; minimal sanity check
            Assert.IsFalse(result.Contains(" "));
        }

        [Test]
        public void Capture_WithNamedCamera_FindsCameraByName()
        {
            // Use the camera name we created in SetUp
            var result = ScreenshotCapture.Capture(16, 16, "SCTest_Camera");
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Capture_WithUnknownCameraName_FallsBackToMainCamera()
        {
            // Unknown name → Camera.main fallback (no exception)
            var result = ScreenshotCapture.Capture(16, 16, "DoesNotExist");
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Capture_WithInactiveCamera_Succeeds()
        {
            // Deactivate the camera — Camera.main and Camera.allCameras exclude inactive
            _cameraGo.SetActive(false);
            var result = ScreenshotCapture.Capture(16, 16, null);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        // ── No camera in scene → ArgumentException ───────────────────────────

        [Test]
        public void Capture_NoCameraInScene_ThrowsArgumentException()
        {
            // Temporarily remove the camera to simulate an empty scene
            Object.DestroyImmediate(_cameraGo);
            _cameraGo = null;

            // Destroy all cameras including inactive ones
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(cam.gameObject);

            Assert.Throws<System.ArgumentException>(
                () => ScreenshotCapture.Capture(16, 16, null));
        }
    }
}
