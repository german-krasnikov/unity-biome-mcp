using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClientConfigPathTableTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ResolveFor_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(ClientConfigPathTable.ResolveFor("no-such-client", false, false, "/home/u", "/appdata"));
        }

        [Test]
        public void ResolveFor_Linux_UsesPathTemplate()
        {
            var resolved = ClientConfigPathTable.ResolveFor("claude-desktop", isWindows: false, isMac: false,
                home: "/home/u", appData: "/appdata");
            Assert.AreEqual("/home/u/.config/Claude/claude_desktop_config.json", resolved);
        }

        [Test]
        public void ResolveFor_Mac_PrefersPathMacOverride()
        {
            var resolved = ClientConfigPathTable.ResolveFor("claude-desktop", isWindows: false, isMac: true,
                home: "/Users/u", appData: null);
            Assert.AreEqual("/Users/u/Library/Application Support/Claude/claude_desktop_config.json", resolved);
        }

        [Test]
        public void ResolveFor_Windows_SubstitutesAppData()
        {
            var resolved = ClientConfigPathTable.ResolveFor("claude-desktop", isWindows: true, isMac: false,
                home: null, appData: "C:/Users/u/AppData/Roaming");
            Assert.AreEqual("C:/Users/u/AppData/Roaming/Claude/claude_desktop_config.json", resolved);
        }

        [Test]
        public void ResolveFor_MacWithNoOverride_FallsBackToPath()
        {
            // windsurf has pathWin but no pathMac -- mac falls back to the shared `path` template.
            var resolved = ClientConfigPathTable.ResolveFor("windsurf", isWindows: false, isMac: true,
                home: "/Users/u", appData: null);
            Assert.AreEqual("/Users/u/.codeium/windsurf/mcp_config.json", resolved);
        }

        [Test]
        public void ResolveFor_ClaudeCode_LinuxAndMacShareSamePath()
        {
            var linux = ClientConfigPathTable.ResolveFor("claude-code", isWindows: false, isMac: false,
                home: "/home/u", appData: null);
            var mac = ClientConfigPathTable.ResolveFor("claude-code", isWindows: false, isMac: true,
                home: "/Users/u", appData: null);
            Assert.AreEqual("/home/u/.claude.json", linux);
            Assert.AreEqual("/Users/u/.claude.json", mac);
        }
    }
}
