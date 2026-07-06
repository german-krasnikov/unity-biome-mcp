// TDD: Command registration init-order tests (formerly Bootstrap.Init, now MCPServer.StartAsync).
// Bootstrap.cs was deleted in the registration-race fix: MCPServer.StartAsync now calls
// CommandRegistry.InitDefaults() BEFORE _listener.Start() to guarantee commands are registered
// before the first AcceptTcpClientAsync. This file verifies that wiring via source-text assertions.
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BootstrapTests
    {
        [Test]
        public void MCPServer_CallsInitDefaults_BeforeTcpBind()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "MCPServer.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"MCPServer.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            var initIndex = code.IndexOf("CommandRegistry.InitDefaults()");
            var bindIndex = code.IndexOf("_listener.Start()");
            Assert.GreaterOrEqual(initIndex, 0, "StartAsync must call CommandRegistry.InitDefaults()");
            Assert.GreaterOrEqual(bindIndex, 0, "StartAsync must call _listener.Start()");
            Assert.Less(initIndex, bindIndex, "InitDefaults() must appear before _listener.Start()");
        }

        [Test]
        public void Bootstrap_FileDeleted_NoLongerExists()
        {
            // Bootstrap.cs was deleted in the registration-race fix (registration-gate sprint).
            // If this file reappears, the double-registration risk returns.
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "Bootstrap.cs"));
            Assert.IsFalse(File.Exists(src),
                "Bootstrap.cs must not exist — its responsibility was absorbed by MCPServer.StartAsync");
        }

        [Test]
        public void CommandRegistry_HasNoStaticConstructorCall_ToInitDefaults()
        {
            var src = Path.GetFullPath(
                Path.Combine("Packages", "com.unity-mcp.editor", "Editor", "CommandRegistry.cs"));
            if (!File.Exists(src))
            {
                Assert.Ignore($"CommandRegistry.cs not found at {src} — skip in CI");
                return;
            }
            var code = File.ReadAllText(src);
            StringAssert.DoesNotContain("static CommandRegistry()", code,
                "CommandRegistry must not eagerly self-populate via a static ctor (M7) — MCPServer.StartAsync owns that now");
        }
    }
}
