using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentMissDetectorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ExtractAgentName_FromPayload_ReturnsName() =>
            Assert.AreEqual("senior-developer",
                AgentMissDetector.ExtractAgentName("Fix bug\n[agent:senior-developer]"));

        [Test]
        public void ExtractAgentName_NoAgentTag_ReturnsNull() =>
            Assert.IsNull(AgentMissDetector.ExtractAgentName("Fix bug"));

        [Test]
        public void ExtractAgentName_MultipleChips_ReturnsFirst() =>
            Assert.AreEqual("foo",
                AgentMissDetector.ExtractAgentName("[agent:foo][agent:bar]"));
    }
}
