// BUG 5 TDD: ImageViewerWindow "1:1" button calls Reset() instead of SetZoom(pixelRatio).
//
// RED tests: ApplyOneToOne with pixelRatio != 1f must produce zoom != 1f.
// Currently ApplyOneToOne always calls Reset() → zoom always == 1f → tests FAIL.
//
// GREEN fix: replace zp?.Reset() with zp?.SetZoom(pixelRatio) in ApplyOneToOne.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ImageViewerWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── CalcPixelRatio (pure seam — GREEN immediately) ───────────────────

        [Test]
        public void CalcPixelRatio_NormalValues_ReturnsRatio()
            => Assert.AreEqual(2f, ImageViewerWindow.CalcPixelRatio(800, 400f), 0.001f);

        [Test]
        public void CalcPixelRatio_SameDimensions_ReturnsOne()
            => Assert.AreEqual(1f, ImageViewerWindow.CalcPixelRatio(400, 400f), 0.001f);

        [Test]
        public void CalcPixelRatio_ZeroViewport_ReturnsFallback()
            => Assert.AreEqual(1f, ImageViewerWindow.CalcPixelRatio(800, 0f), 0.001f);

        [Test]
        public void CalcPixelRatio_ZeroTexWidth_ReturnsFallback()
            => Assert.AreEqual(1f, ImageViewerWindow.CalcPixelRatio(0, 400f), 0.001f);

        // ── BUG 5: ApplyOneToOne must use SetZoom, not Reset ─────────────────
        // These FAIL because ApplyOneToOne currently calls Reset() → zoom = 1f
        // regardless of pixelRatio argument.

        [Test]
        public void ApplyOneToOne_HiDpiRatio_ZoomEqualsRatio()
        {
            var content = new VisualElement();
            var zp      = new ZoomPanManipulator(content);
            // pixel ratio 2 → expect zoom == 2, but BUG: Reset() → zoom == 1
            ImageViewerWindow.ApplyOneToOne(zp, zoomLabel: null, pixelRatio: 2f);
            Assert.AreEqual(2f, zp.Zoom, 0.001f,
                "1:1 with pixelRatio=2 must set zoom to 2, not 1 (BUG 5)");
        }

        [Test]
        public void ApplyOneToOne_SubPixelRatio_ZoomEqualsRatio()
        {
            var content = new VisualElement();
            var zp      = new ZoomPanManipulator(content);
            // small image → ratio 0.5 → expect zoom == 0.5
            ImageViewerWindow.ApplyOneToOne(zp, zoomLabel: null, pixelRatio: 0.5f);
            Assert.AreEqual(0.5f, zp.Zoom, 0.001f,
                "1:1 with pixelRatio=0.5 must set zoom to 0.5, not 1 (BUG 5)");
        }

        [Test]
        public void ApplyOneToOne_DiffersFromFit_WhenPixelRatioNotOne()
        {
            var content  = new VisualElement();
            var zp       = new ZoomPanManipulator(content);
            zp.Reset(); // Fit
            float fitZoom = zp.Zoom; // 1f

            ImageViewerWindow.ApplyOneToOne(zp, zoomLabel: null, pixelRatio: 2f);
            Assert.AreNotEqual(fitZoom, zp.Zoom,
                "1:1 zoom must differ from Fit zoom for 2× image (BUG 5)");
        }

        [Test]
        public void ApplyOneToOne_UpdatesLabel()
        {
            var content = new VisualElement();
            var zp      = new ZoomPanManipulator(content);
            var label   = new Label("0%");
            ImageViewerWindow.ApplyOneToOne(zp, label, pixelRatio: 2f);
            // label should show 200%, not 100%
            Assert.AreEqual("200%", label.text,
                "1:1 label must show actual zoom percentage (BUG 5)");
        }
    }
}
