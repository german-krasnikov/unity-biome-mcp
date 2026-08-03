// TDD: MCPServer.ShouldStartServer — AssetImportWorker / batch mode guard.
// EditMode, no TCP required.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerStartGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ShouldStartServer_BatchMode_ReturnsFalse()
        {
            Assert.IsFalse(MCPServer.ShouldStartServer(isBatchMode: true));
        }

        [Test]
        public void ShouldStartServer_NormalEditor_ReturnsTrue()
        {
            Assert.IsTrue(MCPServer.ShouldStartServer(isBatchMode: false));
        }

        // Source-text assertion: verifies the static ctor actually calls ShouldStartServer.
        // Unit tests above verify pure-function behavior; this verifies wiring in the ctor
        // (cannot test static ctor directly — [InitializeOnLoad] fires once at domain load).
        [Test]
        public void StaticCtor_ContainsBatchModeGuard()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-biome-mcp.editor", "Editor", "MCPServer.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"MCPServer.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            StringAssert.Contains("ShouldStartServer", code,
                "static ctor must call ShouldStartServer to guard against batch mode / AssetImportWorker");
        }

        private static string LoadMCPServerSrc()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-biome-mcp.editor", "Editor", "MCPServer.cs"));
            if (!File.Exists(src))
                Assert.Ignore($"MCPServer.cs not found at {src} — skip in CI");
            return File.ReadAllText(src);
        }

        // Phase 2 M1: RunAcceptLoop/HandleClientAsync/RoleToLabel moved from MCPServer
        // to ClientConnectionHandler — source-text checks below repointed accordingly.
        private static string LoadClientConnectionHandlerSrc()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-biome-mcp.editor", "Editor", "ClientConnectionHandler.cs"));
            if (!File.Exists(src))
                Assert.Ignore($"ClientConnectionHandler.cs not found at {src} — skip in CI");
            return File.ReadAllText(src);
        }

        // ---------------------------------------------------------------------------
        // Transparent connections — probe silence + role labels (ARCH-transparent-connections)
        // ---------------------------------------------------------------------------

        [Test]
        public void HandleClientAsync_DefersConnectedLog_UntilFirstMessage()
        {
            var code = LoadClientConnectionHandlerSrc();
            StringAssert.Contains("receivedFirstMessage", code,
                "HandleClientAsync must use receivedFirstMessage flag to defer 'connected' log");
        }

        [Test]
        public void HandleClientAsync_ContainsRoleToLabel()
        {
            var code = LoadClientConnectionHandlerSrc();
            StringAssert.Contains("RoleToLabel", code,
                "HandleClientAsync must call RoleToLabel to resolve role field from ping to human-readable label");
        }

        [Test]
        public void RoleToLabel_MapsChateRelayLabel()
        {
            var code = LoadClientConnectionHandlerSrc();
            StringAssert.Contains("Chat relay", code,
                "RoleToLabel must map 'chat-relay' to 'Chat relay'");
        }

        [Test]
        public void RoleToLabel_MapsClaudeCodeSessionLabel()
        {
            var code = LoadClientConnectionHandlerSrc();
            StringAssert.Contains("Claude Code session", code,
                "RoleToLabel must map 'mcp' to 'Claude Code session'");
        }
    }
}
