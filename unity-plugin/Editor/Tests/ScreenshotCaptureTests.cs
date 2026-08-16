using NUnit.Framework;
using System.IO;
using ArgumentException = System.ArgumentException;
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

        [Test]
        public void Process_PlayModeGuard_AllowsScreenshotCapture()
        {
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
            var output = AssetFolder + "/play_guard_capture.png";
            var fullPath = Path.GetFullPath(output);
            var savedPlayMode = CommandRouter.IsPlayMode;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var result = CommandRouter.Process(
                    "{\"cmd\":\"screenshot\",\"id\":\"shot-play\",\"args\":" +
                    "{\"width\":\"16\",\"height\":\"16\",\"output_path\":\"" +
                    output + "\"}}");

                StringAssert.DoesNotContain("Play mode active", result, result);
                Assert.IsTrue(File.Exists(fullPath), result);
            }
            finally
            {
                CommandRouter.IsPlayMode = savedPlayMode;
            }
        }

        // ── P-317: PNG dimension validation ──────────────────────────────────

        [Test]
        public void ReadPngDimensions_ValidHeader_ReturnsCorrectWidthHeight()
        {
            // Minimal 24-byte buffer: PNG signature (8) + IHDR length (4) + "IHDR" (4) + width (4) + height (4)
            // width=640 (0x00000280), height=480 (0x000001E0)
            var png = new byte[]
            {
                137, 80, 78, 71, 13, 10, 26, 10, // PNG signature
                0, 0, 0, 13,                      // IHDR chunk length
                73, 72, 68, 82,                   // "IHDR"
                0, 0, 2, 128,                     // width = 640
                0, 0, 1, 224,                     // height = 480
            };
            var (w, h) = ScreenshotCapture.ReadPngDimensions(png);
            Assert.AreEqual(640, w);
            Assert.AreEqual(480, h);
        }

        [Test]
        public void ReadPngDimensions_TruncatedData_ThrowsArgumentException()
        {
            var shortPng = new byte[10]; // < 24 bytes required
            Assert.Throws<ArgumentException>(() => ScreenshotCapture.ReadPngDimensions(shortPng));
        }
    }
}
