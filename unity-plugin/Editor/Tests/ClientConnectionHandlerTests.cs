// TDD: RefManager.Invalidate deferred to first slow-path command.
// Fast-path commands (ping/get_version/status/get_enabled_tools) skip invalidation.
// Slow-path (any mutating command) triggers it exactly once per connection.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClientConnectionHandlerTests
    {
        // Fast-path commands must NOT trigger ref invalidation.
        [TestCase("ping")]
        [TestCase("get_version")]
        [TestCase("status")]
        [TestCase("get_enabled_tools")]
        public void IsSlowPath_FastPathCommands_ReturnFalse(string cmd)
        {
            Assert.IsFalse(ClientConnectionHandler.IsSlowPath(cmd));
        }

        // Slow-path (non-probe) commands trigger ref invalidation.
        [TestCase("create_object")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        [TestCase("batch")]
        [TestCase("run_tests")]
        public void IsSlowPath_SlowPathCommands_ReturnTrue(string cmd)
        {
            Assert.IsTrue(ClientConnectionHandler.IsSlowPath(cmd));
        }

        // Null / empty must be treated as slow-path (safe default — don't skip invalidate).
        [TestCase("")]
        [TestCase(null)]
        public void IsSlowPath_NullOrEmpty_ReturnTrue(string cmd)
        {
            Assert.IsTrue(ClientConnectionHandler.IsSlowPath(cmd));
        }
    }
}
