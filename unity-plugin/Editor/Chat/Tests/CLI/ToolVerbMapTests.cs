using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolVerbMapTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Humanize_KnownTool_ReturnsVerb()
        {
            Assert.AreEqual("Reading scene",   ToolVerbMap.Humanize("mcp__unity-biome-mcp__get_hierarchy"));
            Assert.AreEqual("Editing",         ToolVerbMap.Humanize("mcp__unity-biome-mcp__set_property"));
            Assert.AreEqual("Creating",        ToolVerbMap.Humanize("mcp__unity-biome-mcp__create_object"));
            Assert.AreEqual("Deleting",        ToolVerbMap.Humanize("mcp__unity-biome-mcp__delete_object"));
            Assert.AreEqual("Playtesting",     ToolVerbMap.Humanize("mcp__unity-biome-mcp__run_playtest"));
            Assert.AreEqual("Running batch",   ToolVerbMap.Humanize("mcp__unity-biome-mcp__batch"));
            Assert.AreEqual("Checking refs",   ToolVerbMap.Humanize("mcp__unity-biome-mcp__validate_references"));
        }

        [Test]
        public void Humanize_UnknownTool_StripsPrefixAndReplaceUnderscores()
        {
            var result = ToolVerbMap.Humanize("mcp__unity-biome-mcp__some_custom_tool");
            Assert.AreEqual("some custom tool", result);
        }

        [Test]
        public void Humanize_NullInput_ReturnsWorking()
        {
            Assert.AreEqual("Working", ToolVerbMap.Humanize(null));
        }

        [Test]
        public void Humanize_EmptyInput_ReturnsWorking()
        {
            Assert.AreEqual("Working", ToolVerbMap.Humanize(""));
        }

        [Test]
        public void Humanize_NoPrefixTool_ReturnsAsIs()
        {
            // Tool without mcp__ prefix — falls back to underscore-replace
            var result = ToolVerbMap.Humanize("get_hierarchy");
            // Does not crash; returns something reasonable
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Humanize_LivePrefix_DriftGuard()
        {
            // Fails automatically if ToolVerbMap prefix ever drifts from PermissionConfig.
            Assert.AreEqual("Reading scene",
                ToolVerbMap.Humanize(PermissionConfig.MCP_TOOL_PREFIX + "get_hierarchy"));
        }
    }
}
