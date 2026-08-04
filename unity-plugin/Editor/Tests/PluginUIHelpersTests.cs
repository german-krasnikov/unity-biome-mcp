using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture, UnityMCP.Editor.Testing.RequiresGraphicsDevice]
    public class PluginUIHelpersTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string PID = "TestPlugin_UIHelpers";
        const string TextKey = "test_text";
        const string ToggleKey = "test_toggle";
        const string FloatKey = "test_float";
        const string IntKey = "test_int";
        const string DropdownKey = "test_dropdown";

        EditorWindow _win;

        [SetUp]
        public void SetUp()
        {
            DeleteEditorPrefString(PluginConfig.BuildKey(PID, TextKey));
            DeleteEditorPrefBool(PluginConfig.BuildKey(PID, ToggleKey));
            DeleteEditorPrefFloat(PluginConfig.BuildKey(PID, FloatKey));
            DeleteEditorPrefInt(PluginConfig.BuildKey(PID, IntKey));
            DeleteEditorPrefString(PluginConfig.BuildKey(PID, DropdownKey));
            _win = CreateOwnedEditorWindow<EditorWindow>();
            _win.ShowUtility();
        }

        // ── MakeCard ────────────────────────────────────────────────────────

        [Test]
        public void MakeCard_ReturnsFoldout_WithSamplingCardClass()
        {
            var card = PluginUIHelpers.MakeCard("Title");
            Assert.IsInstanceOf<Foldout>(card);
            Assert.IsTrue(card.ClassListContains("sampling-card"));
        }

        [Test]
        public void MakeCard_OpenTrue_FoldoutValueIsTrue()
        {
            var card = PluginUIHelpers.MakeCard("Title", open: true);
            Assert.IsTrue(card.value);
        }

        // ── InlineRow ───────────────────────────────────────────────────────

        [Test]
        public void InlineRow_HasSamplingInlineRowClass()
        {
            var row = PluginUIHelpers.InlineRow();
            Assert.IsTrue(row.ClassListContains("sampling-inline-row"));
        }

        // ── AddTextField ────────────────────────────────────────────────────

        [Test]
        public void AddTextField_InitFromDefault_WhenNoSavedValue()
        {
            var el = PluginUIHelpers.AddTextField(new VisualElement(), "L", PID, TextKey, "myDefault");
            Assert.AreEqual("myDefault", el.value);
        }

        [Test]
        public void AddTextField_InitFromSaved_WhenValueExists()
        {
            PluginConfig.SetString(PID, TextKey, "saved");
            var el = PluginUIHelpers.AddTextField(new VisualElement(), "L", PID, TextKey, "myDefault");
            Assert.AreEqual("saved", el.value);
        }

        [Test]
        public void AddTextField_CallbackWritesToPluginConfig()
        {
            var el = PluginUIHelpers.AddTextField(_win.rootVisualElement, "L", PID, TextKey);
            el.value = "written";
            Assert.AreEqual("written", PluginConfig.GetString(PID, TextKey));
        }

        [Test]
        public void AddTextField_AddedToParent()
        {
            var parent = new VisualElement();
            var el = PluginUIHelpers.AddTextField(parent, "L", PID, TextKey);
            Assert.IsTrue(parent.Contains(el));
        }

        // ── AddToggle ───────────────────────────────────────────────────────

        [Test]
        public void AddToggle_InitFromDefault()
        {
            var el = PluginUIHelpers.AddToggle(new VisualElement(), "L", PID, ToggleKey, defaultValue: true);
            Assert.IsTrue(el.value);
        }

        [Test]
        public void AddToggle_CallbackWritesToPluginConfig()
        {
            var el = PluginUIHelpers.AddToggle(_win.rootVisualElement, "L", PID, ToggleKey, defaultValue: false);
            el.value = true;
            Assert.IsTrue(PluginConfig.GetBool(PID, ToggleKey));
        }

        // ── AddSlider ───────────────────────────────────────────────────────

        [Test]
        public void AddSlider_InitFromDefault()
        {
            var el = PluginUIHelpers.AddSlider(new VisualElement(), "L", PID, FloatKey, 0.5f, 0f, 1f);
            Assert.AreEqual(0.5f, el.value, 0.001f);
        }

        [Test]
        public void AddSlider_CallbackWritesToPluginConfig()
        {
            var el = PluginUIHelpers.AddSlider(_win.rootVisualElement, "L", PID, FloatKey, 0f, 0f, 1f);
            el.value = 0.75f;
            Assert.AreEqual(0.75f, PluginConfig.GetFloat(PID, FloatKey), 0.001f);
        }

        // ── AddIntSlider ────────────────────────────────────────────────────

        [Test]
        public void AddIntSlider_InitFromDefault()
        {
            var el = PluginUIHelpers.AddIntSlider(new VisualElement(), "L", PID, IntKey, 5, 0, 10);
            Assert.AreEqual(5, el.value);
        }

        [Test]
        public void AddIntSlider_CallbackWritesToPluginConfig()
        {
            var el = PluginUIHelpers.AddIntSlider(_win.rootVisualElement, "L", PID, IntKey, 0, 0, 10);
            el.value = 7;
            Assert.AreEqual(7, PluginConfig.GetInt(PID, IntKey));
        }

        // ── AddDropdown ─────────────────────────────────────────────────────

        [Test]
        public void AddDropdown_InitFromDefault_FirstChoice()
        {
            var el = PluginUIHelpers.AddDropdown(new VisualElement(), "L", PID, DropdownKey, new[] { "A", "B", "C" });
            Assert.AreEqual("A", el.value);
        }

        [Test]
        public void AddDropdown_InitFromSaved()
        {
            PluginConfig.SetString(PID, DropdownKey, "B");
            var el = PluginUIHelpers.AddDropdown(new VisualElement(), "L", PID, DropdownKey, new[] { "A", "B", "C" });
            Assert.AreEqual("B", el.value);
        }

        [Test]
        public void AddDropdown_CallbackWritesToPluginConfig()
        {
            var el = PluginUIHelpers.AddDropdown(_win.rootVisualElement, "L", PID, DropdownKey, new[] { "A", "B", "C" });
            el.value = "C";
            Assert.AreEqual("C", PluginConfig.GetString(PID, DropdownKey));
        }

        [Test]
        public void AddDropdown_NullChoices_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                PluginUIHelpers.AddDropdown(new VisualElement(), "L", PID, DropdownKey, null));
        }

        [Test]
        public void AddDropdown_EmptyChoices_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                PluginUIHelpers.AddDropdown(new VisualElement(), "L", PID, DropdownKey, new string[0]));
        }

        [Test]
        public void AddDropdown_SavedValueNotInChoices_FallsBackToDefault()
        {
            PluginConfig.SetString(PID, DropdownKey, "Deleted");
            var el = PluginUIHelpers.AddDropdown(new VisualElement(), "L", PID, DropdownKey, new[] { "A", "B" }, "A");
            Assert.AreEqual("A", el.value);
        }
    }
}
