// TDD: MCPServer.ShouldSweepPorts — periodic stale-port sweep throttle guard.
// EditMode, no TCP required. Pure helper, mirrors ShouldStartServer pattern:
// only the seam gets direct unit coverage, not WatchdogTick itself (tied to
// EditorApplication.timeSinceStartup).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerSweepTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ShouldSweepPorts_BelowInterval_ReturnsFalse()
        {
            Assert.IsFalse(MCPServer.ShouldSweepPorts(now: 10.0, lastSweep: 0.0, intervalSeconds: 30.0));
        }

        [Test]
        public void ShouldSweepPorts_AtOrAboveInterval_ReturnsTrue()
        {
            Assert.IsTrue(MCPServer.ShouldSweepPorts(now: 30.0, lastSweep: 0.0, intervalSeconds: 30.0));
            Assert.IsTrue(MCPServer.ShouldSweepPorts(now: 31.0, lastSweep: 0.0, intervalSeconds: 30.0));
        }
    }
}
