using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests that MCPSettings.IsToolEnabled reads the correct EditorPrefs key.
    /// No UI instantiation — tests the key convention directly.
    /// </summary>
    [TestFixture]
    public class PluginSubcategorySettingsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string KeyA = "UnityMCP_Tool_tool_a";
        private const string KeyB = "UnityMCP_Tool_tool_b";

        [SetUp]
        public void SetUp()
        {
            SetEditorPrefBool(KeyA, true);
            SetEditorPrefBool(KeyB, true);
        }

        [Test]
        public void MCPSettings_IsToolEnabled_ReadsCorrectKey()
        {
            SetEditorPrefBool(KeyA, false);
            Assert.IsFalse(MCPSettings.IsToolEnabled("tool_a"),
                "MCPSettings.IsToolEnabled must read UnityMCP_Tool_{name}");
            Assert.IsTrue(MCPSettings.IsToolEnabled("tool_b"),
                "Sibling must still be enabled");
        }
    }
}
