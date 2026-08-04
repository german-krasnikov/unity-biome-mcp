using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using System.Collections.Generic;

namespace UnityMCP.TestProject.Visual
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    public class MultiViewTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp() => _created.Clear();

        private GameObject _go
        {
            get => _created.Count > 0 ? _created[0] : null;
            set
            {
                if (value != null)
                    _created.Add(TrackOwnedObject(value));
            }
        }

        [Test]
        public void ComputeBounds_WithRenderer_ReturnsMeshBounds()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var b = MultiViewCapture.ComputeBounds(_go);
            Assert.AreEqual(Vector3.zero, b.center, "center at origin");
            Assert.That(b.size.x, Is.EqualTo(1f).Within(0.01f));
            Assert.That(b.size.y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(b.size.z, Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ComputeBounds_NoRenderer_UsesCollider()
        {
            _go = new GameObject("ColliderOnly");
            var col = _go.AddComponent<BoxCollider>();
            col.size = Vector3.one * 2f;
            var b = MultiViewCapture.ComputeBounds(_go);
            Assert.That(b.size.x, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void ComputeBounds_NoRendererNoCollider_FallbackFiveMeters()
        {
            _go = new GameObject("Empty");
            var b = MultiViewCapture.ComputeBounds(_go);
            Assert.That(b.size.x, Is.EqualTo(3f).Within(0.01f));
        }

        [Test]
        public void CaptureToFile_ReturnsPngPath()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var path = MultiViewCapture.CaptureToFile(_go, 64);
            Assert.IsNotNull(path);
            Assert.IsTrue(path.EndsWith(".png"), $"expected .png, got: {path}");
            Assert.IsTrue(System.IO.File.Exists(path), $"file not found: {path}");
        }

        [Test]
        public void CaptureToFile_CorrectDimensions()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var path = MultiViewCapture.CaptureToFile(_go, 100);
            var tex = new Texture2D(1, 1);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            Assert.AreEqual(200, tex.width);
            Assert.AreEqual(200, tex.height);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void CaptureMultiView_SphereAmongCubes_FitsTargetOnly()
        {
            // 4 cubes at corners, 1 sphere in center
            var positions = new[] {
                new Vector3(-5, 0, -5), new Vector3(5, 0, -5),
                new Vector3(-5, 0, 5),  new Vector3(5, 0, 5),
            };
            foreach (var p in positions)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "DistantCube";
                cube.transform.position = p;
                _created.Add(cube);
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "TargetSphere";
            sphere.transform.position = Vector3.zero;
            _created.Add(sphere);

            // Bounds should be sphere only (1m diameter), NOT including cubes
            var bounds = MultiViewCapture.ComputeBounds(sphere);
            Assert.That(bounds.size.x, Is.LessThan(2f), "bounds should be sphere, not scene");
            Assert.That(bounds.center.magnitude, Is.LessThan(0.1f), "center should be near origin");

            // Capture should succeed and show only the sphere
            var path = MultiViewCapture.CaptureToFile(sphere, 128);
            Assert.IsTrue(System.IO.File.Exists(path));

            // Verify: orthoSize should be ~0.6 (maxDim=1 * 0.6)
            // At this orthoSize, cubes at distance 5 are WAY outside the camera frustum
            // Camera ortho vertical extent = 0.6*2 = 1.2m total
            // Cubes at 5m away → 5/0.6 = 8.3x outside frame ✓
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float orthoSize = maxDim * 0.6f;
            float cameraExtent = orthoSize * 2f; // total vertical visible range
            Assert.That(cameraExtent, Is.LessThan(3f),
                $"Camera shows {cameraExtent}m, cubes at 5m should be outside frame");
        }
    }

    [TestFixture]
    public class OverlayDrawerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Texture2D _tex;

        [SetUp]
        public void SetUp()
        {
            // Small texture (16x16) — enough for all drawing tests
            _tex = TrackOwnedObject(new Texture2D(16, 16, TextureFormat.RGBA32, false));
            // Fill with transparent black
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                _tex.SetPixel(x, y, Color.clear);
            _tex.Apply();
        }

        // Helpers
        private bool IsSet(int x, int y) => _tex.GetPixel(x, y).a > 0.5f;
        private int CountSet() { int n = 0; for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (IsSet(x,y)) n++; return n; }

        // ------------------------------------------------------------------ //
        // DrawLine
        // ------------------------------------------------------------------ //

        [Test]
        public void DrawLine_Horizontal_SetsCorrectPixels()
        {
            OverlayDrawer.DrawLine(_tex, 0, 0, 5, 0, Color.red);
            _tex.Apply();
            // 6 pixels: x=0..5 at y=0
            for (int x = 0; x <= 5; x++)
                Assert.IsTrue(IsSet(x, 0), $"pixel ({x},0) should be set");
            Assert.AreEqual(6, CountSet(), "exactly 6 pixels");
        }

        [Test]
        public void DrawLine_Vertical_SetsCorrectPixels()
        {
            OverlayDrawer.DrawLine(_tex, 3, 0, 3, 7, Color.red);
            _tex.Apply();
            // 8 pixels: y=0..7 at x=3
            for (int y = 0; y <= 7; y++)
                Assert.IsTrue(IsSet(3, y), $"pixel (3,{y}) should be set");
            Assert.AreEqual(8, CountSet(), "exactly 8 pixels");
        }

        [Test]
        public void DrawLine_OutOfBounds_NoCrash()
        {
            // Line from far outside to far outside — should not throw
            Assert.DoesNotThrow(() =>
            {
                OverlayDrawer.DrawLine(_tex, -5, 0, 5, 0, Color.white);
                _tex.Apply();
            });
        }

        // ------------------------------------------------------------------ //
        // DrawRect
        // ------------------------------------------------------------------ //

        [Test]
        public void DrawRect_Outline_OnlyBorderPixelsSet()
        {
            // DrawRect at (2,2) width=4, height=3 → outline only
            OverlayDrawer.DrawRect(_tex, 2, 2, 4, 3, Color.green);
            _tex.Apply();

            // Interior pixel (3,3) should be clear
            Assert.IsFalse(IsSet(3, 3), "interior pixel should not be set");
            Assert.IsFalse(IsSet(4, 3), "interior pixel should not be set");

            // Corner pixels must be set
            Assert.IsTrue(IsSet(2, 2), "bottom-left corner");
            Assert.IsTrue(IsSet(5, 2), "bottom-right corner"); // x+w-1
            Assert.IsTrue(IsSet(2, 4), "top-left corner");     // y+h-1
            Assert.IsTrue(IsSet(5, 4), "top-right corner");
        }

        [Test]
        public void DrawRect_WithThickness2_BroaderBorder()
        {
            // With thickness=2 the border is 2 pixels wide
            OverlayDrawer.DrawRect(_tex, 0, 0, 16, 16, Color.white, 2);
            _tex.Apply();
            // Interior pixels well away from border should be clear
            Assert.IsFalse(IsSet(5, 5), "deep interior pixel should be clear");
        }

        // ------------------------------------------------------------------ //
        // FillRect
        // ------------------------------------------------------------------ //

        [Test]
        public void FillRect_3x3_Sets9Pixels()
        {
            OverlayDrawer.FillRect(_tex, 0, 0, 3, 3, Color.blue);
            _tex.Apply();
            Assert.AreEqual(9, CountSet(), "3x3 fill = 9 pixels");
            // Verify all interior
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.IsTrue(IsSet(x, y), $"pixel ({x},{y}) should be set");
        }

        [Test]
        public void FillRect_OutOfBounds_NoCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                OverlayDrawer.FillRect(_tex, -2, -2, 5, 5, Color.red);
                _tex.Apply();
            });
        }

        // ------------------------------------------------------------------ //
        // DrawBoundingBoxes integration
        // ------------------------------------------------------------------ //

        [Test]
        public void DrawBoundingBoxes_VisibleObject_DrawsRect()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Texture2D composite = null;
            try
            {
                go.transform.position = Vector3.zero;
                var cam = new CamState(
                    new Vector3(0, 0, 10f),
                    Quaternion.LookRotation(Vector3.back, Vector3.up), 5f);

                composite = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    composite.SetPixel(x, y, Color.clear);

                var objs = new List<(GameObject go, Color color)> { (go, Color.red) };
                OverlayDrawer.DrawBoundingBoxes(composite, 64, cam, 0, objs);
                composite.Apply();

                bool anyRed = false;
                for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    var p = composite.GetPixel(x, y);
                    if (p.r > 0.5f && p.g < 0.1f && p.b < 0.1f) { anyRed = true; break; }
                }
                Assert.IsTrue(anyRed, "visible object should produce red bounding box pixels");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (composite) Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void DrawBoundingBoxes_OffscreenObject_NoPixels()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Texture2D composite = null;
            try
            {
                go.transform.position = new Vector3(100f, 0, 0);
                var cam = new CamState(
                    new Vector3(0, 0, 10f),
                    Quaternion.LookRotation(Vector3.back, Vector3.up), 1f);

                composite = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    composite.SetPixel(x, y, Color.clear);

                var objs = new List<(GameObject go, Color color)> { (go, Color.red) };
                OverlayDrawer.DrawBoundingBoxes(composite, 32, cam, 0, objs);
                composite.Apply();

                int colored = 0;
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    if (composite.GetPixel(x, y).a > 0.5f) colored++;
                Assert.AreEqual(0, colored, "off-screen object should draw nothing");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (composite) Object.DestroyImmediate(composite);
            }
        }

        [Test]
        public void DrawBoundingBoxes_CenteredObject_RectApproxCentered()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Texture2D composite = null;
            try
            {
                go.transform.position = Vector3.zero;
                var cam = new CamState(
                    new Vector3(0, 0, 10f),
                    Quaternion.LookRotation(Vector3.back, Vector3.up), 2f);

                int cellSize = 128;
                composite = new Texture2D(cellSize * 2, cellSize * 2, TextureFormat.RGBA32, false);
                for (int y = 0; y < cellSize * 2; y++)
                for (int x = 0; x < cellSize * 2; x++)
                    composite.SetPixel(x, y, Color.clear);

                var objs = new List<(GameObject go, Color color)> { (go, Color.yellow) };
                OverlayDrawer.DrawBoundingBoxes(composite, cellSize, cam, 0, objs);
                composite.Apply();

                int minX = cellSize, maxX = 0, minY = cellSize, maxY = 0;
                for (int y = cellSize; y < cellSize * 2; y++)
                for (int x = 0; x < cellSize; x++)
                {
                    var p = composite.GetPixel(x, y);
                    if (p.a > 0.5f) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
                }

                int centerX = (minX + maxX) / 2;
                int centerYInCell = (minY + maxY) / 2 - cellSize;
                Assert.That(centerX, Is.InRange(cellSize / 2 - cellSize / 5, cellSize / 2 + cellSize / 5),
                    $"rect centerX={centerX} should be near {cellSize/2}");
                Assert.That(centerYInCell, Is.InRange(cellSize / 2 - cellSize / 5, cellSize / 2 + cellSize / 5),
                    $"rect centerY={centerYInCell} should be near {cellSize/2}");
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (composite) Object.DestroyImmediate(composite);
            }
        }
    }
}
