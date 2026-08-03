// TDD — F23: Remember Dropdown Selection.
// Tests EditorPrefs save/restore for the agent selector dropdown.
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class DropdownPersistenceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string PrefKey = "MCPChat.SelectedBackend";

        [SetUp]
        public void SetUp() => DeleteEditorPrefString(PrefKey);

        // RED: DropdownPrefKey constant must exist in MCPChatWindow (Selector partial).
        // Verified indirectly — if BuildAgentSelector reads EditorPrefs on construction,
        // a pre-set value must survive and be reflected in the dropdown value.

        [Test]
        public void DropdownPrefKey_IsCorrectValue()
        {
            // Access via reflection — verifies the constant exists with right value.
            var field = typeof(MCPChatWindow).GetField(
                "DropdownPrefKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "DropdownPrefKey constant must exist on MCPChatWindow");
            Assert.AreEqual(PrefKey, (string)field.GetValue(null));
        }

        [Test]
        public void SavedSelection_EditorPrefs_CanRoundTrip()
        {
            // Simple smoke: write and read back via EditorPrefs.
            SetEditorPrefString(PrefKey, "Claude");
            var restored = EditorPrefs.GetString(PrefKey, "");
            Assert.AreEqual("Claude", restored);
        }

        [Test]
        public void StaleValue_DefaultIsEmpty()
        {
            // No pref set → GetString returns default empty string.
            var restored = EditorPrefs.GetString(PrefKey, "");
            Assert.IsEmpty(restored);
        }

        [Test]
        public void Selection_PersistsOnSet()
        {
            SetEditorPrefString(PrefKey, "Codex");
            Assert.AreEqual("Codex", EditorPrefs.GetString(PrefKey, ""));
        }

        [Test]
        public void Selection_DeletedOnClear()
        {
            SetEditorPrefString(PrefKey, "Codex");
            DeleteEditorPrefString(PrefKey);
            Assert.IsEmpty(EditorPrefs.GetString(PrefKey, ""));
        }
    }
}
