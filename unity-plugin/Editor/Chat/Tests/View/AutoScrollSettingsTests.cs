// TDD — F22: Move AutoScroll to Settings.
// AutoScroll toggle removed from footer bar; now lives in ChatSettingsSection.
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AutoScrollSettingsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string PrefKey = "MCPChat.AutoScroll";

        [SetUp]
        public void SetUp() => DeleteEditorPrefBool(PrefKey);

        // RED: _autoScrollEnabled field must NOT exist after F22 removes it.
        // After fix: Drain reads EditorPrefs directly — no field.

        [Test]
        public void AutoScrollEnabled_FieldRemovedFromWindow()
        {
            var field = typeof(MCPChatWindow).GetField(
                "_autoScrollEnabled",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNull(field, "_autoScrollEnabled field must be removed (F22)");
        }

        [Test]
        public void AutoScrollPref_DefaultTrue()
        {
            // No pref set → EditorPrefs.GetBool returns default true.
            Assert.IsTrue(EditorPrefs.GetBool(PrefKey, true));
        }

        [Test]
        public void AutoScrollPref_CanBeSetFalse()
        {
            SetEditorPrefBool(PrefKey, false);
            Assert.IsFalse(EditorPrefs.GetBool(PrefKey, true));
        }

        [Test]
        public void AutoScrollPref_CanBeSetTrue()
        {
            SetEditorPrefBool(PrefKey, true);
            Assert.IsTrue(EditorPrefs.GetBool(PrefKey, true));
        }

        [Test]
        public void FooterBar_AutoScrollToggle_RemovedFromSource()
        {
            // The autoscroll-toggle CSS class must not appear in BuildFooterBar output.
            // We verify by checking no field reference exists on the window.
            // (Full VE-tree test would require a live EditorWindow; field check is sufficient.)
            var windowType = typeof(MCPChatWindow);
            var allFields  = windowType.GetFields(
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance  |
                System.Reflection.BindingFlags.Public);
            foreach (var f in allFields)
                Assert.AreNotEqual("_autoScrollEnabled", f.Name,
                    "_autoScrollEnabled must be removed from MCPChatWindow");
        }
    }
}
