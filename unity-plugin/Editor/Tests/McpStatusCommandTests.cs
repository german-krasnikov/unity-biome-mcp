// TDD tests for P2.4 get_status command (backing for mcp_status Python tool).
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class McpStatusCommandTests
    {
        [SetUp]
        public void SetUp()
        {
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
    }
}
