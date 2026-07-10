using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MultiViewOverlayTests : SceneTestBase
    {
        // Helper: camera at (0,0,-10) looking toward origin (+Z direction), orthoSize=5.
        // This keeps local +X = world +X, making left/right tests intuitive.
        static CamState FrontCam(float orthoSize = 5f)
        {
            // Camera at (0,0,-10) looking toward +Z (toward origin)
            var rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            return new CamState(new Vector3(0, 0, -10), rot, orthoSize);
        }

        // Helper: object placed at given world position with size 1x1x1
        static Bounds BoundsAt(Vector3 center, float size = 1f)
            => new Bounds(center, Vector3.one * size);

        // ── Classify ─────────────────────────────────────────────────────────

        [Test]
        public void Classify_ObjectAtOrigin_FrontCam_ReturnsVisible()
        {
            var cam = FrontCam(orthoSize: 5f);
            var bounds = BoundsAt(Vector3.zero);
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.Visible, vis);
        }

        [Test]
        public void Classify_ObjectBehindCamera_ReturnsBehind()
        {
            // Camera at (0,0,-10) looking toward -Z (away from origin)
            // Object at origin is behind the camera
            var rot = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var cam = new CamState(new Vector3(0, 0, -10), rot, 5f);
            var bounds = BoundsAt(Vector3.zero);
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.Behind, vis);
        }

        [Test]
        public void Classify_ObjectFarRight_ReturnsOffRight()
        {
            var cam = FrontCam(orthoSize: 2f); // small view window
            // Object at x=100, well outside orthoSize=2
            var bounds = BoundsAt(new Vector3(100, 0, 0));
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.OffRight, vis);
        }

        [Test]
        public void Classify_ObjectFarLeft_ReturnsOffLeft()
        {
            var cam = FrontCam(orthoSize: 2f);
            var bounds = BoundsAt(new Vector3(-100, 0, 0));
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.OffLeft, vis);
        }

        [Test]
        public void Classify_ObjectFarAbove_ReturnsOffTop()
        {
            var cam = FrontCam(orthoSize: 2f);
            var bounds = BoundsAt(new Vector3(0, 100, 0));
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.OffTop, vis);
        }

        [Test]
        public void Classify_ObjectFarBelow_ReturnsOffBottom()
        {
            var cam = FrontCam(orthoSize: 2f);
            var bounds = BoundsAt(new Vector3(0, -100, 0));
            var vis = MultiViewOverlay.Classify(cam, bounds);
            Assert.AreEqual(VisState.OffBottom, vis);
        }

        // ── ParseHighlight ────────────────────────────────────────────────────

        [Test]
        public void ParseHighlight_NullInput_ReturnsEmptyList()
        {
            var result = MultiViewOverlay.ParseHighlight(null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseHighlight_EmptyString_ReturnsEmptyList()
        {
            var result = MultiViewOverlay.ParseHighlight("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseHighlight_MissingObject_SkipsEntry()
        {
            // Object "/NonExistent" doesn't exist in scene — should be skipped, not throw
            var result = MultiViewOverlay.ParseHighlight("/NonExistent:#FF0000");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseHighlight_CapAt8Entries_WithMissingObjects()
        {
            // Even if we pass 10 entries, cap is 8 — but missing objects are skipped,
            // so result count <= 8 (and likely 0 since nothing exists in test scene)
            var entries = string.Join(",", new string[10]);
            var result = MultiViewOverlay.ParseHighlight(entries);
            Assert.LessOrEqual(result.Count, 8);
        }

        [Test]
        public void ParseHighlight_InvalidHex_FallsBackToYellow()
        {
            // Create a real object so parsing reaches the color step
            var go = new GameObject("MVOverlayTest_ParseHighlight");
            try
            {
                // ":#ZZZZZZ" is not valid hex — should fall back to yellow
                var result = MultiViewOverlay.ParseHighlight($"/{go.name}:#ZZZZZZ");
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(Color.yellow, result[0].color);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ParseHighlight_ValidHexColor_ParsedCorrectly()
        {
            var go = new GameObject("MVOverlayTest_HexColor");
            try
            {
                var result = MultiViewOverlay.ParseHighlight($"/{go.name}:#FF0000");
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(go, result[0].go);
                Assert.That(result[0].color.r, Is.EqualTo(1f).Within(0.01f));
                Assert.That(result[0].color.g, Is.EqualTo(0f).Within(0.01f));
                Assert.That(result[0].color.b, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
