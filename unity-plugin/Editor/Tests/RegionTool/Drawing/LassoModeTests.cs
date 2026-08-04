using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    internal class LassoModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        LassoMode _mode;

        [SetUp]
        public void SetUp() => _mode = new LassoMode();

        // ── Initial state ───────────────────────────────────────────────────────

        [Test]
        public void InitialState_IdIsLasso()
            => Assert.AreEqual(DrawingModeId.Lasso, _mode.Id);

        [Test]
        public void InitialState_IsNotActive()
            => Assert.IsFalse(_mode.IsActive);

        [Test]
        public void InitialState_IsNotComplete()
            => Assert.IsFalse(_mode.IsComplete);

        [Test]
        public void InitialState_PreviewVerticesEmpty()
            => Assert.AreEqual(0, _mode.PreviewVertices.Count);

        // ── Begin ───────────────────────────────────────────────────────────────

        [Test]
        public void Begin_SetsIsActive()
        {
            _mode.Begin(Vector2.zero, false);
            Assert.IsTrue(_mode.IsActive);
        }

        [Test]
        public void Begin_AddsFirstPoint()
        {
            _mode.Begin(new Vector2(1f, 2f), false);
            Assert.AreEqual(1, _mode.PreviewVertices.Count);
        }

        [Test]
        public void Begin_SetsFirstPointCorrectly()
        {
            _mode.Begin(new Vector2(5f, 10f), false);
            Assert.AreEqual(new Vector2(5f, 10f), _mode.PreviewVertices[0]);
        }

        // ── OnEvent drag ────────────────────────────────────────────────────────

        [Test]
        public void OnEvent_MouseDrag_AppendsPoint()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            var e = MakeEvent(EventType.MouseDrag);
            _mode.OnEvent(e, new Vector2(1f, 0f));
            Assert.AreEqual(2, _mode.PreviewVertices.Count);
        }

        [Test]
        public void OnEvent_MouseDrag_MinDistanceFilter_DropsClosePoint()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            var e = MakeEvent(EventType.MouseDrag);
            // Distance = 0.1m < 0.2m threshold — must be dropped
            _mode.OnEvent(e, new Vector2(0.1f, 0f));
            Assert.AreEqual(1, _mode.PreviewVertices.Count);
        }

        [Test]
        public void OnEvent_MouseDrag_MinDistanceFilter_KeepsFarPoint()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            var e = MakeEvent(EventType.MouseDrag);
            // Distance = 0.25m >= 0.2m threshold — must be kept
            _mode.OnEvent(e, new Vector2(0.25f, 0f));
            Assert.AreEqual(2, _mode.PreviewVertices.Count);
        }

        [Test]
        public void OnEvent_MouseDrag_ConsumesEvent()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            var e = MakeEvent(EventType.MouseDrag);
            bool consumed = _mode.OnEvent(e, new Vector2(1f, 0f));
            Assert.IsTrue(consumed);
        }

        // ── MouseUp = IsComplete ────────────────────────────────────────────────

        [Test]
        public void OnEvent_MouseUp_SetsIsComplete()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(1f, 0f));
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(2f, 1f));
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(0f, 2f));
            _mode.OnEvent(MakeEvent(EventType.MouseUp), new Vector2(0f, 2f));
            Assert.IsTrue(_mode.IsComplete);
        }

        // ── Finalize ────────────────────────────────────────────────────────────

        [Test]
        public void Finalize_LessThan3Points_ReturnsNull()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(1f, 0f));
            var result = _mode.Finalize();
            Assert.IsNull(result);
        }

        [Test]
        public void Finalize_Exactly3Points_ReturnsPolygon()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(5f, 0f));
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(5f, 5f));
            _mode.OnEvent(MakeEvent(EventType.MouseUp), Vector2.zero);
            var result = _mode.Finalize();
            Assert.IsNotNull(result);
        }

        [Test]
        public void Finalize_ManyPoints_ReturnsRawPolygon()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            for (int i = 1; i <= 20; i++)
                _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(i * 0.5f, 0.001f * i));
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(10f, 5f));
            _mode.OnEvent(MakeEvent(EventType.MouseUp), Vector2.zero);
            var result = _mode.Finalize();
            Assert.IsNotNull(result);
            // LassoMode returns raw polygon — simplification is SceneRegionTool's job
            Assert.GreaterOrEqual(result.Value.Vertices.Length, 3);
        }

        // ── Reset ───────────────────────────────────────────────────────────────

        [Test]
        public void Reset_ClearsPreviewVertices()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeEvent(EventType.MouseDrag), new Vector2(1f, 0f));
            _mode.Reset();
            Assert.AreEqual(0, _mode.PreviewVertices.Count);
        }

        [Test]
        public void Reset_ClearsIsActive()
        {
            _mode.Begin(Vector2.zero, false);
            _mode.Reset();
            Assert.IsFalse(_mode.IsActive);
        }

        [Test]
        public void Reset_ClearsIsComplete()
        {
            _mode.Begin(new Vector2(0f, 0f), false);
            _mode.OnEvent(MakeEvent(EventType.MouseUp), Vector2.zero);
            _mode.Reset();
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

        // ── Helper ──────────────────────────────────────────────────────────────

        static Event MakeEvent(EventType type)
        {
            var e = new Event { type = type };
            return e;
        }
    }
}
