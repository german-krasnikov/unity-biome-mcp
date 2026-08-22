// TDD tests for P2.4 get_status command (backing for mcp_status Python tool).
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class McpStatusCommandTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => CommandRegistry.InitDefaults());
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        [Test]
        public void ExecGetStatus_ReturnsSceneName()
        {
            var result = CommandRouter.ExecuteCommand("get_status", "{}");
            Assert.That(result, Does.Contain("scene="));
        }

        [Test]
        public void ExecGetStatus_ContainsAllKeys()
        {
            var result = CommandRouter.ExecuteCommand("get_status", "{}");
            Assert.That(result, Does.Contain("scene="));
            Assert.That(result, Does.Contain("dirty="));
            Assert.That(result, Does.Contain("playing="));
            Assert.That(result, Does.Contain("compiling="));
            Assert.That(result, Does.Contain("port="));
            Assert.That(result, Does.Contain("aliases="));
        }

        // GAP 1: Version identity fields
        [Test]
        public void ExecGetStatus_ContainsPluginVersion()
        {
            var result = CommandRouter.ExecuteCommand("get_status", "{}");
            Assert.That(result, Does.Contain("plugin_version="));
        }

        [Test]
        public void ExecGetStatus_ContainsProtocol()
        {
            var result = CommandRouter.ExecuteCommand("get_status", "{}");
            Assert.That(result, Does.Contain("protocol=4"));
        }

        [Test]
        public void ExecGetStatus_PluginVersionIsSemver()
        {
            var result = CommandRouter.ExecuteCommand("get_status", "{}");
            var line = System.Array.Find(result.Split('\n'), l => l.StartsWith("plugin_version="));
            Assert.IsNotNull(line, "plugin_version= line must be present");
            var ver = line.Split('=')[1].Trim();
            var parts = ver.Split('.');
            Assert.AreEqual(3, parts.Length, $"plugin_version must be semver, got: {ver}");
            foreach (var part in parts)
                Assert.IsTrue(int.TryParse(part.Split('-')[0], out _),
                    $"each semver segment must start with digits, got: {part}");
        }
    }
}
