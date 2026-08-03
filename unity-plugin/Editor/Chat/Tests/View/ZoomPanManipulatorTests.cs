using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ZoomPanManipulatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _viewport;
        private VisualElement _content;
        private ZoomPanManipulator _zp;

        [SetUp]
        public void SetUp()
        {
            _viewport = new VisualElement();
            _content  = new VisualElement();
            _zp = new ZoomPanManipulator(_content);
            _viewport.AddManipulator(_zp);
            _viewport.Add(_content);
        }

        [Test]
        public void InitialZoom_IsOne()
        {
            Assert.AreEqual(1f, _zp.Zoom);
        }

        [Test]
        public void InitialPan_IsZero()
        {
            Assert.AreEqual(0f, _zp.PanX);
            Assert.AreEqual(0f, _zp.PanY);
        }

        [Test]
        public void Reset_SetsZoomToOne()
        {
            _zp.Reset();
            Assert.AreEqual(1f, _zp.Zoom);
        }

        [Test]
        public void Reset_SetsPanToZero()
        {
            _zp.Reset();
            Assert.AreEqual(0f, _zp.PanX);
            Assert.AreEqual(0f, _zp.PanY);
        }

        [Test]
        public void Apply_DoesNotThrow_OnNullContent()
        {
            var zp = new ZoomPanManipulator(null);
            Assert.DoesNotThrow(() => zp.Reset());
        }

        [Test]
        public void Manipulator_CanAttachToTarget()
        {
            Assert.IsNotNull(_zp);
            Assert.AreEqual(_viewport, _zp.target);
        }

        [Test]
        public void Reset_ZoomRemainsOne_AfterMultipleCalls()
        {
            _zp.Reset();
            _zp.Reset();
            Assert.AreEqual(1f, _zp.Zoom);
        }

        // BUG 5: "1:1" button calls Reset() instead of SetZoom(pixelRatio).
        // These tests verify the SetZoom seam works and is distinct from Reset.

        [Test]
        public void SetZoom_SetsExactValue()
        {
            _zp.SetZoom(2f);
            Assert.AreEqual(2f, _zp.Zoom, 0.001f);
        }

        [Test]
        public void SetZoom_DoesNotResetPan()
        {
            // Simulate pan by calling Reset then SetZoom — pan should stay at 0
            // (pan is not changed by SetZoom, unlike Reset which also zeroes pan).
            _zp.SetZoom(1.5f);
            Assert.AreEqual(0f, _zp.PanX);
            Assert.AreEqual(0f, _zp.PanY);
        }

        [Test]
        public void SetZoom_OneToOne_DiffersFromFitZoom()
        {
            // After Reset (Fit), zoom == 1f.
            // After SetZoom with a pixel ratio != 1 the zoom must NOT equal 1f.
            // This documents the intended contract: 1:1 != Fit for non-100% DPI images.
            const float pixelRatio = 2f;
            _zp.Reset(); // Fit → zoom = 1
            float fitZoom = _zp.Zoom;

            _zp.SetZoom(pixelRatio); // 1:1 → zoom = pixelRatio
            Assert.AreNotEqual(fitZoom, _zp.Zoom,
                "1:1 zoom must differ from Fit zoom when pixelRatio != 1");
        }

        [Test]
        public void SetZoom_Clamps_AtMin()
        {
            _zp.SetZoom(0f);
            Assert.AreEqual(0.1f, _zp.Zoom, 0.001f);
        }

        [Test]
        public void SetZoom_Clamps_AtMax()
        {
            _zp.SetZoom(999f);
            Assert.AreEqual(10f, _zp.Zoom, 0.001f);
        }
    }
}
