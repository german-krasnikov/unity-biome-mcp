using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    public class ScreenshotCaptureTests : SceneTestBase
    {
        private const string AssetFolder = "Assets/TestsTemp/Screenshots";
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
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*DoesNotExist.*"));
            var result = ScreenshotCapture.Capture(16, 16, "DoesNotExist");
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void FindCamera_UnknownName_LogsWarning()
        {
            // G19: FindCamera must warn when named camera not found and falling back.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(".*UnknownCamG19.*"));
            var cam = ScreenshotCapture.FindCamera("UnknownCamG19");
            Assert.IsNotNull(cam, "should fall back to Camera.main");
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

        [Test]
        public void FindCamera_BracketNamedCamera_ReturnsCameraComponent()
        {
            // RED: GameObject.Find("[GAMEPLAY]") returns null (Unity bracket-selector bug)
            // After fix: ComponentSerializer.FindObject finds it correctly
            var bracketGo = new GameObject("[GAMEPLAY]");
            var cam = bracketGo.AddComponent<Camera>();
            try
            {
                var found = ScreenshotCapture.FindCamera("[GAMEPLAY]");
                Assert.That(found, Is.EqualTo(cam),
                    "FindCamera must find bracket-named cameras via ComponentSerializer.FindObject, not GameObject.Find");
            }
            finally
            {
                Object.DestroyImmediate(bracketGo);
            }
        }

        [Test]
        public void Process_OverviewScreenshot_HonorsOutputPath()
        {
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
            var output = AssetFolder + "/overview_requested.png";
            var fullPath = Path.GetFullPath(output);

            var json = "{\"cmd\":\"screenshot\",\"id\":\"shot1\",\"args\":{\"camera\":\"overview\",\"width\":\"32\",\"height\":\"32\",\"output_path\":\"" + output + "\"}}";
            var result = CommandRouter.Process(json);

            Assert.IsTrue(File.Exists(fullPath), result);
            StringAssert.Contains(fullPath, result);
        }
    }
}
