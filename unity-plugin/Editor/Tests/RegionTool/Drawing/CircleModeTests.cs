using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    internal class CircleModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        CircleMode _mode;

        [SetUp]
        public void SetUp() => _mode = new CircleMode();

        // ── Id ──────────────────────────────────────────────────────────────────

        [Test]
        public void Id_IsCircle()
            => Assert.AreEqual(DrawingModeId.Circle, _mode.Id);

        // ── Begin ───────────────────────────────────────────────────────────────

        [Test]
        public void Begin_SetsIsActive()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsTrue(_mode.IsActive);
        }

        // ── Default segments ────────────────────────────────────────────────────

        [Test]
        public void DefaultSegments_UsesPolygonDetailConfig()
        {
            // Default level is Normal → 16 segments
            var mode = new CircleMode(segments: 16);
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(5f, 0f));
            // 16 preview vertices
            Assert.AreEqual(16, mode.PreviewVertices.Count);
        }

        [Test]
        public void CustomSegments_ReflectedInPreview()
        {
            var mode = new CircleMode(segments: 24);
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            Assert.AreEqual(24, mode.PreviewVertices.Count);
        }

        [Test]
        public void Segments_ClampedTo12Min()
        {
            var mode = new CircleMode(segments: 4);  // below min
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            Assert.GreaterOrEqual(mode.PreviewVertices.Count, 12);
        }

        [Test]
        public void Segments_ClampedTo64Max()
        {
            var mode = new CircleMode(segments: 128);  // above max
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            Assert.LessOrEqual(mode.PreviewVertices.Count, 64);
        }

        // ── PreviewVertices geometry ────────────────────────────────────────────

        [Test]
        public void PreviewVertices_AllOnCircleRadius()
        {
            var mode = new CircleMode(segments: 16);
            var center = new Vector2(1f, 2f);
            mode.Begin(center, false);
            mode.OnEvent(MakeDrag(), new Vector2(1f + 3f, 2f));  // radius = 3
            foreach (var v in mode.PreviewVertices)
            {
                float dist = Vector2.Distance(v, center);
                Assert.AreEqual(3f, dist, 0.001f, $"Vertex {v} not on circle");
            }
        }

        // ── MouseUp = IsComplete ────────────────────────────────────────────────

        [Test]
        public void OnEvent_MouseUp_SetsIsComplete()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(3f, 0f));
            Assert.IsTrue(_mode.IsComplete);
        }

        // ── Finalize ────────────────────────────────────────────────────────────

        [Test]
        public void Finalize_ZeroRadius_ReturnsNull()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseUp(), Vector2.zero);
            var result = _mode.Finalize();
            Assert.IsNull(result);
        }

        [Test]
        public void Finalize_ValidRadius_ReturnsPolygon()
        {
            var mode = new CircleMode(segments: 16);
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(5f, 0f));
            mode.OnEvent(MakeMouseUp(), new Vector2(5f, 0f));
            var result = mode.Finalize();
            Assert.IsNotNull(result);
            Assert.AreEqual(16, result!.Value.Vertices.Length);
        }

        [Test]
        public void Finalize_Area_ApproximatesPiRSquared()
        {
            var mode = new CircleMode(segments: 24);
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(5f, 0f));
            mode.OnEvent(MakeMouseUp(), new Vector2(5f, 0f));
            var polygon = mode.Finalize()!.Value;
            float expected = Mathf.PI * 25f; // π*r²
            Assert.AreEqual(expected, polygon.Area(), expected * 0.05f); // within 5%
        }

        [Test]
        public void Finalize_RoundTrip_CsvPreservesVertexCount()
        {
            var mode = new CircleMode(segments: 16);
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            mode.OnEvent(MakeMouseUp(), new Vector2(3f, 0f));
            var polygon = mode.Finalize()!.Value;
            var csv = polygon.ToCsv();
            var restored = Polygon2D.FromCsv(csv);
            Assert.AreEqual(polygon.Vertices.Length, restored.Vertices.Length);
        }

        // ── Grid snap ───────────────────────────────────────────────────────────

        [Test]
        public void GridSnap_RadiusRoundedToHalfMeter()
        {
            var mode = new CircleMode(segments: 8);
            var center = Vector2.zero;
            mode.Begin(center, gridSnap: true);
            // Drag to radius 2.3m → should snap to 2.5m
            mode.OnEvent(MakeDrag(), new Vector2(2.3f, 0f));
            foreach (var v in mode.PreviewVertices)
            {
                float dist = Vector2.Distance(v, center);
                // Snapped radius should be multiple of 0.5
                Assert.AreEqual(0f, dist % 0.5f, 0.01f, $"Radius {dist} not snapped");
            }
        }

        // ── Reset ───────────────────────────────────────────────────────────────

        [Test]
        public void Reset_ClearsState()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 0f));
            _mode.Reset();
            Assert.IsFalse(_mode.IsActive);
            Assert.IsFalse(_mode.IsComplete);
        }

        // ── ConfirmPending ───────────────────────────────────────────────────────

        [Test]
        public void CanConfirm_AlwaysFalse()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsFalse(_mode.CanConfirm);
        }

        [Test]
        public void ConfirmPending_IsNoOp()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.DoesNotThrow(() => _mode.ConfirmPending());
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        static Event MakeDrag() => new Event { type = EventType.MouseDrag };
        static Event MakeMouseUp() => new Event { type = EventType.MouseUp, button = 0 };
    }
}
