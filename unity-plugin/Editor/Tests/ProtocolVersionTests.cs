// TDD: MCPServer.BuildVersionString — new proto:3 format. EditMode, no TCP required.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProtocolVersionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetVersion_ReturnsNewFormat()
        {
            var result = MCPServer.BuildVersionString("deadbeef", "0.37.0");
            StringAssert.StartsWith("proto:3|plugin:", result);
            StringAssert.Contains("|stamp:deadbeef", result);
        }

        [Test]
        public void GetVersion_ContainsPluginVersion()
        {
            var result = MCPServer.BuildVersionString("abc", "1.2.3");
            StringAssert.Contains("plugin:1.2.3", result);
        }

        [Test]
        public void GetVersion_EmptyStamp_OmitsStampSegment()
        {
            var result = MCPServer.BuildVersionString("", "0.37.0");
            Assert.AreEqual("proto:3|plugin:0.37.0", result);
        }

        [Test]
        public void GetVersion_NullStamp_OmitsStampSegment()
        {
            var result = MCPServer.BuildVersionString(null, "0.37.0");
            Assert.AreEqual("proto:3|plugin:0.37.0", result);
        }
    }
}
