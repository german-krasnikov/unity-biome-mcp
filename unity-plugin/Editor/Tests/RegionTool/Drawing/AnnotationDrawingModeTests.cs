using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    internal class AnnotationDrawingModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── PointMode ────────────────────────────────────────────────────────

        [Test]
        public void PointMode_Begin_NotComplete()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsFalse(mode.IsComplete);
        }

        [Test]
        public void PointMode_Click_IsComplete()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 4f));
            Assert.IsTrue(mode.IsComplete);
        }

        [Test]
        public void PointMode_Finalize_SingleVertex()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 4f));
            var pts = mode.FinalizedPoints;
            Assert.AreEqual(1, pts.Length);
            Assert.AreEqual(new Vector2(3f, 4f), pts[0]);
        }

        [Test]
        public void PointMode_PreviewVertices_ShowsCursor()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            var cursor = new Vector2(5f, 6f);
            mode.OnEvent(MouseMove(), cursor);
            var preview = mode.PreviewVertices;
            Assert.AreEqual(1, preview.Count);
            Assert.AreEqual(cursor, preview[0]);
        }

        [Test]
        public void PointMode_BeforeClick_IsActive()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsTrue(mode.IsActive);
        }

        [Test]
        public void PointMode_Id_IsPoint()
        {
            var mode = new PointMode();
            Assert.AreEqual(AnnotationModeId.Point, mode.Id);
        }

        [Test]
        public void PointMode_Reset_ClearsState()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(1f, 2f));
            mode.Reset();
            Assert.IsFalse(mode.IsActive);
            Assert.IsFalse(mode.IsComplete);
        }

        // ── PolylineMode ─────────────────────────────────────────────────────

        [Test]
        public void PolylineMode_Begin_NotComplete()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsFalse(mode.IsComplete);
        }

        [Test]
        public void PolylineMode_OneClick_NotComplete()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            Assert.IsFalse(mode.IsComplete);
        }

        [Test]
        public void PolylineMode_TwoClicks_Enter_Complete()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(5f, 5f));
            mode.OnEvent(Enter(), Vector2.zero);
            Assert.IsTrue(mode.IsComplete);
        }

        [Test]
        public void PolylineMode_Finalize_OpenLine()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(5f, 5f));
            mode.OnEvent(Enter(), Vector2.zero);
            var pts = mode.FinalizedPoints;
            Assert.GreaterOrEqual(pts.Length, 2);
            // Not closed: first != last
            Assert.AreNotEqual(pts[0], pts[pts.Length - 1]);
        }

        [Test]
        public void PolylineMode_Backspace_RemovesLast()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(5f, 5f));
            int before = RealVertexCount(mode);
            mode.OnEvent(Backspace(), Vector2.zero);
            Assert.AreEqual(before - 1, RealVertexCount(mode));
        }

        [Test]
        public void PolylineMode_Backspace_OnEmpty_NoError()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            Assert.DoesNotThrow(() => mode.OnEvent(Backspace(), Vector2.zero));
        }

        [Test]
        public void PolylineMode_DoubleClick_Complete()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));           // adds v1
            mode.OnEvent(MouseDown(clickCount: 2), new Vector2(5f, 5f)); // adds v2 + commits
            Assert.IsTrue(mode.IsComplete);
            Assert.AreEqual(2, mode.FinalizedPoints.Length);
        }

        [Test]
        public void PolylineMode_PreviewVertices_IncludesCursor()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            var cursor = new Vector2(7f, 3f);
            mode.OnEvent(MouseMove(), cursor);
            var preview = mode.PreviewVertices;
            Assert.AreEqual(cursor, preview[preview.Count - 1]);
        }

        [Test]
        public void PolylineMode_Id_IsPolyline()
        {
            var mode = new PolylineMode();
            Assert.AreEqual(AnnotationModeId.Polyline, mode.Id);
        }

        // ── MeasurementMode ──────────────────────────────────────────────────

        [Test]
        public void MeasurementMode_Begin_NotComplete()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsFalse(mode.IsComplete);
        }

        [Test]
        public void MeasurementMode_FirstClick_NotComplete()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 0f));
            Assert.IsFalse(mode.IsComplete);
        }

        [Test]
        public void MeasurementMode_SecondClick_Complete()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(6f, 0f));
            Assert.IsTrue(mode.IsComplete);
        }

        [Test]
        public void MeasurementMode_Finalize_TwoVertices()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(6f, 0f));
            var pts = mode.FinalizedPoints;
            Assert.AreEqual(2, pts.Length);
            Assert.AreEqual(new Vector2(3f, 0f), pts[0]);
            Assert.AreEqual(new Vector2(6f, 0f), pts[1]);
        }

        [Test]
        public void MeasurementMode_PreviewVertices_ShowsLine()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(3f, 0f));
            var cursor = new Vector2(6f, 0f);
            mode.OnEvent(MouseMove(), cursor);
            var preview = mode.PreviewVertices;
            // [A, cursor]
            Assert.AreEqual(2, preview.Count);
            Assert.AreEqual(new Vector2(3f, 0f), preview[0]);
            Assert.AreEqual(cursor, preview[1]);
        }

        [Test]
        public void MeasurementMode_Id_IsMeasurement()
        {
            var mode = new MeasurementMode();
            Assert.AreEqual(AnnotationModeId.Measurement, mode.Id);
        }

        // ── ConfirmPending ──────────────────────────────────────────────────────

        [Test]
        public void PolylineMode_CanConfirm_WhileDrawingWithVertex_IsTrue()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            Assert.IsTrue(mode.CanConfirm);
        }

        [Test]
        public void PolylineMode_CanConfirm_AfterComplete_IsFalse()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            mode.OnEvent(MouseDown(), new Vector2(5f, 5f));
            mode.OnEvent(Enter(), Vector2.zero);
            Assert.IsFalse(mode.CanConfirm);
        }

        [Test]
        public void PolylineMode_ConfirmPending_AppendsVertex()
        {
            var mode = new PolylineMode();
            mode.Begin(Vector2.zero, false);
            mode.OnEvent(MouseDown(), new Vector2(5f, 0f));
            mode.OnEvent(MouseMove(), new Vector2(8f, 3f));
            int before = RealVertexCount(mode);
            mode.ConfirmPending();
            Assert.AreEqual(before + 1, RealVertexCount(mode));
        }

        [Test]
        public void PointMode_CanConfirm_AlwaysFalse()
        {
            var mode = new PointMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsFalse(mode.CanConfirm);
        }

        [Test]
        public void MeasurementMode_CanConfirm_AlwaysFalse()
        {
            var mode = new MeasurementMode();
            mode.Begin(Vector2.zero, false);
            Assert.IsFalse(mode.CanConfirm);
        }

        // ── AnnotationModeFactory ────────────────────────────────────────────

        [Test]
        public void AnnotationModeFactory_Create_AllIds_ReturnCorrectType()
        {
            Assert.IsInstanceOf<PointMode>(AnnotationModeFactory.Create(AnnotationModeId.Point));
            Assert.IsInstanceOf<PolylineMode>(AnnotationModeFactory.Create(AnnotationModeId.Polyline));
            Assert.IsInstanceOf<MeasurementMode>(AnnotationModeFactory.Create(AnnotationModeId.Measurement));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static int RealVertexCount(PolylineMode mode)
        {
            // PreviewVertices = real verts + cursor (when active+incomplete)
            int total = mode.PreviewVertices.Count;
            return mode.IsActive && !mode.IsComplete ? total - 1 : total;
        }

        static Event MouseDown(int clickCount = 1)
            => new Event { type = EventType.MouseDown, button = 0, clickCount = clickCount };

        static Event MouseMove() => new Event { type = EventType.MouseMove };

        static Event Enter() => new Event
            { type = EventType.KeyDown, keyCode = KeyCode.Return };

        static Event Backspace() => new Event
            { type = EventType.KeyDown, keyCode = KeyCode.Backspace };
    }
}
