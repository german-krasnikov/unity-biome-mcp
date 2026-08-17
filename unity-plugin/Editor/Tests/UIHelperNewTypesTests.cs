// TDD: G5 — create_ui new types: Toggle, Slider, InputField, ScrollView.
// EditMode NUnit tests. No TCP required.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIHelperNewTypesTests : SceneTestBase
    {
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvas = TrackOwnedObject(new GameObject("UHNT_Canvas"));
            _canvas.AddComponent<Canvas>();
        }

        // ── 1. Toggle ──────────────────────────────────────────────────────────

        [Test]
        public void CreateUI_Toggle_ReturnsCreatedPath()
        {
            var result = UIHelper.CreateUI("Toggle", "TestToggle", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            Assert.That(result, Does.StartWith("Created"), $"Unexpected result: {result}");
        }

        [Test]
        public void CreateUI_Toggle_HasToggleComponent()
        {
            UIHelper.CreateUI("Toggle", "TestToggle2", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestToggle2");
            Assert.IsNotNull(go, "Toggle GameObject not found");
            TrackOwnedObject(go);
            Assert.IsNotNull(go.GetComponent<Toggle>(), "Toggle component missing");
        }

        [Test]
        public void CreateUI_Toggle_TargetGraphicWired()
        {
            UIHelper.CreateUI("Toggle", "TestToggle3", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestToggle3");
            Assert.IsNotNull(go, "Toggle GameObject not found");
            TrackOwnedObject(go);
            var toggle = go.GetComponent<Toggle>();
            Assert.IsNotNull(toggle.targetGraphic, "Toggle.targetGraphic must be wired");
        }

        // ── 2. Slider ──────────────────────────────────────────────────────────

        [Test]
        public void CreateUI_Slider_HasSliderWithFillRect()
        {
            UIHelper.CreateUI("Slider", "TestSlider", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSlider");
            Assert.IsNotNull(go, "Slider GameObject not found");
            TrackOwnedObject(go);
            var slider = go.GetComponent<Slider>();
            Assert.IsNotNull(slider, "Slider component missing");
            Assert.IsNotNull(slider.fillRect, "Slider.fillRect must be wired");
        }

        // ── 3. InputField ──────────────────────────────────────────────────────

        [Test]
        public void CreateUI_InputField_TextComponentWired()
        {
            UIHelper.CreateUI("InputField", "TestInputField", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestInputField");
            Assert.IsNotNull(go, "InputField GameObject not found");
            TrackOwnedObject(go);
            var inputField = go.GetComponent<InputField>();
            Assert.IsNotNull(inputField, "InputField component missing");
            Assert.IsNotNull(inputField.textComponent, "InputField.textComponent must be wired");
        }

        // ── 4. ScrollView ──────────────────────────────────────────────────────

        [Test]
        public void CreateUI_ScrollView_HasScrollRect()
        {
            UIHelper.CreateUI("ScrollView", "TestScrollView", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestScrollView");
            Assert.IsNotNull(go, "ScrollView GameObject not found");
            TrackOwnedObject(go);
            var scrollRect = go.GetComponent<ScrollRect>();
            Assert.IsNotNull(scrollRect, "ScrollRect component missing");
            Assert.IsNotNull(scrollRect.viewport, "ScrollRect.viewport must be wired");
            Assert.IsNotNull(scrollRect.content, "ScrollRect.content must be wired");
        }

        [Test]
        public void CreateUI_ScrollView_NoMaskOnRoot()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_NoMaskRoot", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSV_NoMaskRoot");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            Assert.IsNull(go.GetComponent<Mask>(), "Root must NOT have Mask component");
        }

        [Test]
        public void CreateUI_ScrollView_ViewportStretched()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_VPStretch", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSV_VPStretch");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            var viewport = go.transform.Find("Viewport");
            Assert.IsNotNull(viewport, "Viewport child not found");
            var rt = viewport.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, rt.anchorMin, "Viewport anchorMin must be (0,0)");
            Assert.AreEqual(Vector2.one, rt.anchorMax, "Viewport anchorMax must be (1,1)");
        }

        [Test]
        public void CreateUI_ScrollView_ContentHasContentSizeFitter()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_CSF", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSV_CSF");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            var viewport = go.transform.Find("Viewport");
            Assert.IsNotNull(viewport, "Viewport not found");
            var content = viewport.Find("Content");
            Assert.IsNotNull(content, "Content not found");
            Assert.IsNotNull(content.GetComponent<ContentSizeFitter>(),
                "Content must have ContentSizeFitter");
        }

        // ── U3: Content horizontal stretch + vertical-only ────────────────────

        [Test]
        public void CreateUI_ScrollView_ContentStretchesHorizontally()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_HStretch", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSV_HStretch");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            var content = go.transform.Find("Viewport/Content");
            Assert.IsNotNull(content, "Viewport/Content not found");
            var rt = content.GetComponent<RectTransform>();
            Assert.AreEqual(1f, rt.anchorMax.x, 0.001f,
                "Content anchorMax.x must be 1 for full-width horizontal stretch");
        }

        [Test]
        public void CreateUI_ScrollView_IsVerticalOnly()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_VertOnly", "UHNT_Canvas",
                null, null, null, null, null, null, null);
            var go = GameObject.Find("TestSV_VertOnly");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            var sr = go.GetComponent<ScrollRect>();
            Assert.IsFalse(sr.horizontal,
                "ScrollView must default to vertical-only (horizontal=false)");
            Assert.IsTrue(sr.vertical, "ScrollView must have vertical=true");
        }

        [Test]
        public void CreateUI_ScrollView_ColorApplied()
        {
            UIHelper.CreateUI("ScrollView", "TestSV_Color", "UHNT_Canvas",
                null, null, null, null, "#FF0000", null, null);
            var go = GameObject.Find("TestSV_Color");
            Assert.IsNotNull(go, "ScrollView not found");
            TrackOwnedObject(go);
            var img = go.GetComponent<Image>();
            Assert.IsNotNull(img, "Root must have Image");
            Assert.AreEqual(Color.red, img.color, "Root Image color must be red");
        }
    }
}
