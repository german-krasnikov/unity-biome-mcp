// TDD: MCPServer.ShouldStartServer — AssetImportWorker / batch mode guard.
// EditMode, no TCP required.
using System.IO;
using System;
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
            var previous = Environment.GetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE");
            try
            {
                Environment.SetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE", null);
                Assert.IsFalse(MCPServer.ShouldStartServer(isBatchMode: true));
            }
            finally
            {
                Environment.SetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE", previous);
            }
        }

        [Test]
        public void ShouldStartServer_NormalEditor_ReturnsTrue()
        {
            Assert.IsTrue(MCPServer.ShouldStartServer(isBatchMode: false));
        }

        [Test]
        public void ShouldStartServer_BatchModeWithCiOptIn_ReturnsTrue()
        {
            var previous = Environment.GetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE");
            try
            {
                Environment.SetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE", "1");
                Assert.IsTrue(MCPServer.ShouldStartServer(isBatchMode: true));
            }
            finally
            {
                Environment.SetEnvironmentVariable("UNITY_MCP_ENABLE_BATCHMODE", previous);
            }
        }

        [Test]
        public void ResolveBootstrapScenePath_ProjectRelativePath_ReturnsAbsolutePath()
        {
            var root = Path.GetFullPath(Path.Combine("Temp", "McpBootstrapRoot"));
            var resolved = MCPServer.ResolveBootstrapScenePath(root, "Assets/Scenes/GridTest.unity");

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(root, "Assets", "Scenes", "GridTest.unity")),
                resolved);
        }

        [Test]
        public void ResolveBootstrapScenePath_EmptyPath_ReturnsNull()
        {
            Assert.IsNull(MCPServer.ResolveBootstrapScenePath("Temp", ""));
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

        // ---------------------------------------------------------------------------
        // Windows port stability — TIME_WAIT mitigation (Phase 4)
        // ---------------------------------------------------------------------------

        [Test]
        public void MCPServer_WindowsHasMoreBindRetries()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Contain("#if UNITY_EDITOR_WIN"),
                "MCPServer must have Windows-specific bind retry count");
            Assert.That(src, Does.Contain("maxAttempts = 6"),
                "Windows must have 6 bind retry attempts to cover longer TIME_WAIT window");
        }

        [Test]
        public void MCPServer_SetsLingerBeforeTeardown()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Contain("LingerOption(true, 0)"),
                "MCPServer must set linger=0 before teardown to avoid TIME_WAIT on Windows");
        }

        // ARC-8 T1: StartAsync's main + chat retry loops must delegate their
        // same-port-vs-fallback boundary math to PortResolver, not inline it —
        // the inline form silently dropped the last budgeted same-port retry.
        [Test]
        public void MCPServer_RetryLoopsDelegateToPortResolver()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Contain("PortResolver.IsSamePortAttempt"),
                "StartAsync retry loops must call PortResolver.IsSamePortAttempt for the same-port-vs-fallback branch");
            Assert.That(src, Does.Contain("PortResolver.BackoffDelayMs"),
                "StartAsync retry loops must call PortResolver.BackoffDelayMs for the retry delay");
        }

        [Test]
        public void MCPServer_RetryLoopsNoInlineOffByOneLiteral()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Not.Contain("attempt == maxAttempts - 1"),
                "StartAsync must not inline the off-by-one boundary check — use PortResolver.IsSamePortAttempt");
        }

        // ---------------------------------------------------------------------------
        // WP9: SO_REUSEPORT must be macOS-only (Linux uses different constant = 15)
        // ---------------------------------------------------------------------------

        [Test]
        public void MCPServer_SoReusePortOnlyMacOS()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Not.Contain("UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX"),
                "SO_REUSEPORT (0x0200) is the macOS value; Linux uses 15 — guard must be macOS-only");
            Assert.That(src, Does.Contain("UNITY_EDITOR_OSX"),
                "MCPServer must still apply SO_REUSEPORT on macOS");
        }

        [Test]
        public void PortResolver_SoReusePortOnlyMacOS()
        {
            var src = ReadRequiredPackageSource(typeof(PortResolver), "Editor/PortResolver.cs");
            Assert.That(src, Does.Not.Contain("UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX"),
                "SO_REUSEPORT (0x0200) is the macOS value; Linux uses 15 — guard must be macOS-only");
        }
    }
}
