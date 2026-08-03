using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigTargetsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void RelativePathFor_KnownKey_ReturnsPath()
        {
            Assert.AreEqual(".cursor/mcp.json", ProjectConfigTargets.RelativePathFor("cursor"));
        }

        [Test]
        public void RelativePathFor_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(ProjectConfigTargets.RelativePathFor("no-such-backend"));
        }

        [TestCase("claude-code", ".mcp.json")]
        [TestCase("vscode", ".vscode/mcp.json")]
        [TestCase("windsurf", ".windsurf/mcp.json")]
        [TestCase("codex", ".codex/config.toml")]
        [TestCase("junie", ".junie/mcp/mcp.json")]
        public void RelativePathFor_MatchesProjectConfigTargetsAll(string key, string expectedPath)
        {
            Assert.AreEqual(expectedPath, ProjectConfigTargets.RelativePathFor(key));
        }
    }
}
