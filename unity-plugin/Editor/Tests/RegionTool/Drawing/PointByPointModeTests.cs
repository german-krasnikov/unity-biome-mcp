using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    internal class PointByPointModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        PointByPointMode _mode;

        [SetUp]
        public void SetUp() => _mode = new PointByPointMode();

        // ── Id ──────────────────────────────────────────────────────────────────

        [Test]
        public void Id_IsPointByPoint()
            => Assert.AreEqual(DrawingModeId.PointByPoint, _mode.Id);

        // ── Begin ───────────────────────────────────────────────────────────────

        [Test]
        public void Begin_SetsActiveNotComplete()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsTrue(_mode.IsActive);
            Assert.IsFalse(_mode.IsComplete);
        }

        [Test]
        public void Begin_AddsFirstVertex()
        {
            _mode.Begin(new Vector2(1f, 2f), false);
            Assert.GreaterOrEqual(_mode.PreviewVertices.Count, 1);
        }

        // ── Click accumulation ──────────────────────────────────────────────────

        [Test]
        public void OnEvent_MouseDown_AddsVertex()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(3f, 0f));
            Assert.GreaterOrEqual(_mode.PreviewVertices.Count, 2);
        }

        [Test]
        public void OnEvent_ThreeClicks_StillActiveNotComplete()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            Assert.IsTrue(_mode.IsActive);
            Assert.IsFalse(_mode.IsComplete);
        }

        [Test]
        public void PreviewVertices_LastIsLiveCursorPosition()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            var cursor = new Vector2(7f, 3f);
            _mode.OnEvent(MakeMouseMove(), cursor);
            var preview = _mode.PreviewVertices;
            Assert.AreEqual(cursor, preview[preview.Count - 1]);
        }

        // ── Close: near start ───────────────────────────────────────────────────

        [Test]
        public void OnEvent_ClickNearStart_Closes()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(0.1f, 0.1f)); // within 0.4m
            Assert.IsTrue(_mode.IsComplete);
        }

        [Test]
        public void OnEvent_ClickFarFromStart_DoesNotClose()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(2f, 0f)); // 2m away
            Assert.IsFalse(_mode.IsComplete);
        }

        // ── Close: double-click ─────────────────────────────────────────────────

        [Test]
        public void OnEvent_DoubleClick_Closes()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(clickCount: 2), new Vector2(2f, 3f));
            Assert.IsTrue(_mode.IsComplete);
        }

        // ── Escape ──────────────────────────────────────────────────────────────

        [Test]
        public void OnEvent_Escape_RemovesLastVertex()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            int before = CountRealVertices();
            _mode.OnEvent(MakeEscape(), Vector2.zero);
            Assert.AreEqual(before - 1, CountRealVertices());
        }

        [Test]
        public void OnEvent_Escape_With1Vertex_SetsInactive()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeEscape(), Vector2.zero);
            Assert.IsFalse(_mode.IsActive);
        }

        // ── Finalize ────────────────────────────────────────────────────────────

        [Test]
        public void Finalize_AfterClose_ReturnsPolygon()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(0.1f, 0.1f));
            Assert.IsNotNull(_mode.Finalize());
        }

        [Test]
        public void Finalize_DropsCursorPreviewPoint()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(0.1f, 0.1f)); // close
            Assert.AreEqual(3, _mode.Finalize()!.Value.Vertices.Length);
        }

        [Test]
        public void Finalize_LessThan3Vertices_ReturnsNull()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            Assert.IsNull(_mode.Finalize());
        }

        // ── Grid snap ───────────────────────────────────────────────────────────

        [Test]
        public void GridSnap_VerticesRoundedToHalfMeter()
        {
            _mode.Begin(new Vector2(0.7f, 0.3f), gridSnap: true);
            _mode.OnEvent(MakeMouseDown(), new Vector2(4.8f, 0.2f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(4.9f, 4.7f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(0.1f, 0.1f)); // close
            var result = _mode.Finalize()!.Value;
            foreach (var v in result.Vertices)
            {
                Assert.AreEqual(0f, v.x % 0.5f, 0.001f, $"x={v.x} not snapped");
                Assert.AreEqual(0f, v.y % 0.5f, 0.001f, $"z={v.y} not snapped");
            }
        }

        // ── Reset ───────────────────────────────────────────────────────────────

        [Test]
        public void Reset_ClearsAll()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.Reset();
            Assert.IsFalse(_mode.IsActive);
            Assert.IsFalse(_mode.IsComplete);
            Assert.AreEqual(0, _mode.PreviewVertices.Count);
        }

        // ── ConfirmPending ───────────────────────────────────────────────────────

        [Test]
        public void CanConfirm_WhileDrawing_IsTrue()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsTrue(_mode.CanConfirm);
        }

        [Test]
        public void CanConfirm_AfterComplete_IsFalse()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(clickCount: 2), new Vector2(2f, 3f));
            Assert.IsFalse(_mode.CanConfirm);
        }

        [Test]
        public void ConfirmPending_AppendsVertex()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseMove(), new Vector2(3f, 3f));
            int before = CountRealVertices();
            _mode.ConfirmPending();
            Assert.AreEqual(before + 1, CountRealVertices());
        }

        [Test]
        public void ConfirmPending_WhenComplete_NoOp()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 0f));
            _mode.OnEvent(MakeMouseDown(), new Vector2(5f, 5f));
            _mode.OnEvent(MakeMouseDown(clickCount: 2), new Vector2(2f, 3f));
            int count = CountRealVertices();
            _mode.ConfirmPending();
            Assert.AreEqual(count, CountRealVertices());
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        int CountRealVertices()
        {
            int total = _mode.PreviewVertices.Count;
            return _mode.IsActive && !_mode.IsComplete ? total - 1 : total;
        }

        static Event MakeMouseDown(int clickCount = 1)
            => new Event { type = EventType.MouseDown, button = 0, clickCount = clickCount };

        static Event MakeMouseMove() => new Event { type = EventType.MouseMove };
        static Event MakeEscape() => new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape };
    }
}
