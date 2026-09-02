// TDD: DEV-61 [B3-#22] — BiomeVersion.Plugin must track package.json.
// MCPServer.PluginVersion is the existing source of truth read at runtime;
// BiomeVersion.Plugin is a compile-time constant meant to mirror it.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BiomeVersionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Plugin_MatchesMCPServerPluginVersion()
        {
            Assert.AreEqual(
                MCPServer.PluginVersion,
                BiomeVersion.Plugin,
                "BiomeVersion.Plugin must match MCPServer.PluginVersion / package.json (see scripts/sync_versions.py)");
        }
    }
}
