using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class InstallDirTableTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ResolveFor_UnknownKey_ReturnsNull()
        {
            Assert.IsNull(InstallDirTable.ResolveFor("no-such-client", false, false, "/home/u", "/appdata"));
        }

        [Test]
        public void ResolveFor_Linux_UsesDirTemplate()
        {
            var resolved = InstallDirTable.ResolveFor("claude-desktop", isWindows: false, isMac: false,
                home: "/home/u", appData: "/appdata");
            Assert.AreEqual("/home/u/.config/Claude", resolved);
        }

        [Test]
        public void ResolveFor_Mac_PrefersDirMacOverride()
        {
            var resolved = InstallDirTable.ResolveFor("claude-desktop", isWindows: false, isMac: true,
                home: "/Users/u", appData: null);
            Assert.AreEqual("/Users/u/Library/Application Support/Claude", resolved);
        }

        [Test]
        public void ResolveFor_Windows_SubstitutesAppData()
        {
            var resolved = InstallDirTable.ResolveFor("claude-desktop", isWindows: true, isMac: false,
                home: null, appData: "C:/Users/u/AppData/Roaming");
            Assert.AreEqual("C:/Users/u/AppData/Roaming/Claude", resolved);
        }

        [Test]
        public void ResolveFor_MacWithNoOverride_FallsBackToDir()
        {
            // windsurf has dirWin but no dirMac -- mac falls back to the shared `dir` template.
            var resolved = InstallDirTable.ResolveFor("windsurf", isWindows: false, isMac: true,
                home: "/Users/u", appData: null);
            Assert.AreEqual("/Users/u/.codeium", resolved);
        }
    }
}
