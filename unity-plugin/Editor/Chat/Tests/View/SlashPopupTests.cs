// TDD — RED first. Tests for SlashPopup / MCPChatWindow.Slash integration (Feature #12).
// Bare VisualElement tree — no EditorWindow needed.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SlashPopupTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _inputContainer;
        private TextField     _field;
        private SlashPopup    _popup;

        [SetUp]
        public void SetUp()
        {
            _inputContainer = new VisualElement();
            _field = new TextField();
            _inputContainer.Add(_field);
            _popup = new SlashPopup(_inputContainer, _field);
        }

        [TearDown]
        public void TearDown() { _popup = null; }

        [Test]
        public void ApplyTemplate_SetsInputToPrefill()
        {
            var t = new SlashTemplate("test-cmd", "Do the thing.", ContextGather.None);
            _popup.Apply(t, gatherOverride: _ => null);
            Assert.AreEqual("Do the thing.", _field.value);
        }

        [Test]
        public void ApplyTemplate_ClearsSlashPrefix()
        {
            _field.value = "/test-cmd";
            var t = new SlashTemplate("test-cmd", "Resolved prefill.", ContextGather.None);
            _popup.Apply(t, gatherOverride: _ => null);
            Assert.IsFalse(_field.value.StartsWith("/"));
        }

        [Test]
        public void ApplyTemplate_WithContext_AppendsGatheredText()
        {
            var t = new SlashTemplate("ctx-cmd", "Prefill.", ContextGather.CompileErrors);
            _popup.Apply(t, gatherOverride: _ => "gathered-ctx");
            StringAssert.Contains("Prefill.",     _field.value);
            StringAssert.Contains("gathered-ctx", _field.value);
        }

        [Test]
        public void ApplyTemplate_EmptyInput_SetsPrefillDirectly()
        {
            _field.value = "";
            var t = new SlashTemplate("cmd", "Clean slate.", ContextGather.None);
            _popup.Apply(t, gatherOverride: _ => null);
            Assert.AreEqual("Clean slate.", _field.value);
        }

        [Test]
        public void DismissOnEsc_ClearsPopup()
        {
            _popup.Show(SlashRegistry.Builtins);
            Assert.IsTrue(_popup.IsVisible);
            _popup.Dismiss();
            Assert.IsFalse(_popup.IsVisible);
        }

        [Test]
        public void DismissOnBlur_ClearsPopup()
        {
            _popup.Show(SlashRegistry.Builtins);
            _popup.OnBlur();
            Assert.IsFalse(_popup.IsVisible);
        }

        [Test]
        public void SelectionNavigation_WrapsAround()
        {
            _popup.Show(SlashRegistry.Builtins);
            int count = SlashRegistry.Builtins.Length;
            // Navigate exactly count steps → index wraps back to 0.
            for (int i = 0; i < count; i++) _popup.MoveDown();
            Assert.AreEqual(0, _popup.SelectedIndex);
        }

        [Test]
        public void Show_AllItemsRendered_NoMaxCap()
        {
            var templates = new SlashTemplate[10];
            for (int i = 0; i < 10; i++)
                templates[i] = new SlashTemplate($"cmd-{i}", $"Prefill {i}.", ContextGather.None);

            _popup.Show(templates);

            Assert.AreEqual(10, _popup.VisibleCount, "All 10 items must be rendered — no MaxVisible cap");
        }

        // CH5.test.3: MoveUp() wraps around, and ApplySelected() applies current index

        [Test]
        public void MoveUp_FromIndex0_WrapsToLast()
        {
            _popup.Show(SlashRegistry.Builtins);
            // Start at 0, MoveUp once → wraps to last index
            _popup.MoveUp();
            Assert.AreEqual(SlashRegistry.Builtins.Length - 1, _popup.SelectedIndex,
                "MoveUp from index 0 must wrap to last item");
        }

        [Test]
        public void ApplySelected_AppliesCurrentlySelectedTemplate()
        {
            var templates = new[]
            {
                new SlashTemplate("a", "Prefill A.", ContextGather.None),
                new SlashTemplate("b", "Prefill B.", ContextGather.None),
            };
            _popup.Show(templates);
            _popup.MoveDown(); // select index 1

            _popup.ApplySelected();

            Assert.AreEqual("Prefill B.", _field.value,
                "ApplySelected must apply the currently selected template (index 1)");
        }

        [Test]
        public void ApplySelected_EmptyItems_NoThrow()
        {
            // Show with empty, then ApplySelected — must not throw
            _popup.Show(new SlashTemplate[0]);
            Assert.DoesNotThrow(() => _popup.ApplySelected());
        }

        [Test]
        public void Show_EmptyItems_DoesNotRender()
        {
            _popup.Show(new SlashTemplate[0]);
            Assert.IsFalse(_popup.IsVisible, "empty item list must not show popup");
            Assert.AreEqual(0, _popup.VisibleCount);
        }
    }
}
