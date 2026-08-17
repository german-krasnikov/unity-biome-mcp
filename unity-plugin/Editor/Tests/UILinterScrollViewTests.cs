// TDD: Integration tests for UILinter ScrollRect rules against UIHelper-created ScrollViews.
// EditMode NUnit tests. No TCP required.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UILinterScrollViewTests : SceneTestBase
    {
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvas = TrackOwnedObject(new GameObject("ULSVT_Canvas"));
            _canvas.AddComponent<Canvas>();
        }

        // ── S3: Mask on root (broken ScrollView) triggers warning ─────────────

        [Test]
        public void LintUGUI_BrokenScrollView_ReportsDoubleMask()
        {
            // Manually build broken structure: Mask on root AND on Viewport
            var root = TrackOwnedObject(new GameObject("BrokenSV_Mask", typeof(RectTransform)));
            root.transform.SetParent(_canvas.transform, false);
            root.AddComponent<Image>();
            root.AddComponent<Mask>();  // BUG: Mask on root
            var sr = root.AddComponent<ScrollRect>();

            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(root.transform, false);
            vpGo.AddComponent<Image>();
            vpGo.AddComponent<Mask>();
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;

            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vpGo.transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(0, 300);
            cGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S3]"),
                $"Expected [S3] double Mask warning, got: {result}");
        }

        // ── S1: Viewport not stretched triggers warning ───────────────────────

        [Test]
        public void LintUGUI_BrokenScrollView_ReportsViewportNotStretched()
        {
            var root = TrackOwnedObject(new GameObject("BrokenSV_VP", typeof(RectTransform)));
            root.transform.SetParent(_canvas.transform, false);
            root.AddComponent<Image>();
            var sr = root.AddComponent<ScrollRect>();

            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(root.transform, false);
            vpGo.AddComponent<Image>();
            vpGo.AddComponent<Mask>();
            var vpRt = vpGo.GetComponent<RectTransform>();
            // Non-stretch anchor (center point)
            vpRt.anchorMin = new Vector2(0.5f, 0.5f);
            vpRt.anchorMax = new Vector2(0.5f, 0.5f);
            sr.viewport = vpRt;

            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vpGo.transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(0, 300);
            sr.content = cRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S1]"),
                $"Expected [S1] viewport not stretched, got: {result}");
        }

        // ── V8-S3: Mask on root ONLY (no viewport Mask) triggers S3 ──────────

        [Test]
        public void LintUGUI_MaskOnRootOnly_ReportsS3()
        {
            // Mask on root but NOT on viewport — old rule only fires for both.
            // New split rule must catch root-only Mask.
            var root = TrackOwnedObject(new GameObject("BrokenSV_RootMaskOnly", typeof(RectTransform)));
            root.transform.SetParent(_canvas.transform, false);
            root.AddComponent<Image>();
            root.AddComponent<Mask>();  // Mask on root — wrong
            var sr = root.AddComponent<ScrollRect>();

            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(root.transform, false);
            vpGo.AddComponent<Image>();
            // No Mask on viewport — so double-mask check does NOT fire under old rule
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;

            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vpGo.transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(0, 300);
            cGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = cRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S3]"),
                $"Expected [S3] for Mask on root-only, got: {result}");
        }

        // ── V8-S6: Content zero width with horizontal=true triggers S6 ────────

        [Test]
        public void LintUGUI_ContentZeroWidth_ReportsS6()
        {
            // ScrollRect with horizontal=true, content anchorMax.x=0, sizeDelta.x=0
            var root = TrackOwnedObject(new GameObject("BrokenSV_ZeroWidth", typeof(RectTransform)));
            root.transform.SetParent(_canvas.transform, false);
            root.AddComponent<Image>();
            var sr = root.AddComponent<ScrollRect>();
            sr.horizontal = true;

            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(root.transform, false);
            vpGo.AddComponent<Image>();
            vpGo.AddComponent<Mask>();
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;

            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vpGo.transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            // Content with anchorMax.x=0 and sizeDelta.x=0 → zero effective width
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(0, 1);  // point anchor, no horizontal stretch
            cRt.sizeDelta = new Vector2(0, 300); // width = 0
            sr.content = cRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S6]"),
                $"Expected [S6] for zero-width content with horizontal=true, got: {result}");
        }

        // ── Canonical ScrollView (post-fix) has no [S*] warnings ─────────────

        [Test]
        public void LintUGUI_CanonicalScrollView_NoScrollRectIssues()
        {
            // Use the fixed UIHelper to create a canonical ScrollView.
            UIHelper.CreateUI("ScrollView", "CanonicalSV", "ULSVT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("CanonicalSV");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);

            var result = UILinter.LintUGUI(null);

            // S1-S5 warnings must not appear.
            Assert.That(result, Does.Not.Contain("[S1]"), $"Unexpected [S1]: {result}");
            Assert.That(result, Does.Not.Contain("[S2]"), $"Unexpected [S2]: {result}");
            Assert.That(result, Does.Not.Contain("[S3]"), $"Unexpected [S3]: {result}");
            Assert.That(result, Does.Not.Contain("[S4]"), $"Unexpected [S4]: {result}");
            Assert.That(result, Does.Not.Contain("[S5]"), $"Unexpected [S5]: {result}");
        }
    }
}
