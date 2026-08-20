using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    internal class RectangleModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        RectangleMode _mode;

        [SetUp]
        public void SetUp() => _mode = new RectangleMode();

        // ── Id ──────────────────────────────────────────────────────────────────

        [Test]
        public void Id_IsRectangle()
            => Assert.AreEqual(DrawingModeId.Rectangle, _mode.Id);

        // ── Begin ───────────────────────────────────────────────────────────────

        [Test]
        public void Begin_SetsIsActive()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsTrue(_mode.IsActive);
        }

        [Test]
        public void Begin_Has4PreviewVertices_AfterBegin()
        {
            _mode.Begin(new Vector2(1f, 2f), false);
            Assert.AreEqual(4, _mode.PreviewVertices.Count);
        }

        // ── PreviewVertices ─────────────────────────────────────────────────────

        [Test]
        public void PreviewVertices_After_Drag_Are4Corners()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
            Assert.AreEqual(4, _mode.PreviewVertices.Count);
        }

        [Test]
        public void PreviewVertices_ContainBothCornerExtents()
        {
            _mode.Begin(new Vector2(1f, 1f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(4f, 5f));
            var verts = _mode.PreviewVertices;
            // Must contain min and max for both axes
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in verts)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minZ) minZ = v.y;
                if (v.y > maxZ) maxZ = v.y;
            }
            Assert.AreEqual(1f, minX, 0.001f);
            Assert.AreEqual(4f, maxX, 0.001f);
            Assert.AreEqual(1f, minZ, 0.001f);
            Assert.AreEqual(5f, maxZ, 0.001f);
        }

        // ── Complete ────────────────────────────────────────────────────────────

        [Test]
        public void OnEvent_MouseUp_SetsIsComplete()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(3f, 4f));
            Assert.IsTrue(_mode.IsComplete);
        }

        // ── Build ───────────────────────────────────────────────────────────────

        [Test]
        public void Build_Returns4VertexPolygon()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(3f, 4f));
            var result = _mode.Build();
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Value.Vertices.Length);
        }

        [Test]
        public void Build_CCW_Winding()
        {
            // BL(0,0) → BR(3,0) → TR(3,4) → TL(0,4): CCW = positive area
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(3f, 4f));
            var polygon = _mode.Build()!.Value;
            Assert.Greater(polygon.SignedArea(), 0f, "Expected CCW (positive signed area)");
        }

        [Test]
        public void Build_CorrectArea()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(3f, 4f));
            var polygon = _mode.Build()!.Value;
            Assert.AreEqual(12f, polygon.Area(), 0.01f);
        }

        [Test]
        public void Build_Degenerate_SameCorners_ReturnsNull()
        {
            _mode.Begin(new Vector2(2f, 2f), false);
            _mode.OnEvent(MakeMouseUp(), new Vector2(2f, 2f));
            var result = _mode.Build();
            Assert.IsNull(result);
        }

        [Test]
        public void Build_TinyArea_ReturnsNull()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeMouseUp(), new Vector2(0.005f, 0.005f));
            var result = _mode.Build();
            Assert.IsNull(result);
        }

        // ── Grid snap ───────────────────────────────────────────────────────────

        [Test]
        public void GridSnap_RoundsCornerToHalfMeter()
        {
            // 0.7 rounds to 0.5, 2.3 rounds to 2.5
            _mode.Begin(new Vector2(0.7f, 0.7f), gridSnap: true);
            _mode.OnEvent(MakeDrag(), new Vector2(2.3f, 2.3f));
            _mode.OnEvent(MakeMouseUp(), new Vector2(2.3f, 2.3f));
            var result = _mode.Build();
            Assert.IsNotNull(result);
            // All vertices should be multiples of 0.5
            foreach (var v in result!.Value.Vertices)
            {
                Assert.AreEqual(0f, v.x % 0.5f, 0.001f, $"x={v.x} not snapped");
                Assert.AreEqual(0f, v.y % 0.5f, 0.001f, $"z={v.y} not snapped");
            }
        }

        [Test]
        public void NoGridSnap_PreservesExactCoordinates()
        {
            _mode.Begin(new Vector2(0.7f, 0.3f), gridSnap: false);
            _mode.OnEvent(MakeMouseUp(), new Vector2(3.2f, 4.6f));
            var result = _mode.Build();
            Assert.IsNotNull(result);
            // At least one vertex should have fractional coordinates
            bool hasFractional = false;
            foreach (var v in result!.Value.Vertices)
                if (v.x % 0.5f > 0.01f || v.y % 0.5f > 0.01f) hasFractional = true;
            Assert.IsTrue(hasFractional);
        }

        // ── Reset ───────────────────────────────────────────────────────────────

        [Test]
        public void Reset_ClearsState()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeDrag(), new Vector2(3f, 4f));
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
