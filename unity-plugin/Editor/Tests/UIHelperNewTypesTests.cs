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
    }
}
