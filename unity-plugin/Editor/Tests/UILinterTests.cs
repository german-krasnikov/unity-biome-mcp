// TDD: ScrollRect lint rules (S1-S5) and general layout rules (G1-G3).
// EditMode NUnit tests. No TCP required.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UILinterTests : SceneTestBase
    {
        // ── Setup helpers ─────────────────────────────────────────────────────

        private ScrollRect MakeScrollRect(string goName)
        {
            var go = TrackOwnedObject(new GameObject(goName, typeof(RectTransform)));
            return go.AddComponent<ScrollRect>();
        }

        private RectTransform MakeViewport(GameObject parent, string name = "Viewport")
        {
            var vp = TrackOwnedObject(new GameObject(name, typeof(RectTransform)));
            vp.transform.SetParent(parent.transform, false);
            vp.AddComponent<Image>();
            vp.AddComponent<Mask>();
            return vp.GetComponent<RectTransform>();
        }

        private RectTransform MakeContent(GameObject parent, string name = "Content")
        {
            var c = TrackOwnedObject(new GameObject(name, typeof(RectTransform)));
            c.transform.SetParent(parent.transform, false);
            return c.GetComponent<RectTransform>();
        }

        // ── S5: ScrollRect.content null ───────────────────────────────────────

        [Test]
        public void LintUGUI_ScrollRect_NullContent_ReturnsS5Warning()
        {
            var sr = MakeScrollRect("SV_S5");
            var vp = MakeViewport(sr.gameObject);
            sr.viewport = vp;
            sr.content = null;  // explicitly null

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S5]"),
                $"Expected [S5] content null warning, got: {result}");
        }

        // ── S1: Viewport not full-stretch ─────────────────────────────────────

        [Test]
        public void LintUGUI_ScrollRect_ViewportNotStretch_ReturnsS1Warning()
        {
            var sr = MakeScrollRect("SV_S1");
            var vpRt = MakeViewport(sr.gameObject);
            vpRt.anchorMin = new Vector2(0.5f, 0.5f);
            vpRt.anchorMax = new Vector2(0.5f, 0.5f);
            sr.viewport = vpRt;
            var contentRt = MakeContent(sr.gameObject);
            contentRt.sizeDelta = new Vector2(0, 300);
            sr.content = contentRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S1]"),
                $"Expected [S1] viewport not stretched warning, got: {result}");
        }

        // ── S2: Content cannot grow ───────────────────────────────────────────

        [Test]
        public void LintUGUI_ScrollRect_NoContentFitter_ReturnsS2Warning()
        {
            var sr = MakeScrollRect("SV_S2");
            var vpRt = MakeViewport(sr.gameObject);
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;
            var contentRt = MakeContent(sr.gameObject);
            contentRt.sizeDelta = Vector2.zero;  // zero size, no CSF
            sr.content = contentRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S2]"),
                $"Expected [S2] content cannot grow warning, got: {result}");
        }

        // ── S3: Nested Mask (root + viewport both have Mask) ──────────────────

        [Test]
        public void LintUGUI_ScrollRect_NestedMask_ReturnsS3Warning()
        {
            var go = TrackOwnedObject(new GameObject("SV_S3", typeof(RectTransform)));
            go.AddComponent<Image>();
            go.AddComponent<Mask>();  // BUG: Mask on root
            var sr = go.AddComponent<ScrollRect>();

            var vpRt = MakeViewport(go);  // Viewport also has Mask
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;
            var contentRt = MakeContent(go);
            contentRt.sizeDelta = new Vector2(0, 300);
            sr.content = contentRt;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S3]"),
                $"Expected [S3] nested mask warning, got: {result}");
        }

        // ── S4: Scrollbar present but unwired ─────────────────────────────────

        [Test]
        public void LintUGUI_ScrollRect_UnwiredScrollbar_ReturnsS4Warning()
        {
            var sr = MakeScrollRect("SV_S4");
            var vpRt = MakeViewport(sr.gameObject);
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            sr.viewport = vpRt;
            var contentRt = MakeContent(sr.gameObject);
            contentRt.sizeDelta = new Vector2(0, 300);
            sr.content = contentRt;

            // Scrollbar child — not wired to ScrollRect
            var sbGo = TrackOwnedObject(new GameObject("Scrollbar", typeof(RectTransform)));
            sbGo.transform.SetParent(sr.transform, false);
            sbGo.AddComponent<Scrollbar>();
            // sr.verticalScrollbar stays null

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[S4]"),
                $"Expected [S4] unwired scrollbar warning, got: {result}");
        }

        // ── G1: Active RectTransform with zero size and point anchor ──────────

        [Test]
        public void LintUGUI_ZeroSizeActiveRectTransform_ReturnsG1Warning()
        {
            var go = TrackOwnedObject(new GameObject("G1_ZeroRT", typeof(RectTransform)));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[G1]"),
                $"Expected [G1] zero size RT warning, got: {result}");
        }

        // ── G2: Image without sprite, raycastTarget=true, no Selectable parent ─

        [Test]
        public void LintUGUI_InvisibleBlockerImage_ReturnsG2Warning()
        {
            var go = TrackOwnedObject(new GameObject("G2_Blocker", typeof(RectTransform)));
            var img = go.AddComponent<Image>();
            img.sprite = null;
            img.raycastTarget = true;
            // No Selectable ancestor

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[G2]"),
                $"Expected [G2] invisible blocker warning, got: {result}");
        }

        // ── G3: LayoutGroup with no active children ────────────────────────────

        [Test]
        public void LintUGUI_EmptyLayoutGroup_ReturnsG3Warning()
        {
            var go = TrackOwnedObject(new GameObject("G3_EmptyLG", typeof(RectTransform)));
            go.AddComponent<HorizontalLayoutGroup>();
            // No children

            var result = UILinter.LintUGUI(null);

            Assert.That(result, Does.Contain("[G3]"),
                $"Expected [G3] empty layout group warning, got: {result}");
        }

        // ── Clean scene: no issues beyond EventSystem/Canvas ──────────────────

        [Test]
        public void LintUGUI_CleanScene_ReturnsOkZeroIssues()
        {
            // Satisfy EventSystem check.
            var esGo = TrackOwnedObject(new GameObject("CleanES"));
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Satisfy GraphicRaycaster check: Canvas with GR.
            var canvasGo = TrackOwnedObject(new GameObject("CleanCanvas"));
            canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            // Stretch canvas RT so G1 (zero-size point anchor) doesn't fire on it.
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            if (canvasRt != null)
            {
                canvasRt.anchorMin = Vector2.zero;
                canvasRt.anchorMax = Vector2.one;
                canvasRt.sizeDelta = Vector2.zero;
            }

            // No ScrollRects, no problematic Images, no empty LayoutGroups.

            var result = UILinter.LintUGUI(null);

            Assert.AreEqual("ok: 0 issues", result,
                $"Expected clean scene result, got: {result}");
        }
    }
}
